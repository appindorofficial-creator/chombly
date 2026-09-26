using WebAppPet.Application.Consultations.GetLocalVets;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.SelectLocalVet;

/// <summary>Books the chosen local vet and time (today or tomorrow in the consultation's market).</summary>
public class SelectLocalVetHandler
{
    private readonly AppDbContext _db;
    private readonly ClientHomeCountry _homeCountry;
    private readonly ServiceCatalogService _catalog;

    public SelectLocalVetHandler(AppDbContext db, ClientHomeCountry homeCountry, ServiceCatalogService catalog)
    {
        _db = db;
        _homeCountry = homeCountry;
        _catalog = catalog;
    }

    public async Task<SelectLocalVetResult> HandleAsync(SelectLocalVetCommand command, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(command.ClientId, command.ConsultationId, ct);
        if (consultation is null)
            return new SelectLocalVetResult(ConsultationStep.VetHome, null);

        if (!MarketCountry.AllowsUsLocalTeleconsult(await _homeCountry.ResolveAsync(command.ClientId, ct)))
            return new SelectLocalVetResult(ConsultationStep.IntlMatches, null);

        if (!consultation.HasActiveVcpr)
            return new SelectLocalVetResult(ConsultationStep.Eligibility, null);

        var options = await GetLocalVetsHandler.LoadAsync(_db, _catalog, consultation, ct);
        if (command.ProviderId <= 0 || !options.Providers.Any(p => p.Id == command.ProviderId))
            return new SelectLocalVetResult(null, options);

        if (!AppTimeZones.TryParseSlotToTimeSpan(command.Slot, out var timeOfDay))
            return new SelectLocalVetResult(null, options);

        var market = AppTimeZones.MarketFromCountry(consultation.ContextCountry);
        using var _ = AppTimeZones.UseMarket(market);

        var day = AppTimeZones.TodayLocalDate(market);
        if (string.Equals(command.When, "mañana", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command.When, "tomorrow", StringComparison.OrdinalIgnoreCase))
            day = day.AddDays(1);

        consultation.ProviderId = command.ProviderId;
        consultation.ServiceCatalogCode = ServiceCatalogCodes.VetLocal30;
        consultation.ScheduledAt = AppTimeZones.LocalDateAndTimeToUtc(day, timeOfDay, market);
        consultation.Status = ConsultationStatus.ProviderSelected;
        await _db.TouchAsync(consultation, ct);

        return new SelectLocalVetResult(ConsultationStep.Checkout, null);
    }
}
