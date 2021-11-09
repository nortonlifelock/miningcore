/*
Copyright 2017 Coin Foundry (coinfoundry.org)
Authors: Oliver Weichhold (oliver@weichhold.com)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial
portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace Miningcore.Blockchain.Ethereum.Configuration
{
    public class EthereumPoolPaymentProcessingConfigExtra
    {
        /// <summary>
        /// Password of the daemons wallet (needed for processing payouts)
        /// </summary>
        public string CoinbasePassword { get; set; }

        /// <summary>
        /// True to exempt transaction fees from miner rewards
        /// </summary>
        public bool KeepTransactionFees { get; set; }

        /// <summary>
        /// True to exempt uncle rewards from miner rewards
        /// </summary>
        public bool KeepUncles { get; set; }

        /// <summary>
        /// Gas provided for the transaction execution
        /// </summary>
        public ulong Gas { get; set; }

        /// <summary>
        /// Gas price check has to be enforced or not
        /// </summary>
        public bool EnableGasLimit { get; set; }

        /// <summary>
        /// Gas price allowed for the transaction within specified time frame since last payout for the miner
        /// </summary>
        public ulong GasLimit { get; set; }

        /// <summary>
        /// Time frame to use above gas limit since last payout for the miner
        /// </summary>
        public int GasLimitToleranceHrs { get; set; }

        /// <summary>
        /// Percentage as a decimal value between 0 and 100
        /// </summary>
        public decimal GasDeductionPercentage { get; set; }

        /// <summary>
        /// Maximum gas price allowed for the transaction outside the specified time frame since last payout for the miner
        /// </summary>
        public ulong MaxGasLimit { get; set; }

        /// <summary>
        /// Gas price factor used to pay top miners
        /// </summary>
        public float TopMinersGasLimitFactor { get; set; }

        /// <summary>
        /// No of transactions to spawn on each on-demand payout cycle
        /// </summary>
        public int PayoutBatchSize { get; set; }

        /// <summary>
        /// if True, miners pay payment tx fees
        /// </summary>
        public bool MinersPayTxFees { get; set; }

        /// <summary>
        /// Hex encoded private key
        /// </summary>
        public string PrivateKey { get; set; }
    }
}
