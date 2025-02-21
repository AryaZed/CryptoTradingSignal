using CryptoTradingSignal.Data;
using CryptoTradingSignal.Helper;
using CryptoTradingSignal.ML;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CryptoTradingSignal.Service;

public class CryptoSignalService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MLModel _mlModel;
    private readonly NotificationService _notificationService;

    public CryptoSignalService(IServiceScopeFactory scopeFactory, MLModel mlModel, NotificationService notificationService)
    {
        _scopeFactory = scopeFactory;
        _mlModel = mlModel;
        _notificationService = notificationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("🔄 Checking Crypto Market...");

            using (var scope = _scopeFactory.CreateScope())
            {
                var coinService = scope.ServiceProvider.GetRequiredService<CoinMarketCapService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<CryptoDbContext>();

                var trendingCryptos = await coinService.GetTrendingCryptos();
                var userHoldings = await dbContext.UserHoldings.ToListAsync();

                foreach (var holding in userHoldings)
                {
                    Console.WriteLine($"📈 Analyzing {holding.Symbol}...");

                    string marketData = await coinService.GetCryptoPriceRaw(holding.Symbol);
                    if (string.IsNullOrEmpty(marketData)) continue;

                    float open = ExtractJsonValue(marketData, holding.Symbol, "open");
                    float high = ExtractJsonValue(marketData, holding.Symbol, "high");
                    float low = ExtractJsonValue(marketData, holding.Symbol, "low");
                    float close = ExtractJsonValue(marketData, holding.Symbol, "close");
                    float volume = ExtractJsonValue(marketData, holding.Symbol, "volume_24h");

                    List<float> closes = new List<float> { open, high, low, close };
                    float sma = TradingIndicators.CalculateSMA(closes, 20);
                    float rsi = TradingIndicators.CalculateRSI(closes);
                    float macd = TradingIndicators.CalculateMACD(closes);
                    float volatility = TradingIndicators.CalculateVolatility(closes);
                    float momentum = TradingIndicators.CalculateMomentum(closes);
                    float trendStrength = TradingIndicators.CalculateTrendStrength(sma, macd);

                    string prediction = _mlModel.Predict(open, high, low, close, volume, sma, rsi, macd, volatility, momentum, trendStrength);

                    if (prediction == "buy")
                        await _notificationService.SendNotification($"🚀 Buy Signal: {holding.Symbol}!");
                    else if (prediction == "sell")
                        await _notificationService.SendNotification($"⚠️ Sell Signal: {holding.Symbol}!");
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    private float ExtractJsonValue(string json, string symbol, string property)
    {
        try
        {
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var dataElement) ||
                !dataElement.TryGetProperty(symbol, out var symbolElement) ||
                !symbolElement.TryGetProperty("quote", out var quoteElement) ||
                !quoteElement.TryGetProperty("USD", out var usdElement) ||
                !usdElement.TryGetProperty(property, out var valueElement))
            {
                Console.WriteLine($"⚠️ Missing property '{property}' in API response for {symbol}");
                return 0f;  // Default to 0 if missing
            }

            return valueElement.GetSingle();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error extracting '{property}' for {symbol}: {ex.Message}");
            return 0f;
        }
    }
}