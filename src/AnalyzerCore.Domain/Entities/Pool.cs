using System;
using AnalyzerCore.Domain.ValueObjects;

namespace AnalyzerCore.Domain.Entities
{
    public class Pool
    {
        public Guid Id { get; private set; }
        public string Address { get; private set; }
        public Token Token0 { get; private set; }
        public Token Token1 { get; private set; }
        public decimal Reserve0 { get; private set; }
        public decimal Reserve1 { get; private set; }
        public PoolType Type { get; private set; }
        public string Factory { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private Pool() { } // For EF Core

        public static Pool Create(
            string address,
            Token token0,
            Token token1,
            decimal reserve0,
            decimal reserve1,
            PoolType type,
            string factory)
        {
            if (token0 == null) throw new ArgumentNullException(nameof(token0));
            if (token1 == null) throw new ArgumentNullException(nameof(token1));
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentNullException(nameof(address));
            if (string.IsNullOrWhiteSpace(factory)) throw new ArgumentNullException(nameof(factory));
            if (reserve0 < 0) throw new ArgumentException("Reserve0 cannot be negative", nameof(reserve0));
            if (reserve1 < 0) throw new ArgumentException("Reserve1 cannot be negative", nameof(reserve1));

            return new Pool
            {
                Id = Guid.NewGuid(),
                Address = address.ToLower(),
                Token0 = token0,
                Token1 = token1,
                Reserve0 = reserve0,
                Reserve1 = reserve1,
                Type = type,
                Factory = factory.ToLower(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void UpdateReserves(decimal reserve0, decimal reserve1)
        {
            if (reserve0 < 0) throw new ArgumentException("Reserve0 cannot be negative", nameof(reserve0));
            if (reserve1 < 0) throw new ArgumentException("Reserve1 cannot be negative", nameof(reserve1));

            Reserve0 = reserve0;
            Reserve1 = reserve1;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}