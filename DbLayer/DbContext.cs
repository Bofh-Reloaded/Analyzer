using Microsoft.EntityFrameworkCore;
using DbContext = Microsoft.EntityFrameworkCore.DbContext;


namespace AnalyzerCore.DbLayer
{
    public class TokenDbContext : DbContext
    {
        public TokenDbContext()
        {

        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source=.\\local.db");

        private string DbPath
        { get; set; }


        public DbSet<Models.TokenEntity> Tokens { get; set; }
        public DbSet<Models.Pool> Pools { get; set; }
        public DbSet<Models.Exchange> Exchanges { get; set; }
        public DbSet<Models.TransactionHash> TransactionHashes { get; set; }
    }
}