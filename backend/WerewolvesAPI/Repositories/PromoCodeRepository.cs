using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Npgsql;

namespace WerewolvesAPI.Repositories;

public class PromoCodeRepository(string connectionString) : IPromoCodeRepository
{
    // Excludes ambiguous characters: 0/O, 1/I/L
    private static readonly char[] _alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789".ToCharArray();

    private bool HasDatabase => !string.IsNullOrEmpty(connectionString);
    private NpgsqlConnection Open() => new(connectionString);

    // In-memory store used when no database is configured (e.g. CI).
    private readonly ConcurrentDictionary<string, PromoCodeRecord> _inMemoryCodes = new();

    public async Task<string> GenerateAsync()
    {
        var code = GenerateCode();
        if (!HasDatabase)
        {
            _inMemoryCodes[code] = new PromoCodeRecord(code, DateTime.UtcNow, null);
            return code;
        }
        using var conn = Open();
        await conn.ExecuteAsync(
            "INSERT INTO promo_codes (code) VALUES (@code)",
            new { code });
        return code;
    }

    public async Task<bool> RedeemAsync(string code)
    {
        if (!HasDatabase)
        {
            return _inMemoryCodes.TryGetValue(code, out var record)
                && record.RedeemedAt == null
                && _inMemoryCodes.TryUpdate(code, record with { RedeemedAt = DateTime.UtcNow }, record);
        }
        using var conn = Open();
        var affected = await conn.ExecuteAsync("""
            UPDATE promo_codes
            SET redeemed_at = NOW()
            WHERE code = @code AND redeemed_at IS NULL
            """, new { code });
        return affected == 1;
    }

    public async Task<IEnumerable<PromoCodeRecord>> GetRecentAsync(int count)
    {
        if (!HasDatabase)
            return _inMemoryCodes.Values.OrderByDescending(c => c.CreatedAt).Take(count);
        using var conn = Open();
        return await conn.QueryAsync<PromoCodeRecord>(
            "SELECT code, created_at AS \"CreatedAt\", redeemed_at AS \"RedeemedAt\" FROM promo_codes ORDER BY created_at DESC LIMIT @count",
            new { count });
    }

    private static string GenerateCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var sb = new StringBuilder("WOLF-");
        for (int i = 0; i < 4; i++)
            sb.Append(_alphabet[bytes[i] % _alphabet.Length]);
        sb.Append('-');
        for (int i = 4; i < 8; i++)
            sb.Append(_alphabet[bytes[i] % _alphabet.Length]);
        return sb.ToString();
    }
}
