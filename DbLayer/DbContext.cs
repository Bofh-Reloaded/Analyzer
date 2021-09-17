using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AnalyzerCore.DbLayer
{
    public class TokenContext : DbContext
    {
        public DbSet<Models.TokenDb> Tokens { get; set; }

        public string DbPath { get; private set; }

        public TokenContext()
        {
            const Environment.SpecialFolder folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = $"{path}{System.IO.Path.DirectorySeparatorChar.ToString()}missingTokens.db";
        }

        // The following configures EF to create a Sqlite database file in the
        // special "local" folder for your platform.
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");
    }
}