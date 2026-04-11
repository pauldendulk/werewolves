using Stripe;
using Stripe.Checkout;

namespace WerewolvesAPI.Services;

public class StripeService : IStripeService
{
    private readonly string _webhookSecret;
    private readonly string _priceId;
    private readonly bool _skipSignatureVerification;

    public StripeService(IConfiguration configuration, IHostEnvironment environment)
    {
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        _webhookSecret = configuration["Stripe:WebhookSecret"] ?? string.Empty;
        _priceId = configuration["Stripe:PriceId"] ?? string.Empty;
        _skipSignatureVerification = environment.IsDevelopment() && string.IsNullOrEmpty(_webhookSecret);
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
        if (_skipSignatureVerification)
            return GetTournamentCodeFromUnsignedPayload(payload);

        var stripeEvent = EventUtility.ConstructEvent(payload, signature, _webhookSecret);

        if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted)
            return null;

        var session = (Session)stripeEvent.Data.Object;
        session.Metadata.TryGetValue("tournamentCode", out var tournamentCode);
        return tournamentCode;
    }

    private static string? GetTournamentCodeFromUnsignedPayload(string payload)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp) ||
            typeProp.GetString() != "checkout.session.completed")
            return null;

        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty("object", out var obj) &&
            obj.TryGetProperty("metadata", out var metadata) &&
            metadata.TryGetProperty("tournamentCode", out var code))
            return code.GetString();

        return null;
    }
}
