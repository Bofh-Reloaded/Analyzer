using System;
using AnalyzerCore.Domain.ValueObjects;

namespace AnalyzerCore.Domain.Entities
{
    public class Pool
    {
        private Pool() { }  // For EF Core

        public int Id { get; set; }
        public string Address { get; set; } = null!;
        public Token Token0 { get; set; } = null!;
        public Token Token1 { get; set; } = null!;
        public string Factory { get; set; } = null!;
        public decimal Reserve0 { get; set; }
        public decimal Reserve1 { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public PoolType Type { get; set; } = PoolType.UniswapV2;

        public static Pool Create(
            string address,
            Token token0,
            Token token1,
            string factory,
            PoolType type = PoolType.UniswapV2)
        {
            return new Pool
            {
                Address = address.ToLowerInvariant(),
                Token0 = token0,
                Token1 = token1,
                Factory = factory.ToLowerInvariant(),
                Type = type,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };
        }

        public void UpdateReserves(decimal reserve0, decimal reserve1)
        {
            Reserve0 = reserve0;
            Reserve1 = reserve1;
            LastUpdated = DateTime.UtcNow;
        }
    }
}