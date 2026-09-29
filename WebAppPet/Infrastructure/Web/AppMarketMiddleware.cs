using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Infrastructure.Web;

/// <summary>
/// Makes the signed-in user's persisted <c>CountryCode</c> the market of the rest of the request,
/// so <see cref="AppTimeZones"/> and everything after it share one home-market flag.
/// </summary>
public sealed class AppMarketMiddleware
{
    private readonly RequestDelegate _next;

    public AppMarketMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AuthService auth, AppDbContext db)
    {
        var country = MarketCountry.DefaultIso;
        var market = BusinessMarket.Colombia;

        if (auth.CurrentUserId is int userId)
        {
            var row = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.CountryCode, u.City, u.Latitude, u.Longitude })
                .FirstOrDefaultAsync(context.RequestAborted);

            if (row is not null)
            {
                country = MarketCountry.ResolveForUser(row.CountryCode, row.City, row.Latitude, row.Longitude);
                market = MarketCountry.ToMarket(country);
            }
        }

        using (AppTimeZones.UseRequest(market, country))
            await _next(context);
    }
}
