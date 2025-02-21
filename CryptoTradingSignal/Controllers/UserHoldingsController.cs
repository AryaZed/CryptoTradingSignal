using CryptoTradingSignal.Service;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoTradingSignal.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserHoldingsController : ControllerBase
    {
        private readonly CoinMarketCapService _coinService;

        public UserHoldingsController(CoinMarketCapService coinService)
        {
            _coinService = coinService;
        }

        [HttpGet("{userId}/holdings")]
        public async Task<IActionResult> GetUserHoldings(string userId)
        {
            var holdings = await _coinService.GetUserCryptoHoldings(userId);
            return Ok(holdings);
        }

        [HttpPost("{userId}/holdings")]
        public async Task<IActionResult> AddOrUpdateHolding(string userId, [FromBody] HoldingRequest request)
        {
            await _coinService.AddOrUpdateHolding(userId, request.Symbol, request.Amount);
            return Ok(new { message = "Holding updated successfully" });
        }

        [HttpDelete("{userId}/holdings/{symbol}")]
        public async Task<IActionResult> RemoveHolding(string userId, string symbol)
        {
            await _coinService.RemoveHolding(userId, symbol);
            return Ok(new { message = "Holding removed successfully" });
        }
    }

    public class HoldingRequest
    {
        public string Symbol { get; set; }
        public float Amount { get; set; }
    }
}
