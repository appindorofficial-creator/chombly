using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Pets.DeletePet;
using WebAppPet.Application.Pets.GetPet;
using WebAppPet.Application.Pets.GetPetHistory;
using WebAppPet.Application.Pets.GetPets;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Pets;

public class PetHandlersTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _owner;
    private readonly GroomerProfile _business;

    public PetHandlersTests()
    {
        _db = _database.CreateContext();
        _owner = TestData.AddUser(_db);
        _business = TestData.AddBusiness(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private Appointment AddAppointment(Pet pet, AppointmentStatus status, DateTime scheduledAt)
    {
        var service = TestData.AddService(_db, _business);
        var appt = new Appointment
        {
            ClientId = pet.OwnerId,
            GroomerId = _business.Id,
            PetId = pet.Id,
            ServiceId = service.Id,
            Status = status,
            ScheduledAt = scheduledAt
        };
        _db.Appointments.Add(appt);
        _db.SaveChanges();
        return appt;
    }

    private Consultation AddConsultation(Pet pet, int clientId)
    {
        var consult = new Consultation { ClientId = clientId, ProviderId = _business.Id, PetId = pet.Id, UpdatedAt = DateTime.UtcNow };
        _db.Consultations.Add(consult);
        _db.SaveChanges();
        return consult;
    }

    [Fact]
    public async Task Pets_are_listed_by_name_and_lost_uploads_fall_back_to_the_species_photo()
    {
        var luna = TestData.AddPet(_db, _owner, PetSpecies.Cat);
        luna.Name = "Luna";
        luna.PhotoUrl = $"/uploads/pets/lost-{Guid.NewGuid():N}.jpg";
        var bruno = TestData.AddPet(_db, _owner);
        bruno.Name = "Bruno";
        bruno.PhotoUrl = "https://example.com/bruno.jpg";
        TestData.AddPet(_db, TestData.AddUser(_db));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var pets = await new GetPetsHandler(_db, new FakeWebHostEnvironment()).HandleAsync(new GetPetsQuery(_owner.Id));

        Assert.Equal(["Bruno", "Luna"], pets.Select(p => p.Name));
        var stored = await _db.Pets.AsNoTracking().Where(p => p.OwnerId == _owner.Id).ToDictionaryAsync(p => p.Name, p => p.PhotoUrl);
        Assert.Equal(PetSpecies.DefaultPhoto(PetSpecies.Cat), stored["Luna"]);
        Assert.Equal("https://example.com/bruno.jpg", stored["Bruno"]);
    }

    [Fact]
    public async Task Only_the_owner_can_load_a_pet()
    {
        var pet = TestData.AddPet(_db, _owner);
        var handler = new GetPetHandler(_db);

        Assert.NotNull(await handler.HandleAsync(new GetPetQuery(_owner.Id, pet.Id)));
        Assert.Null(await handler.HandleAsync(new GetPetQuery(TestData.AddUser(_db).Id, pet.Id)));
    }

    [Fact]
    public async Task Deleting_a_pet_keeps_consultations_and_removes_its_reminders()
    {
        var pet = TestData.AddPet(_db, _owner);
        var consult = AddConsultation(pet, _owner.Id);
        _db.BehaviorCases.Add(new BehaviorCase { ClientId = _owner.Id, PetId = pet.Id });
        _db.ReminderSchedules.Add(new ReminderSchedule { UserId = _owner.Id, PetId = pet.Id, Title = "Vacuna" });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var result = await new DeletePetHandler(_db).HandleAsync(new DeletePetCommand(_owner.Id, pet.Id));

        Assert.Equal(DeletePetResult.Deleted, result);
        Assert.False(await _db.Pets.AnyAsync(p => p.Id == pet.Id));
        Assert.Null((await _db.Consultations.AsNoTracking().SingleAsync(c => c.Id == consult.Id)).PetId);
        Assert.Null((await _db.BehaviorCases.AsNoTracking().SingleAsync()).PetId);
        Assert.False(await _db.ReminderSchedules.AnyAsync());
    }

    [Fact]
    public async Task A_pet_with_appointments_or_another_owner_is_not_deleted()
    {
        var booked = TestData.AddPet(_db, _owner);
        AddAppointment(booked, AppointmentStatus.Cancelled, DateTime.UtcNow);
        var free = TestData.AddPet(_db, _owner);
        var handler = new DeletePetHandler(_db);

        Assert.Equal(DeletePetResult.HasAppointments, await handler.HandleAsync(new DeletePetCommand(_owner.Id, booked.Id)));
        Assert.Equal(DeletePetResult.NotFound, await handler.HandleAsync(new DeletePetCommand(TestData.AddUser(_db).Id, free.Id)));
        Assert.Equal(2, await _db.Pets.CountAsync(p => p.OwnerId == _owner.Id));
    }

    [Fact]
    public async Task History_has_completed_appointments_newest_first_and_the_owners_consultations()
    {
        var pet = TestData.AddPet(_db, _owner);
        var older = AddAppointment(pet, AppointmentStatus.Completed, new DateTime(2026, 8, 1, 15, 0, 0, DateTimeKind.Utc));
        var newer = AddAppointment(pet, AppointmentStatus.Completed, new DateTime(2026, 9, 1, 15, 0, 0, DateTimeKind.Utc));
        AddAppointment(pet, AppointmentStatus.Confirmed, new DateTime(2026, 9, 10, 15, 0, 0, DateTimeKind.Utc));
        var mine = AddConsultation(pet, _owner.Id);
        AddConsultation(pet, TestData.AddUser(_db).Id);
        var handler = new GetPetHistoryHandler(_db);

        var history = await handler.HandleAsync(new GetPetHistoryQuery(_owner.Id, pet.Id));

        Assert.NotNull(history);
        Assert.Equal([newer.Id, older.Id], history.Appointments.Select(a => a.Id));
        Assert.All(history.Appointments, a => Assert.NotNull(a.Groomer));
        Assert.Equal(mine.Id, Assert.Single(history.Consultations).Id);
        Assert.Null(await handler.HandleAsync(new GetPetHistoryQuery(TestData.AddUser(_db).Id, pet.Id)));
    }
}
