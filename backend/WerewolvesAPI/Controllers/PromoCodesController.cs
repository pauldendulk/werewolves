using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using WerewolvesAPI.Repositories;

namespace WerewolvesAPI.Controllers;

[ApiController]
[Route("promo-codes")]
public class PromoCodesController : ControllerBase
{
    private readonly IPromoCodeRepository _promoCodeRepository;
    private readonly IConfiguration _configuration;

    public PromoCodesController(IPromoCodeRepository promoCodeRepository, IConfiguration configuration)
    {
        _promoCodeRepository = promoCodeRepository;
        _configuration = configuration;
    }

    private const string GeneratedCodeCookie = "promo_generated";

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized(out var unauthorized))
            return unauthorized;

        Request.Cookies.TryGetValue(GeneratedCodeCookie, out var generated);
        Response.Cookies.Delete(GeneratedCodeCookie);

        var recent = await _promoCodeRepository.GetRecentAsync(10);
        return Content(BuildHtml(recent, generated), "text/html");
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate()
    {
        if (!IsAuthorized(out var unauthorized))
            return unauthorized;

        var code = await _promoCodeRepository.GenerateAsync();
        Response.Cookies.Append(GeneratedCodeCookie, code, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromMinutes(5)
        });
        return Redirect("/promo-codes");
    }

    private bool IsAuthorized(out IActionResult result)
    {
        result = null!;

        var expectedUsername = _configuration["Admin:Username"] ?? string.Empty;
        var expectedPassword = _configuration["Admin:Password"] ?? string.Empty;

        if (!Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            !AuthenticationHeaderValue.TryParse(authHeader, out var parsed) ||
            parsed.Scheme != "Basic" ||
            parsed.Parameter == null)
        {
            result = BasicAuthChallenge();
            return false;
        }

        string credentials;
        try
        {
            credentials = Encoding.UTF8.GetString(Convert.FromBase64String(parsed.Parameter));
        }
        catch (FormatException)
        {
            result = BasicAuthChallenge();
            return false;
        }

        var colon = credentials.IndexOf(':');
        if (colon < 0)
        {
            result = BasicAuthChallenge();
            return false;
        }

        var username = credentials[..colon];
        var password = credentials[(colon + 1)..];

        if (username != expectedUsername || password != expectedPassword)
        {
            result = BasicAuthChallenge();
            return false;
        }

        return true;
    }

    private IActionResult BasicAuthChallenge()
    {
        Response.Headers["WWW-Authenticate"] = "Basic realm=\"Werewolves Admin\", charset=\"UTF-8\"";
        return StatusCode(401);
    }

    private static string BuildHtml(IEnumerable<PromoCodeRecord> recent, string? generated)
    {
        var rows = string.Join("\n", recent.Select(c =>
        {
            var status = c.RedeemedAt.HasValue
                ? $"<span style='color:#16a34a'>&#x2705; Redeemed {c.RedeemedAt.Value:dd MMM yyyy HH:mm} UTC</span>"
                : "<span style='color:#ca8a04'>&#x23F3; Unused</span>";
            return $"<tr><td style='font-family:monospace;font-size:1.1rem;padding:0.5rem 1rem'>{c.Code}</td>" +
                   $"<td style='padding:0.5rem 1rem;color:#6b7280;font-size:0.875rem'>{c.CreatedAt:dd MMM yyyy HH:mm} UTC</td>" +
                   $"<td style='padding:0.5rem 1rem'>{status}</td></tr>";
        }));

        var generatedSection = generated == null ? "" : $$"""
            <div style='background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:1.5rem;margin-bottom:2rem'>
              <div style='font-size:0.875rem;color:#16a34a;margin-bottom:0.75rem'>&#x2705; New code generated &mdash; share it now</div>
              <div style='display:flex;align-items:center;gap:1rem;flex-wrap:wrap'>
                <span id='newCode' style='font-family:monospace;font-size:1.5rem;font-weight:bold;letter-spacing:0.05em'>{{generated}}</span>
                <button onclick='copyCode()' style='padding:0.4rem 1.2rem;background:#16a34a;color:white;border:none;border-radius:6px;cursor:pointer;font-size:0.875rem'>Copy</button>
              </div>
            </div>
            <script>
              function copyCode() {
                navigator.clipboard.writeText('{{generated}}');
              }
            </script>
            """;

        var tableBody = rows.Length > 0
            ? rows
            : "<tr><td colspan='3' style='padding:1.5rem;text-align:center;color:#9ca3af'>No codes yet</td></tr>";

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Promo Codes &mdash; Werewolves</title>
              <style>
                body { font-family: system-ui, -apple-system, sans-serif; max-width: 720px; margin: 2rem auto; padding: 0 1rem; background: #f9fafb; color: #111827; }
                h1 { font-size: 1.5rem; margin-bottom: 1.5rem; }
                table { width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
                th { text-align: left; padding: 0.75rem 1rem; background: #f3f4f6; font-size: 0.8rem; color: #6b7280; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; }
                tr:not(:last-child) td { border-bottom: 1px solid #e5e7eb; }
              </style>
            </head>
            <body>
              <h1>&#x1F43A; Werewolves &mdash; Promo Codes</h1>
              {{generatedSection}}
              <form method="post" action="/promo-codes/generate" style="margin-bottom:2rem">
                <button type="submit" style="padding:0.6rem 1.5rem;background:#2563eb;color:white;border:none;border-radius:6px;cursor:pointer;font-size:1rem">Generate new code</button>
              </form>
              <table>
                <thead>
                  <tr>
                    <th>Code</th>
                    <th>Created</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {{tableBody}}
                </tbody>
              </table>
            </body>
            </html>
            """;
    }
}
