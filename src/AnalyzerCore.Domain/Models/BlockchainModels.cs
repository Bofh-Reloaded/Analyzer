using System;
using System.Collections.Generic;
using System.Numerics;
using AnalyzerCore.Domain.ValueObjects;

namespace AnalyzerCore.Domain.Models
{
    public record BlockData
    {
        public BigInteger Number { get; init; }
        public string Hash { get; init; }
        public DateTime Timestamp { get; init; }
        public IEnumerable<TransactionInfo> Transactions { get; init; }
    }

    public record TransactionInfo
    {
        public string Hash { get; init; }
        public string From { get; init; }
        public string To { get; init; }
        public BigInteger Value { get; init; }
        public string Input { get; init; }
        public BigInteger GasUsed { get; init; }
        public bool Status { get; init; }
        public DateTime Timestamp { get; init; }
    }

    public record TokenInfo
    {
        public string Address { get; init; }
        public string Name { get; init; }
        public string Symbol { get; init; }
        public int Decimals { get; init; }
        public decimal TotalSupply { get; init; }
    }

    public record PoolInfo
    {
        public string Address { get; init; }
        public string Factory { get; init; }
        public string Token0 { get; init; }
        public string Token1 { get; init; }
        public decimal Reserve0 { get; init; }
        public decimal Reserve1 { get; init; }
        public PoolType Type { get; init; }
    }

    public record ChainConfig
    {
        public string ChainId { get; init; }
        public string Name { get; init; }
        public string RpcUrl { get; init; }
        public int RpcPort { get; init; }
        public string NativeCurrency { get; init; }
        public int BlockTime { get; init; }
        public int ConfirmationBlocks { get; init; }
        public IEnumerable<string> ExplorerUrls { get; init; }
    }
}