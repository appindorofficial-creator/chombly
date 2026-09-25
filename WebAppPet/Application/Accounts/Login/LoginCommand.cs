using WebAppPet.Models;

namespace WebAppPet.Application.Accounts.Login;

public sealed record LoginCommand(string Email, string Password, string Culture);

public sealed record LoginResult(AppUser? User)
{
    public bool Success => User is not null;
}
