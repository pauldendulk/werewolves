namespace WerewolvesAPI.Repositories;

public record PromoCodeRecord(
    string Code,
    DateTime CreatedAt,
    DateTime? RedeemedAt);

public interface IPromoCodeRepository
{
    Task<string> GenerateAsync();
    Task<bool> RedeemAsync(string code);
    Task<IEnumerable<PromoCodeRecord>> GetRecentAsync(int count);
}
