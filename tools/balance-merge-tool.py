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

    balanceMap = {}
    guidMap = {}

    for line in pendingBalanceFile:
        if not line:
            break

        splitLine = line.split(",")

        address = splitLine[0].strip().upper()
        balance = float(splitLine[1])

        balanceMap[address] = balance

    for line in currentBalanceFile:
        if not line:
            break

        splitLine = line.split(",")

        guid = splitLine[0].strip()
        address = splitLine[1].strip().upper()
        balance = float(splitLine[2].strip())

        if address in balanceMap:
            balanceMap[address] = balanceMap[address] + balance
        else:
            balanceMap[address] = balance

        guidMap[address] = guid

    for address in balanceMap:
        guid = "NULL"
        if address in guidMap:
            guid = guidMap[address]
        
        totalBalance = balanceMap[address]
        outputFile.write(f"{guid},{address},{totalBalance:.20f}\n")

    outputFile.flush()
    outputFile.close()

if __name__ == '__main__':
    main()
