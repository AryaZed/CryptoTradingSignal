using CryptoTradingSignal.Data;
using CryptoTradingSignal.Helper;
using CryptoTradingSignal.ML;
using CryptoTradingSignal.Model;
using CryptoTradingSignal.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CryptoTradingSignal.Controllers;

[ApiController]
[Route("api/tradingview")]
public class TradingViewController : ControllerBase
{
    private readonly CoinMarketCapService _coinService;
    private readonly MLModel _mlModel;
    private readonly CryptoDbContext _dbContext;

    public TradingViewController(CoinMarketCapService coinService, MLModel mlModel, CryptoDbContext dbContext)
    {
        _coinService = coinService;
        _mlModel = mlModel;
        _dbContext = dbContext;
    }

    [HttpPost("signal")]
    public async Task<IActionResult> ReceiveSignal([FromBody] JsonElement payload)
    {
        if (!payload.TryGetProperty("symbol", out var symbolElement) || string.IsNullOrEmpty(symbolElement.GetString()))
            return BadRequest("Symbol is required.");

        string symbol = symbolElement.GetString();

        if (string.IsNullOrEmpty(symbol))
            return BadRequest("Symbol is required.");

        string marketData = await _coinService.GetCryptoPriceRaw(symbol);
        if (string.IsNullOrEmpty(marketData))
        {
            Console.WriteLine($"⚠️ Error fetching market data for {symbol}.");
            return StatusCode(500, "Error fetching market data.");
        }

        try
        {
            float price = ExtractJsonValue(marketData, symbol, "price");
            float volume = ExtractJsonValue(marketData, symbol, "volume_24h");

            Console.WriteLine($"✅ Extracted {symbol} Price: {price}, Volume: {volume}");

            return Ok(new { symbol, price, volume, marketData });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error processing market data: {ex.Message}");
            return StatusCode(500, "Error processing market data.");
        }
    }

    [HttpPost("train")]
    public async Task<IActionResult> TrainModel([FromBody] JsonElement payload)
    {
        if (!payload.TryGetProperty("symbol", out var symbolElement) || string.IsNullOrEmpty(symbolElement.GetString()))
            return BadRequest("Symbol is required.");

        string? symbol = symbolElement.GetString();

        if (string.IsNullOrEmpty(symbol))
            return BadRequest("Symbol is required.");

        int days = 30;

        string jsonData = await _coinService.GetHistoricalData(symbol, days);
        var trainingData = ParseHistoricalData(jsonData, symbol);

        if (trainingData.Count == 0)
            return BadRequest("No valid data available for training.");

        _dbContext.CryptoHistory.AddRange(trainingData);
        await _dbContext.SaveChangesAsync();

        _mlModel.TrainModel(trainingData);

        return Ok(new { message = "Model trained successfully", dataCount = trainingData.Count });
    }

    [HttpPost("predict")]
    public async Task<IActionResult> PredictSignal([FromBody] JsonElement payload)
    {
        if (!payload.TryGetProperty("symbol", out var symbolElement) || string.IsNullOrEmpty(symbolElement.GetString()))
            return BadRequest("Symbol is required.");

        string symbol = symbolElement.GetString();

        string marketData = await _coinService.GetCryptoPriceRaw(symbol);
        if (string.IsNullOrEmpty(marketData))
            return StatusCode(500, "Error fetching market data.");

        float open = ExtractJsonValue(marketData, symbol, "open");
        float high = ExtractJsonValue(marketData, symbol, "high");
        float low = ExtractJsonValue(marketData, symbol, "low");
        float close = ExtractJsonValue(marketData, symbol, "close");
        float volume = ExtractJsonValue(marketData, symbol, "volume_24h");

        List<float> closes = new List<float> { open, high, low, close };

        float sma = TradingIndicators.CalculateSMA(closes, 20);
        float rsi = TradingIndicators.CalculateRSI(closes);
        float macd = TradingIndicators.CalculateMACD(closes);
        float volatility = TradingIndicators.CalculateVolatility(closes);
        float momentum = TradingIndicators.CalculateMomentum(closes);
        float trendStrength = TradingIndicators.CalculateTrendStrength(sma, macd);

        string prediction = _mlModel.Predict(open, high, low, close, volume, sma, rsi, macd, volatility, momentum, trendStrength);

        return Ok(new { symbol, open, high, low, close, volume, decision = prediction });
    }

    /// <summary>
    /// Parses historical data and extracts indicators.
    /// </summary>
    private List<CryptoHistoricalData> ParseHistoricalData(string json, string symbol)
    {
        var data = new List<CryptoHistoricalData>();
        var closes = new List<float>();

        try
        {
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var dataElement) ||
                !dataElement.TryGetProperty("quotes", out var quotesElement))
                return data;

            foreach (var entry in quotesElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("quote", out var quoteElement) ||
                    !quoteElement.TryGetProperty("USD", out var usdElement))
                    continue;

                float open = GetSafeFloat(usdElement, "open");
                float high = GetSafeFloat(usdElement, "high");
                float low = GetSafeFloat(usdElement, "low");
                float close = GetSafeFloat(usdElement, "close");
                float volume = GetSafeFloat(usdElement, "volume");

                closes.Add(close);
                float sma = TradingIndicators.CalculateSMA(closes, 20);
                float rsi = TradingIndicators.CalculateRSI(closes);

                string signal = rsi > 70 ? "sell" : rsi < 30 ? "buy" : "hold";

                data.Add(new CryptoHistoricalData
                {
                    Symbol = symbol,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume,
                    Signal = signal
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing historical data: {ex.Message}");
        }

        return data;
    }

    /// <summary>
    /// Extracts a float value safely from JSON.
    /// </summary>
    private static float GetSafeFloat(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetSingle()
            : 0f;
    }

    /// <summary>
    /// Extracts a price-related value from JSON for a given symbol.
    /// </summary>
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
