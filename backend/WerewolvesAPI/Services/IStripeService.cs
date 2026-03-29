namespace WerewolvesAPI.Services;

public interface IStripeService
{
    Task<string> CreateCheckoutSessionAsync(string tournamentCode, string successUrl, string cancelUrl);
    string? GetTournamentCodeFromEvent(string payload, string signature);
}
