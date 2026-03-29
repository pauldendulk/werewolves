using Stripe;
using Stripe.Checkout;

namespace WerewolvesAPI.Services;

public class StripeService : IStripeService
{
    private readonly string _webhookSecret;
    private readonly string _priceId;

    public StripeService(IConfiguration configuration)
    {
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        _webhookSecret = configuration["Stripe:WebhookSecret"] ?? string.Empty;
        _priceId = configuration["Stripe:PriceId"] ?? string.Empty;
    }

    public async Task<string> CreateCheckoutSessionAsync(string tournamentCode, string successUrl, string cancelUrl)
    {
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = _priceId,
                    Quantity = 1,
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                ["tournamentCode"] = tournamentCode
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);
        return session.Url;
    }

    public string? GetTournamentCodeFromEvent(string payload, string signature)
    {
        var stripeEvent = EventUtility.ConstructEvent(payload, signature, _webhookSecret);

        if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted)
            return null;

        var session = (Session)stripeEvent.Data.Object;
        session.Metadata.TryGetValue("tournamentCode", out var tournamentCode);
        return tournamentCode;
    }
}
