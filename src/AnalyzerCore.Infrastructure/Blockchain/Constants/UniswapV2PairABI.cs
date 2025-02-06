namespace AnalyzerCore.Infrastructure.Blockchain
{
    internal static class UniswapV2PairABI
    {
        public const string ABI = @"[
            {
                'constant': true,
                'inputs': [],
                'name': 'token0',
                'outputs': [{'name': '', 'type': 'address'}],
                'payable': false,
                'stateMutability': 'view',
                'type': 'function'
            },
            {
                'constant': true,
                'inputs': [],
                'name': 'token1',
                'outputs': [{'name': '', 'type': 'address'}],
                'payable': false,
                'stateMutability': 'view',
                'type': 'function'
            },
            {
                'constant': true,
                'inputs': [],
                'name': 'factory',
                'outputs': [{'name': '', 'type': 'address'}],
                'payable': false,
                'stateMutability': 'view',
                'type': 'function'
            },
            {
                'constant': true,
                'inputs': [],
                'name': 'getReserves',
                'outputs': [
                    {'name': '_reserve0', 'type': 'uint112'},
                    {'name': '_reserve1', 'type': 'uint112'},
                    {'name': '_blockTimestampLast', 'type': 'uint32'}
                ],
                'payable': false,
                'stateMutability': 'view',
                'type': 'function'
            },
            {
                'constant': true,
                'inputs': [],
                'name': 'price0CumulativeLast',
                'outputs': [{'name': '', 'type': 'uint256'}],
                'payable': false,
                'stateMutability': 'view',
                'type': 'function'
            },
            {
                'constant': true,
                'inputs': [],
                'name': 'price1CumulativeLast',
                'outputs': [{'name': '', 'type': 'uint256'}],
                'payable': false,
                'stateMutability': 'view',
                'type': 'function'
            },
            {
                'anonymous': false,
                'inputs': [
                    {'indexed': true, 'name': 'sender', 'type': 'address'},
                    {'indexed': false, 'name': 'amount0', 'type': 'uint256'},
                    {'indexed': false, 'name': 'amount1', 'type': 'uint256'},
                    {'indexed': true, 'name': 'to', 'type': 'address'}
                ],
                'name': 'Mint',
                'type': 'event'
            },
            {
                'anonymous': false,
                'inputs': [
                    {'indexed': true, 'name': 'sender', 'type': 'address'},
                    {'indexed': false, 'name': 'amount0', 'type': 'uint256'},
                    {'indexed': false, 'name': 'amount1', 'type': 'uint256'},
                    {'indexed': true, 'name': 'to', 'type': 'address'}
                ],
                'name': 'Burn',
                'type': 'event'
            },
            {
                'anonymous': false,
                'inputs': [
                    {'indexed': true, 'name': 'sender', 'type': 'address'},
                    {'indexed': false, 'name': 'amount0In', 'type': 'uint256'},
                    {'indexed': false, 'name': 'amount1In', 'type': 'uint256'},
                    {'indexed': false, 'name': 'amount0Out', 'type': 'uint256'},
                    {'indexed': false, 'name': 'amount1Out', 'type': 'uint256'},
                    {'indexed': true, 'name': 'to', 'type': 'address'}
                ],
                'name': 'Swap',
                'type': 'event'
            },
            {
                'anonymous': false,
                'inputs': [
                    {'indexed': false, 'name': 'reserve0', 'type': 'uint112'},
                    {'indexed': false, 'name': 'reserve1', 'type': 'uint112'}
                ],
                'name': 'Sync',
                'type': 'event'
            }
        ]";
    }
}