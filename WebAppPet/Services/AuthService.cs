using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class AuthService
{
    private readonly IHttpContextAccessor _http;

    public AuthService(IHttpContextAccessor http) => _http = http;

    public int? CurrentUserId
    {
        get
        {
            var id = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(id, out var userId) ? userId : null;
        }
    }

    public bool IsAuthenticated => _http.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool IsGroomer =>
        _http.HttpContext?.User.FindFirstValue(ClaimTypes.Role) == nameof(Models.UserRole.Groomer);

    public bool IsAdmin =>
        _http.HttpContext?.User.FindFirstValue(ClaimTypes.Role) == nameof(Models.UserRole.Admin);

    public async Task SignInAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await _http.HttpContext!.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            });
    }

    public async Task SignOutAsync() =>
        await _http.HttpContext!.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}
