using System;

namespace AnalyzerCore.Domain.Entities
{
    public class Token
    {
        public Guid Id { get; private set; }
        public string Address { get; private set; }
        public string Symbol { get; private set; }
        public string Name { get; private set; }
        public int Decimals { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public string ChainId { get; private set; }

        private Token() { } // For EF Core

        public static Token Create(
            string address,
            string symbol,
            string name,
            int decimals,
            string chainId)
        {
            return new Token
            {
                Id = Guid.NewGuid(),
                Address = address?.ToLower() ?? throw new ArgumentNullException(nameof(address)),
                Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol)),
                Name = name ?? throw new ArgumentNullException(nameof(name)),
                Decimals = decimals,
                ChainId = chainId ?? throw new ArgumentNullException(nameof(chainId)),
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}