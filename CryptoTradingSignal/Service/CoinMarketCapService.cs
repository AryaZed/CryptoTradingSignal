using CryptoTradingSignal.Configuation;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using CryptoTradingSignal.Model;
using Microsoft.EntityFrameworkCore;
using CryptoTradingSignal.Data;
using System.Linq;

namespace CryptoTradingSignal.Service
{
    public class CoinMarketCapService
    {
        private readonly HttpClient _httpClient;
        private readonly CryptoDbContext _dbContext;
        private readonly string _apiKey;
        private readonly string _baseUrl;

        public CoinMarketCapService(HttpClient httpClient, IOptions<CoinMarketCapSettings> options, CryptoDbContext dbContext)
        {
            _httpClient = httpClient;
            _apiKey = options.Value.ApiKey;
            _baseUrl = options.Value.BaseUrl;
            _dbContext = dbContext;

            _httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<string> GetCryptoPriceRaw(string symbol)
        {
            return await GetApiResponse($"/v1/cryptocurrency/quotes/latest?symbol={symbol}");
        }

        public async Task<Dictionary<string, object>> GetCryptoPrice(string symbol)
        {
            string json = await GetCryptoPriceRaw(symbol);
            return ExtractCryptoPrice(json, symbol);
        }

        public async Task<Dictionary<string, decimal>> GetMultipleCryptoPrices(string[] symbols)
        {
            string symbolList = string.Join(",", symbols);
            string json = await GetApiResponse($"/v1/cryptocurrency/quotes/latest?symbol={symbolList}");
            return ExtractMultipleCryptoPrices(json, symbols);
        }

        public async Task<string> GetHistoricalData(string symbol, int days = 30)
        {
            return await GetApiResponse($"/v1/cryptocurrency/ohlcv/historical?symbol={symbol}&count={days}&interval=daily");
        }

        public async Task<List<string>> GetTopCryptos(int limit = 10)
        {
            string json = await GetApiResponse($"/v1/cryptocurrency/listings/latest?limit={limit}");
            return ExtractTrendingCryptoSymbols(json);
        }

        public async Task<string> GetCryptoMetadata(string symbol)
        {
            return await GetApiResponse($"/v1/cryptocurrency/info?symbol={symbol}");
        }

        public async Task<List<string>> GetTrendingCryptos(int limit = 5)
        {
            string json = await GetApiResponse($"/v1/cryptocurrency/trending/latest?limit={limit}");
            return ExtractTrendingCryptoSymbols(json);
        }

        public async Task<List<string>> GetMostViewedCryptos(int limit = 5)
        {
            string json = await GetApiResponse($"/v1/cryptocurrency/trending/gainers-losers?limit={limit}");
            return ExtractTrendingCryptoSymbols(json);
        }

        public async Task<Dictionary<string, float>> GetUserCryptoHoldings(string userId)
        {
            var holdings = await _dbContext.UserHoldings
                .Where(h => h.UserId == userId)
                .ToListAsync();

            return holdings.ToDictionary(h => h.Symbol, h => h.Amount);
        }

        public async Task AddOrUpdateHolding(string userId, string symbol, float amount)
        {
            var holding = await _dbContext.UserHoldings
                .FirstOrDefaultAsync(h => h.UserId == userId && h.Symbol == symbol);

            if (holding == null)
            {
                _dbContext.UserHoldings.Add(new UserCryptoHolding
                {
                    UserId = userId,
                    Symbol = symbol,
                    Amount = amount
                });
            }
            else
            {
                holding.Amount = amount;
                _dbContext.UserHoldings.Update(holding);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task RemoveHolding(string userId, string symbol)
        {
            var holding = await _dbContext.UserHoldings
                .FirstOrDefaultAsync(h => h.UserId == userId && h.Symbol == symbol);

            if (holding != null)
            {
                _dbContext.UserHoldings.Remove(holding);
                await _dbContext.SaveChangesAsync();
            }
        }

        private async Task<string> GetApiResponse(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");

                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"⚠️ CoinMarketCap API Error: {errorMessage}");
                    return null;
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ API Request Failed: {ex.Message}");
                return null;
            }
        }

        private Dictionary<string, object> ExtractCryptoPrice(string json, string symbol)
        {
            var result = new Dictionary<string, object>();

            if (string.IsNullOrEmpty(json)) return result;

            try
            {
                var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var dataElement) ||
                    !dataElement.TryGetProperty(symbol, out var symbolElement) ||
                    !symbolElement.TryGetProperty("quote", out var quoteElement) ||
                    !quoteElement.TryGetProperty("USD", out var usdElement))
                    return result;

                result["price"] = usdElement.GetProperty("price").GetDecimal();
                result["volume_24h"] = usdElement.GetProperty("volume_24h").GetDecimal();
                result["percent_change_1h"] = usdElement.GetProperty("percent_change_1h").GetDecimal();
                result["percent_change_24h"] = usdElement.GetProperty("percent_change_24h").GetDecimal();
                result["market_cap"] = usdElement.GetProperty("market_cap").GetDecimal();
                result["last_updated"] = usdElement.GetProperty("last_updated").GetString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error extracting price for {symbol}: {ex.Message}");
            }

            return result;
        }

        private List<string> ExtractTrendingCryptoSymbols(string json)
        {
            var symbols = new List<string>();

            if (string.IsNullOrEmpty(json)) return symbols;

            try
            {
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("symbol", out var symbolElement))
                        {
                            symbols.Add(symbolElement.GetString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error extracting trending symbols: {ex.Message}");
            }

            return symbols;
        }

        private Dictionary<string, decimal> ExtractMultipleCryptoPrices(string json, string[] symbols)
        {
            var prices = new Dictionary<string, decimal>();

            if (string.IsNullOrEmpty(json)) return prices;

            try
            {
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    foreach (var symbol in symbols)
                    {
                        if (dataElement.TryGetProperty(symbol, out var symbolElement) &&
                            symbolElement.TryGetProperty("quote", out var quoteElement) &&
                            quoteElement.TryGetProperty("USD", out var usdElement) &&
                            usdElement.TryGetProperty("price", out var priceElement))
                        {
                            prices[symbol] = priceElement.GetDecimal();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error extracting multiple crypto prices: {ex.Message}");
            }

            return prices;
        }
    }
}
