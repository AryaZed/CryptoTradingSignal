using System.ComponentModel.DataAnnotations;

namespace CryptoTradingSignal.Model
{
    public class CryptoHistoricalData
    {
        [Key]
        public int Id { get; set; }
        public string Symbol { get; set; }
        public float Open { get; set; }
        public float High { get; set; }
        public float Low { get; set; }
        public float Close { get; set; }
        public float Volume { get; set; }
        public string Signal { get; set; } // Buy or Sell
        public float SMA { get; internal set; }
        public float RSI { get; internal set; }
        public float MACD { get; internal set; }
        public float Volatility { get; internal set; }
        public float Momentum { get; internal set; }
        public float TrendStrength { get; internal set; }
    }
}
