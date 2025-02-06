using System;

namespace AnalyzerCore.Domain.Entities
{
    public class Token
    {
        private Token() { }  // For EF Core

        public int Id { get; set; }
        public string Address { get; set; } = null!;
        public string Symbol { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Decimals { get; set; }
        public decimal TotalSupply { get; set; }
        public string ChainId { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public static Token Create(
            string address,
            string symbol,
            string name,
            int decimals,
            decimal totalSupply,
            string chainId)
        {
            return new Token
            {
                Address = address.ToLowerInvariant(),
                Symbol = symbol.ToUpperInvariant(),
                Name = name,
                Decimals = decimals,
                TotalSupply = totalSupply,
                ChainId = chainId,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}