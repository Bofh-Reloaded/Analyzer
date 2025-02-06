using System;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnalyzerCore.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        private readonly ILogger<ApplicationDbContext> _logger;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            ILogger<ApplicationDbContext> logger)
            : base(options)
        {
            _logger = logger;
        }

        public DbSet<Token> Tokens { get; set; }
        public DbSet<Pool> Pools { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Token>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Address, e.ChainId }).IsUnique();
                
                entity.Property(e => e.Address)
                    .IsRequired()
                    .HasMaxLength(42);
                
                entity.Property(e => e.Symbol)
                    .IsRequired()
                    .HasMaxLength(20);
                
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);
                
                entity.Property(e => e.ChainId)
                    .IsRequired()
                    .HasMaxLength(10);
            });

            modelBuilder.Entity<Pool>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Address, e.Factory }).IsUnique();
                
                entity.Property(e => e.Address)
                    .IsRequired()
                    .HasMaxLength(42);
                
                entity.Property(e => e.Factory)
                    .IsRequired()
                    .HasMaxLength(42);
                
                entity.Property(e => e.Reserve0)
                    .HasPrecision(36, 18);
                
                entity.Property(e => e.Reserve1)
                    .HasPrecision(36, 18);

                entity.HasOne(e => e.Token0)
                    .WithMany()
                    .HasForeignKey("Token0Id")
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Token1)
                    .WithMany()
                    .HasForeignKey("Token1Id")
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving changes to database");
                throw;
            }
        }
    }
}