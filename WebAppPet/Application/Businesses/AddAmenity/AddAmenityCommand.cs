namespace WebAppPet.Application.Businesses.AddAmenity;

public sealed record AddAmenityCommand(int BusinessId, string? Label, string? Icon);
