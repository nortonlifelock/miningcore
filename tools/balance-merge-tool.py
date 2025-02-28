from getpass import getpass
import argparse

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('-c', '--current-balance-file', dest='currentBalanceFile', help='Input file containing current balances', type=str,required=True)
    parser.add_argument('-p', '--pending-balance-file', dest='pendingBalanceFile', help='Input file containing pending balances', type=str,required=True)
    parser.add_argument('-o', '--output-file', dest='outputFile', help='Output file to write balances to', type=str, required=True)

    args = parser.parse_args()

    currentBalanceFile = open(args.currentBalanceFile, 'r')
    pendingBalanceFile = open(args.pendingBalanceFile, 'r')
    outputFile = open(args.outputFile, "a+")

    pendingBalanceMap = {}
    currentBalanceMap = {}
    totalBalanceMap = {}

    pendingBalanceSum = 0
    currentBalanceSum = 0
    totalBalanceSum = 0
    nonZeroAccounts = 0

    guidMap = {}

    for line in pendingBalanceFile:
        if not line:
            break

        splitLine = line.split(",")

        address = splitLine[0].strip().upper()
        balance = float(splitLine[1])

        totalBalanceMap[address] = balance
        pendingBalanceMap[address] = balance

        pendingBalanceSum = pendingBalanceSum + balance

    for line in currentBalanceFile:
        if not line:
            break

        splitLine = line.split(",")

        guid = splitLine[0].strip()
        address = splitLine[1].strip().upper()
        balance = float(splitLine[2].strip())

        currentBalanceMap[address] = balance

        if address in totalBalanceMap:
            totalBalanceMap[address] = totalBalanceMap[address] + balance
        else:
            totalBalanceMap[address] = balance

        guidMap[address] = guid

        currentBalanceSum = currentBalanceSum + balance

    for address in totalBalanceMap:
        guid = "NULL"
        if address in guidMap:
            guid = guidMap[address]
        
        totalBalance = totalBalanceMap[address]
        totalBalanceSum = totalBalanceSum + totalBalance
        pendingBalance = 0
        if address in pendingBalanceMap:
            pendingBalance = pendingBalanceMap[address]

        currentBalance = 0
        if address in currentBalanceMap:
            currentBalance = currentBalanceMap[address]

        if currentBalance > 0:
            nonZeroAccounts += 1

        outputFile.write(f"{guid},{address},{currentBalance:.20f},{pendingBalance:.20f},{totalBalance:.20f}\n")

    print("\n================================================")
    print(f"Total Non Zero Accounts: {nonZeroAccounts}")
    print(f"Total Current Balance: {currentBalanceSum:.20f}")
    print(f"Total Pending Balance: {pendingBalanceSum:.20f}")
    print(f"Total Combined Balance: {totalBalanceSum:.20f}")
    print("================================================\n")

    outputFile.flush()
    outputFile.close()

if __name__ == '__main__':
    main()
