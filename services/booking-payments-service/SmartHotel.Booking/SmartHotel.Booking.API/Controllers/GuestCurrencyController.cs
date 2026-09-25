using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartHotel.Booking.API.Controllers;

public class CurrencyPreferenceDto
{
    public string Currency { get; set; } = "USD";
    public WalletBalanceDto Balances { get; set; } = new();
}

public class WalletBalanceDto
{
    public decimal USD { get; set; } = 150.00m;
    public decimal LKR { get; set; } = 45000.00m;
}

public class UpdateCurrencyRequest
{
    public string Currency { get; set; } = "USD";
}

[ApiController]
[Route("api/guests")]
[Produces("application/json")]
public class GuestCurrencyController : ControllerBase
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _currencyStore = new();

    [HttpGet("{id}/currency-preference")]
    [AllowAnonymous]
    public IActionResult GetCurrencyPreference([FromRoute] string id)
    {
        var preferred = _currencyStore.TryGetValue(id, out var curr) ? curr : "USD";
        return Ok(new CurrencyPreferenceDto
        {
            Currency = preferred,
            Balances = new WalletBalanceDto
            {
                USD = 150.00m,
                LKR = 45000.00m
            }
        });
    }

    [HttpPatch("{id}/currency-preference")]
    [AllowAnonymous]
    public IActionResult UpdateCurrencyPreference([FromRoute] string id, [FromBody] UpdateCurrencyRequest request)
    {
        var normalized = request.Currency?.ToUpperInvariant() == "LKR" ? "LKR" : "USD";
        _currencyStore[id] = normalized;

        return Ok(new CurrencyPreferenceDto
        {
            Currency = normalized,
            Balances = new WalletBalanceDto
            {
                USD = 150.00m,
                LKR = 45000.00m
            }
        });
    }
}
