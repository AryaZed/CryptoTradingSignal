namespace CryptoTradingSignal.Helper;

public class TradingIndicators
{
    public static float CalculateSMA(List<float> prices, int period)
    {
        return prices.Count < period ? 0f : prices.TakeLast(period).Average();
    }

    public static float CalculateRSI(List<float> closes, int period = 14)
    {
        if (closes.Count < period) return 0f;

        float gain = 0, loss = 0;
        for (int i = 1; i < period; i++)
        {
            float change = closes[i] - closes[i - 1];
            if (change > 0) gain += change;
            else loss -= change;
        }

        if (loss == 0) return 100;
        float rs = gain / Math.Abs(loss);
        return 100 - (100 / (1 + rs));
    }

    public static float CalculateMACD(List<float> prices, int shortPeriod = 12, int longPeriod = 26)
    {
        float shortEMA = CalculateEMA(prices, shortPeriod);
        float longEMA = CalculateEMA(prices, longPeriod);
        return shortEMA - longEMA;
    }

    private static float CalculateEMA(List<float> prices, int period)
    {
        if (prices.Count < period) return 0f;
        float smoothing = 2f / (period + 1);
        float ema = prices.Take(period).Average();
        for (int i = period; i < prices.Count; i++)
            ema = (prices[i] - ema) * smoothing + ema;
        return ema;
    }

    public static float CalculateVolatility(List<float> closes, int period = 10)
    {
        if (closes.Count < period) return 0f;
        float mean = closes.TakeLast(period).Average();
        float variance = closes.TakeLast(period).Select(c => (c - mean) * (c - mean)).Sum() / period;
        return (float)Math.Sqrt(variance);
    }

    public static float CalculateMomentum(List<float> closes, int period = 10)
    {
        return closes.Count < period ? 0f : closes.Last() - closes[^period];
    }

    public static float CalculateTrendStrength(float sma, float macd)
    {
        return Math.Abs(macd / sma) * 100;
    }
}
