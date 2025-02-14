from web3 import Web3
from getpass import getpass
import argparse

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('-i', '--input-file', dest='inputFile', help='Input file containing one wallet address per line', type=str,required=True)
    parser.add_argument('-o', '--output-file', dest='outputFile', help='Output file to write balances to', type=str, required=True)
    parser.add_argument('--host', dest='host', help='URL for ethereum node (default: http://localhost:8545)', type=str, default='http://localhost:8545', required=False)

    args = parser.parse_args()

    web3 = Web3(Web3.HTTPProvider(args.host))
    if not web3.is_connected():
        print(f'Failed to connect to ethereum host: {args.host}')
        exit(1)

    inputFile = open(args.inputFile, 'r')
    outputFile = open(args.outputFile, "a+")

    for line in inputFile:
        if not line:
            break

        splitLine = line.split(",")

        guid = splitLine[0].strip()
        address = splitLine[1].strip()

        print(f"{address}")

        balance = web3.eth.get_balance(address)
        outputFile.write(f"{guid},{address},{web3.from_wei(balance, 'ether')}\n")

    outputFile.flush()
    outputFile.close()

if __name__ == '__main__':
    main()
