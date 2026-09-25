using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Common;
using WebAppPet.Data;
using WebAppPet.Localization;

namespace WebAppPet.Application.Bookings.SaveClinicalNote;

public class SaveClinicalNoteHandler
{
    private readonly AppDbContext _db;
    private readonly ClinicalNotes _clinicalNotes;

    public SaveClinicalNoteHandler(AppDbContext db, ClinicalNotes clinicalNotes)
    {
        _db = db;
        _clinicalNotes = clinicalNotes;
    }

    public async Task<Result> HandleAsync(SaveClinicalNoteCommand command, CancellationToken ct = default)
    {
        var note = command.Note?.Trim();
        if (string.IsNullOrWhiteSpace(note))
            return Result.Fail(CatalogLocalizer.Loc("Escribe la nota antes de guardar.", "Write the note before saving."));

        var appt = await _db.Appointments
            .Include(a => a.Groomer)
            .FirstOrDefaultAsync(a => a.Id == command.AppointmentId && a.GroomerId == command.BusinessId, ct);
        if (appt is null)
            return Result.Fail(CatalogLocalizer.Loc("No encontramos la cita.", "Appointment not found."));

        await _clinicalNotes.ApplyAsync(appt, appt.Groomer.BusinessName, note, ct);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
