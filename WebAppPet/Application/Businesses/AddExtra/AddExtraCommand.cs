namespace WebAppPet.Application.Businesses.AddExtra;

public sealed record AddExtraCommand(int BusinessId, string? Name, decimal Price);
