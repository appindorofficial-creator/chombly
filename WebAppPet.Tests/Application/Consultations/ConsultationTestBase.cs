using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Consultations;

public abstract class ConsultationTestBase : IDisposable
{
    protected readonly TestDatabase Database = new();
    protected readonly AppDbContext Db;

    protected ConsultationTestBase()
    {
        Db = Database.CreateContext();
        Db.ServiceCatalog.AddRange(
            new ServiceCatalogItem { Code = ServiceCatalogCodes.VetIntl30, NameEs = "Orientación 30 min", ScopeEs = "Orientación", Price = 30, DurationMinutes = 30 },
            new ServiceCatalogItem { Code = ServiceCatalogCodes.VetLocal30, NameEs = "Consulta local 30 min", ScopeEs = "Local", Price = 70, DurationMinutes = 30 },
            new ServiceCatalogItem { Code = ServiceCatalogCodes.BehaviorSession, NameEs = "Conducta", Price = 50, IsBookable = false });
        Db.SaveChanges();
    }

    public void Dispose()
    {
        Db.Dispose();
        Database.Dispose();
    }

    protected VetAuditService Audit => new(Db);
    protected ClientHomeCountry HomeCountry => new(Db);
    protected ServiceCatalogService Catalog => new(Db);
    protected ChomblyCareService Care => new(Db);
    protected ConsultationRouter Router => new(Db, new VcprService(Db));

    protected AppUser AddClient(string? country = "CO")
    {
        var user = TestData.AddUser(Db);
        user.CountryCode = country;
        Db.SaveChanges();
        return user;
    }

    protected Pet AddPet(AppUser owner, string breed = "Beagle")
    {
        var pet = TestData.AddPet(Db, owner);
        pet.Breed = breed;
        Db.SaveChanges();
        return pet;
    }

    protected Consultation AddConsultation(AppUser client, Pet? pet = null, ConsultationStatus status = ConsultationStatus.Draft,
        string catalogCode = ServiceCatalogCodes.VetIntl30, string country = "CO", string usState = "Other")
    {
        var consultation = new Consultation
        {
            ClientId = client.Id,
            PetId = pet?.Id,
            Status = status,
            Modality = VetModality.Virtual,
            ServiceCatalogCode = catalogCode,
            ContextCountry = country,
            PetUsState = usState
        };
        Db.Consultations.Add(consultation);
        Db.SaveChanges();
        return consultation;
    }

    protected void AddVcpr(Pet pet, string state = "NC")
    {
        Db.VcprRecords.Add(new VcprRecord
        {
            PetId = pet.Id,
            ProviderId = TestData.AddBusiness(Db).Id,
            UsState = state,
            ExamDate = DateTime.UtcNow.AddMonths(-1)
        });
        Db.SaveChanges();
    }

    protected void AddCare(AppUser client)
    {
        Db.CareSubscriptions.Add(new CareSubscription { UserId = client.Id });
        Db.SaveChanges();
    }

    protected List<string> AuditActions() =>
        Db.AuditLogs.OrderBy(a => a.Id).Select(a => a.Action).ToList();

    protected Consultation Reload(int id)
    {
        using var db = Database.CreateContext();
        return db.Consultations.Single(c => c.Id == id);
    }
}
