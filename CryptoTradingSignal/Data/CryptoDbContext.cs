using CryptoTradingSignal.Model;
using Microsoft.EntityFrameworkCore;

namespace CryptoTradingSignal.Data
{
    public class CryptoDbContext : DbContext
    {
        public DbSet<CryptoHistoricalData> CryptoHistory { get; set; }
        public DbSet<UserCryptoHolding> UserHoldings { get; set; }

        public CryptoDbContext(DbContextOptions<CryptoDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite("Data Source=crypto.db");
        }
    }
}
