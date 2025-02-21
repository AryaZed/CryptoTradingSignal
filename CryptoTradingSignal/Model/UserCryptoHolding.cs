using System.ComponentModel.DataAnnotations;

namespace CryptoTradingSignal.Model
{
    public class UserCryptoHolding
    {
        [Key]
        public int Id { get; set; }
        public string UserId { get; set; } // Unique user identifier
        public string Symbol { get; set; } // Crypto symbol (BTC, ETH, etc.)
        public float Amount { get; set; } // Amount in USD
    }
}
