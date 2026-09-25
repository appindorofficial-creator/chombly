using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Bookings.CancelBooking;
using WebAppPet.Application.Bookings.SaveClinicalNote;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Bookings.UpdateBookingStatus;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Bookings;

public class BookingStatusHandlersTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _client;
    private readonly GroomerProfile _business;

    public BookingStatusHandlersTests()
    {
        _db = _database.CreateContext();
        _client = TestData.AddUser(_db);
        _business = TestData.AddBusiness(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private Appointment AddAppointment(AppointmentStatus status, string? notes = null)
    {
        var appt = TestData.AddAppointment(_db, _client, _business, status, DateTime.UtcNow.AddDays(1));
        if (notes != null)
        {
            appt.Notes = notes;
            _db.SaveChanges();
        }
        return appt;
    }

    private Appointment Reload(int id)
    {
        using var db = _database.CreateContext();
        return db.Appointments.AsNoTracking().Single(a => a.Id == id);
    }

    private List<AppNotification> Notifications()
    {
        using var db = _database.CreateContext();
        return db.Notifications.AsNoTracking().OrderBy(n => n.Id).ToList();
    }

    private UpdateBookingStatusHandler UpdateHandler()
    {
        var db = _database.CreateContext();
        return new UpdateBookingStatusHandler(db, new ClinicalNotes(db));
    }

    private SaveClinicalNoteHandler SaveNoteHandler()
    {
        var db = _database.CreateContext();
        return new SaveClinicalNoteHandler(db, new ClinicalNotes(db));
    }

    [Theory]
    [InlineData(AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Confirmed)]
    public async Task Client_can_cancel_pending_or_confirmed(AppointmentStatus status)
    {
        var appt = AddAppointment(status);

        var result = await new CancelBookingHandler(_database.CreateContext())
            .HandleAsync(new CancelBookingCommand(_client.Id, appt.Id));

        Assert.True(result.Success);
        Assert.Equal(AppointmentStatus.Cancelled, Reload(appt.Id).Status);
        var notice = Assert.Single(Notifications());
        Assert.Equal(_client.Id, notice.UserId);
        Assert.Contains(_business.BusinessName, notice.Message);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public async Task Client_cannot_cancel_finished_appointments(AppointmentStatus status)
    {
        var appt = AddAppointment(status);

        var result = await new CancelBookingHandler(_database.CreateContext())
            .HandleAsync(new CancelBookingCommand(_client.Id, appt.Id));

        Assert.False(result.Success);
        Assert.Equal(status, Reload(appt.Id).Status);
        Assert.Empty(Notifications());
    }

    [Fact]
    public async Task Client_cannot_cancel_someone_elses_appointment()
    {
        var appt = AddAppointment(AppointmentStatus.Pending);
        var stranger = TestData.AddUser(_db);

        var result = await new CancelBookingHandler(_database.CreateContext())
            .HandleAsync(new CancelBookingCommand(stranger.Id, appt.Id));

        Assert.False(result.Success);
        Assert.Equal(AppointmentStatus.Pending, Reload(appt.Id).Status);
    }

    [Theory]
    [InlineData(BookingStatusAction.Accept, AppointmentStatus.Pending, AppointmentStatus.Confirmed, "¡Cita confirmada!", "Booking confirmed!")]
    [InlineData(BookingStatusAction.Reject, AppointmentStatus.Pending, AppointmentStatus.Cancelled, "Cita rechazada", "Booking declined")]
    [InlineData(BookingStatusAction.Complete, AppointmentStatus.Confirmed, AppointmentStatus.Completed, "Servicio completado", "Service completed")]
    public async Task Business_transitions_notify_the_client(
        BookingStatusAction action, AppointmentStatus from, AppointmentStatus to, string titleEs, string titleEn)
    {
        var appt = AddAppointment(from);

        var result = await UpdateHandler().HandleAsync(new UpdateBookingStatusCommand(_business.Id, appt.Id, action));

        Assert.True(result.Success);
        Assert.Equal(to, Reload(appt.Id).Status);
        var notice = Assert.Single(Notifications());
        Assert.Equal(_client.Id, notice.UserId);
        Assert.Equal(CatalogLocalizer.Loc(titleEs, titleEn), notice.Title);
        Assert.StartsWith(_business.BusinessName, notice.Message);
    }

    [Theory]
    [InlineData(BookingStatusAction.Accept, AppointmentStatus.Confirmed)]
    [InlineData(BookingStatusAction.Reject, AppointmentStatus.Completed)]
    [InlineData(BookingStatusAction.Complete, AppointmentStatus.Pending)]
    public async Task Transition_from_wrong_status_is_ignored(BookingStatusAction action, AppointmentStatus current)
    {
        var appt = AddAppointment(current);

        var result = await UpdateHandler().HandleAsync(new UpdateBookingStatusCommand(_business.Id, appt.Id, action));

        Assert.False(result.Success);
        Assert.Equal(current, Reload(appt.Id).Status);
        Assert.Empty(Notifications());
    }

    [Fact]
    public async Task Other_business_cannot_change_the_appointment()
    {
        var appt = AddAppointment(AppointmentStatus.Pending);
        var other = TestData.AddBusiness(_db);

        var result = await UpdateHandler().HandleAsync(
            new UpdateBookingStatusCommand(other.Id, appt.Id, BookingStatusAction.Accept));

        Assert.False(result.Success);
        Assert.Equal(AppointmentStatus.Pending, Reload(appt.Id).Status);
    }

    [Fact]
    public async Task Completing_with_clinical_note_stores_it_and_opens_follow_up()
    {
        var appt = AddAppointment(AppointmentStatus.Confirmed, "Mascotas: Thor, Luna · Nota clínica: vieja");
        var consult = new Consultation
        {
            ClientId = _client.Id,
            ProviderId = _business.Id,
            AppointmentId = appt.Id,
            Status = ConsultationStatus.Scheduled
        };
        _db.Consultations.Add(consult);
        _db.SaveChanges();

        await UpdateHandler().HandleAsync(new UpdateBookingStatusCommand(
            _business.Id, appt.Id, BookingStatusAction.Complete, "  Vacuna al día  "));

        Assert.Equal(
            "Mascotas: Thor, Luna · " + CatalogLocalizer.Loc("Nota clínica: Vacuna al día", "Clinical note: Vacuna al día"),
            Reload(appt.Id).Notes);
        using var db = _database.CreateContext();
        var saved = db.Consultations.AsNoTracking().Single();
        Assert.Equal("Vacuna al día", saved.ClinicalNotes);
        Assert.Equal(ConsultationStatus.FollowUpOpen, saved.Status);
        Assert.Equal(new[] { "vet-followup", "appointment" }, Notifications().Select(n => n.Type));
    }

    [Fact]
    public async Task Completing_without_note_closes_linked_consultation()
    {
        var appt = AddAppointment(AppointmentStatus.Confirmed);
        _db.Consultations.Add(new Consultation
        {
            ClientId = _client.Id,
            ProviderId = _business.Id,
            AppointmentId = appt.Id,
            Status = ConsultationStatus.InProgress
        });
        _db.SaveChanges();

        await UpdateHandler().HandleAsync(new UpdateBookingStatusCommand(_business.Id, appt.Id, BookingStatusAction.Complete));

        using var db = _database.CreateContext();
        Assert.Equal(ConsultationStatus.Completed, db.Consultations.AsNoTracking().Single().Status);
    }

    [Fact]
    public async Task Save_note_replaces_previous_clinical_note()
    {
        var appt = AddAppointment(AppointmentStatus.Completed, "Paseo 60 min · Seguimiento: revisar");

        var result = await SaveNoteHandler().HandleAsync(new SaveClinicalNoteCommand(_business.Id, appt.Id, "Todo bien"));

        Assert.True(result.Success);
        Assert.Equal(
            "Paseo 60 min · " + CatalogLocalizer.Loc("Nota clínica: Todo bien", "Clinical note: Todo bien"),
            Reload(appt.Id).Notes);
        Assert.Equal("vet-followup", Assert.Single(Notifications()).Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Save_note_requires_text(string? note)
    {
        var appt = AddAppointment(AppointmentStatus.Completed);

        var result = await SaveNoteHandler().HandleAsync(new SaveClinicalNoteCommand(_business.Id, appt.Id, note));

        Assert.False(result.Success);
        Assert.Empty(Notifications());
    }

    [Fact]
    public async Task Save_note_for_other_business_is_rejected()
    {
        var appt = AddAppointment(AppointmentStatus.Completed);
        var other = TestData.AddBusiness(_db);

        var result = await SaveNoteHandler().HandleAsync(new SaveClinicalNoteCommand(other.Id, appt.Id, "x"));

        Assert.False(result.Success);
        Assert.Null(Reload(appt.Id).Notes);
    }
}
