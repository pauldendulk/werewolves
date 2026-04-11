using Microsoft.AspNetCore.Mvc;
using Stripe;
using WerewolvesAPI.Services;

namespace WerewolvesAPI.Controllers;

[ApiController]
[Route("api/stripe")]
public class StripeController : ControllerBase
{
    private readonly IStripeService _stripeService;
    private readonly IGameService _gameService;
    private readonly ILogger<StripeController> _logger;

    public StripeController(IStripeService stripeService, IGameService gameService, ILogger<StripeController> logger)
    {
        _stripeService = stripeService;
        _gameService = gameService;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();

        string? tournamentCode;
        try
        {
            tournamentCode = _stripeService.GetTournamentCodeFromEvent(payload, signature);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature validation failed");
            return BadRequest();
        }

        if (tournamentCode == null)
            return Ok(); // unknown event type — ignore

        _gameService.UnlockTournamentMode(tournamentCode);
        _logger.LogInformation("Tournament {TournamentCode} unlocked via Stripe payment", tournamentCode);

        return Ok();
    }
}
