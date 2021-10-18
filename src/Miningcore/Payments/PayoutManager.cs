/*
MiningCore 2.0
Copyright 2021 MinerNL (Miningcore.com)
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Features.Metadata;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.Messaging;
using Miningcore.Notifications.Messages;
using Miningcore.Persistence;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Repositories;
using Miningcore.Util;
using NLog;
using Contract = Miningcore.Contracts.Contract;

namespace Miningcore.Payments
{
    /// <summary>
    /// Coin agnostic payment processor
    /// </summary>
    public class PayoutManager
    {
        public PayoutManager(IComponentContext ctx,
            IConnectionFactory cf,
            IBlockRepository blockRepo,
            IShareRepository shareRepo,
            IBalanceRepository balanceRepo,
            IMessageBus messageBus)
        {
            Contract.RequiresNonNull(ctx, nameof(ctx));
            Contract.RequiresNonNull(cf, nameof(cf));
            Contract.RequiresNonNull(blockRepo, nameof(blockRepo));
            Contract.RequiresNonNull(shareRepo, nameof(shareRepo));
            Contract.RequiresNonNull(balanceRepo, nameof(balanceRepo));
            Contract.RequiresNonNull(messageBus, nameof(messageBus));

            this.ctx = ctx;
            this.cf = cf;
            this.blockRepo = blockRepo;
            this.shareRepo = shareRepo;
            this.balanceRepo = balanceRepo;
            this.messageBus = messageBus;
        }

        private readonly IBalanceRepository balanceRepo;
        private readonly IBlockRepository blockRepo;
        private readonly IConnectionFactory cf;
        private readonly IComponentContext ctx;
        private readonly IShareRepository shareRepo;
        private readonly IMessageBus messageBus;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private TimeSpan interval;
        private ClusterConfig clusterConfig;
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
        private static Thread payoutThread;

        // Start Payment Services
        public void Start()
        {
            logger.Info(() => "Starting Payout Manager");

            // The observable will trigger the observer once every interval
            Observable.Interval(interval).Subscribe(_ => PayoutCallback());

            // Run the initial payout
            PayoutCallback();
        }

        public void PayoutCallback() 
        {
            if (!cts.IsCancellationRequested)
            {
                if (null == payoutThread || !payoutThread.IsAlive)
                {
                    // Spin up a separate thread to do the current payout
                    logger.Info(() => "Creating a thread to process the current payouts");
                    payoutThread = new Thread(() => ProcessPayout());
                    payoutThread.Start();
                }
                else
                {
                    logger.Info(() => "Payout thread is null or still alive");
                }
            }
            else
            {
                logger.Info(() => "cts.IsCancellationRequested is true, stopping payout manager");
            }
        }

        public async void ProcessPayout()
        {
            foreach(var pool in clusterConfig.Pools.Where(x => x.Enabled && x.PaymentProcessing.Enabled))
            {
                logger.Info(() => $"Processing payments for pool [{pool.Id}]");

                try
                {
                    var handler = await ResolveAndConfigurePayoutHandlerAsync(pool);

                    // resolve payout scheme
                    var scheme = ctx.ResolveKeyed<IPayoutScheme>(pool.PaymentProcessing.PayoutScheme);

                    if(pool.PaymentProcessing.BalanceUpdateEnabled) await UpdatePoolBalancesAsync(pool, handler, scheme);
                    if(pool.PaymentProcessing.PayoutEnabled) await PayoutPoolBalancesAsync(pool, handler);

                    var poolBalance = await TelemetryUtil.TrackDependency(() => cf.Run(con => balanceRepo.GetTotalBalanceSum(con, pool.Id)),
                        DependencyType.Sql, "GetTotalBalanceSum", "GetTotalBalanceSum");

                    var tc = TelemetryUtil.GetTelemetryClient();
                    tc?.GetMetric("TotalBalance_" + pool.Id).TrackValue((double)poolBalance);
                }
                catch(InvalidOperationException ex)
                {
                    logger.Error(ex.InnerException ?? ex, () => $"[{pool.Id}] Payment processing failed");
                }
                catch(AggregateException ex)
                {
                    switch(ex.InnerException)
                    {
                        case HttpRequestException httpEx:
                            logger.Error(() => $"[{pool.Id}] Payment processing failed: {httpEx.Message}");
                            break;

                        default:
                            logger.Error(ex.InnerException, () => $"[{pool.Id}] Payment processing failed");
                            break;
                    }
                }
                catch(Exception ex)
                {
                    logger.Error(ex, () => $"[{pool.Id}] Payment processing failed");
                }
            }
        }

        public async Task<string> PayoutSingleBalanceAsync(PoolConfig pool, string miner)
        {
            var success = true;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Decimal amount = 0;
            IPayoutHandler handler = null;
            try
            {
                handler = await ResolveAndConfigurePayoutHandlerAsync(pool);
                var balance = await cf.Run(con => balanceRepo.GetBalanceWithPaidDateAsync(con, pool.Id, miner));
                amount = balance.Amount;
                return await handler.PayoutAsync(balance);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to payout {miner}: {ex}");
                success = false;
                throw;
            }
            finally
            {
                TelemetryUtil.GetTelemetryClient()?.GetMetric("FORCED_PAYOUT", "success", "duration").TrackValue((double)amount, success.ToString(), timer.ElapsedMilliseconds.ToString());
            }
        }


        private static CoinFamily HandleFamilyOverride(CoinFamily family, PoolConfig pool)
        {
            switch(family)
            {
                case CoinFamily.Equihash:
                    var equihashTemplate = pool.Template.As<EquihashCoinTemplate>();

                    if(equihashTemplate.UseBitcoinPayoutHandler)
                        return CoinFamily.Bitcoin;

                    break;
            }

            return family;
        }

        private async Task UpdatePoolBalancesAsync(PoolConfig pool, IPayoutHandler handler, IPayoutScheme scheme)
        {
            // get pending blockRepo for pool

            var pendingBlocks = await TelemetryUtil.TrackDependency(() => cf.Run(con => blockRepo.GetPendingBlocksForPoolAsync(con, pool.Id)),
                DependencyType.Sql, "getPendingBlocks", "getPendingBlocks");

            // classify
            var updatedBlocks = await handler.ClassifyBlocksAsync(pendingBlocks);

            if(updatedBlocks.Any())
            {
                foreach(var block in updatedBlocks.OrderBy(x => x.Created))
                {
                    logger.Info(() => $"Processing payments for pool {pool.Id}, block {block.BlockHeight}, effort {block.Effort}");


                    await cf.RunTx(async (con, tx) =>
                    {
                        if(!block.Effort.HasValue)  // fill block effort if empty
                            await CalculateBlockEffortAsync(pool, block, handler);

                        switch(block.Status)
                        {
                            case BlockStatus.Confirmed:
                                // blockchains that do not support block-reward payments via coinbase Tx
                                // must generate balance records for all reward recipients instead
                                var blockReward = await handler.UpdateBlockRewardBalancesAsync(con, tx, block, pool);

                                logger.Info(() => $" --Pool {pool}");
                                logger.Info(() => $" --Block {block}");
                                logger.Info(() => $" --Block reward {blockReward}");

                                logger.Info(() => $" --Con {con}");
                                logger.Info(() => $" --tx {tx}");

                                await scheme.UpdateBalancesAsync(con, tx, pool, clusterConfig, handler, block, blockReward);
                                await blockRepo.UpdateBlockAsync(con, tx, block);
                                break;

                            case BlockStatus.Orphaned:
                                await blockRepo.UpdateBlockAsync(con, tx, block);
                                break;

                            case BlockStatus.Pending:
                                await blockRepo.UpdateBlockAsync(con, tx, block);
                                break;
                        }
                    });
                }
            }
            else
            {
                logger.Info(() => $"No updated blocks for pool {pool.Id} but still payment processed");
                await TelemetryUtil.TrackDependency(() => cf.RunTx(async (con, tx) =>
                {
                    var blockReward = await handler.UpdateBlockRewardBalancesAsync(con, tx, null, pool);
                    await scheme.UpdateBalancesAsync(con, tx, pool, clusterConfig, handler, null, blockReward);
                }), DependencyType.Sql, "UpdateBalancesAsync", "UpdateBalances");
            }
        }

        private async Task PayoutPoolBalancesAsync(PoolConfig pool, IPayoutHandler handler)
        {
            var poolBalancesOverMinimum = await TelemetryUtil.TrackDependency(() => cf.Run(con =>
                    balanceRepo.GetPoolBalancesOverThresholdAsync(con, pool.Id, pool.PaymentProcessing.MinimumPayment)),
                    DependencyType.Sql, "GetPoolBalancesOverThresholdAsync", "GetPoolBalancesOverThresholdAsync");

            if(poolBalancesOverMinimum.Length > 0)
            {
                try
                {
                    await handler.PayoutAsync(poolBalancesOverMinimum);
                }

                catch(Exception ex)
                {
                    await NotifyPayoutFailureAsync(poolBalancesOverMinimum, pool, ex);
                    throw;
                }
            }
            else
                logger.Info(() => $"No balances over configured minimum payout {pool.PaymentProcessing.MinimumPayment:0.#######} for pool {pool.Id}");
        }

        private Task NotifyPayoutFailureAsync(Balance[] balances, PoolConfig pool, Exception ex)
        {
            messageBus.SendMessage(new PaymentNotification(pool.Id, ex.Message, balances.Sum(x => x.Amount), pool.Template.Symbol));

            return Task.FromResult(true);
        }

        private async Task CalculateBlockEffortAsync(PoolConfig pool, Block block, IPayoutHandler handler)
        {
            logger.Info(() => $"Calculate Block Effort");

            // get share date-range
            var from = DateTime.MinValue;
            var to = block.Created;

            // get last block for pool
            var lastBlock = await cf.Run(con => blockRepo.GetBlockBeforeAsync(con, pool.Id, new[]
            {
                BlockStatus.Confirmed,
                BlockStatus.Orphaned,
                BlockStatus.Pending,
            }, block.Created));

            if(lastBlock != null)
                from = lastBlock.Created;

            // get combined diff of all shares for block
            var accumulatedShareDiffForBlock = await cf.Run(con =>
                shareRepo.GetAccumulatedShareDifficultyBetweenCreatedAsync(con, pool.Id, from, to));

            // handler has the final say
            if(accumulatedShareDiffForBlock.HasValue)
                await handler.CalculateBlockEffortAsync(block, accumulatedShareDiffForBlock.Value);
        }

        private async Task<IPayoutHandler> ResolveAndConfigurePayoutHandlerAsync(PoolConfig pool)
        {
            var family = HandleFamilyOverride(pool.Template.Family, pool);

            // resolve payout handler
            var handlerImpl = ctx.Resolve<IEnumerable<Meta<Lazy<IPayoutHandler, CoinFamilyAttribute>>>>()
                .First(x => x.Value.Metadata.SupportedFamilies.Contains(family)).Value;

            var handler = handlerImpl.Value;
            await handler.ConfigureAsync(clusterConfig, pool);

            return handler;
        }


        internal void Configure(ClusterConfig clusterConfig)
        {
            this.clusterConfig = clusterConfig;
            interval = TimeSpan.FromSeconds(clusterConfig.PaymentProcessing.Interval > 0 ? clusterConfig.PaymentProcessing.Interval : 600);
        }

        public void Stop()
        {
            logger.Info(() => "Payments Service Stopping ..");

            cts.Cancel();

            logger.Info(() => "Payment Service Stopped");
        }
    }
}
