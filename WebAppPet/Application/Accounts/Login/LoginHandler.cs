using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Infrastructure.Security;

namespace WebAppPet.Application.Accounts.Login;

/// <summary>Checks the credentials and stores the chosen language. The page issues the auth cookie.</summary>
public class LoginHandler
{
    private readonly AppDbContext _db;

    public LoginHandler(AppDbContext db) => _db = db;

    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == command.Email, ct);
        if (user is null || !PasswordHasher.Verify(command.Password, user.PasswordHash))
            return new LoginResult(null);

        user.PreferredLanguage = command.Culture;
        await _db.SaveChangesAsync(ct);
        return new LoginResult(user);
    }
}
