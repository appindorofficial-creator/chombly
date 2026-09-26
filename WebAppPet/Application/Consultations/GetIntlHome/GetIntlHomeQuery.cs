namespace WebAppPet.Application.Consultations.GetIntlHome;

/// <param name="UserId">Null for visitors who are not signed in.</param>
public sealed record GetIntlHomeQuery(int? UserId, int? ConsultationId, int? PetId);

/// <param name="PreferredLanguage">"es" or "en".</param>
public sealed record IntlHome(string PreferredLanguage, string? PetName, string? PetBreed);
