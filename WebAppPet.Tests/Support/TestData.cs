using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Tests.Support;

public static class TestData
{
    private static int _seq;

    public static AppUser AddUser(AppDbContext db, UserRole role = UserRole.Client)
    {
        var n = Interlocked.Increment(ref _seq);
        var user = new AppUser
        {
            FullName = $"User {n}",
            Email = $"user{n}@test.local",
            PasswordHash = "x",
            Role = role
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static GroomerProfile AddBusiness(AppDbContext db)
    {
        var owner = AddUser(db, UserRole.Groomer);
        var business = new GroomerProfile
        {
            UserId = owner.Id,
            BusinessName = $"Business {owner.Id}"
        };
        db.Groomers.Add(business);
        db.SaveChanges();
        return business;
    }

    public static Appointment AddAppointment(
        AppDbContext db,
        AppUser client,
        GroomerProfile business,
        AppointmentStatus status,
        DateTime scheduledAtUtc)
    {
        var pet = new Pet { OwnerId = client.Id, Name = "Thor" };
        var service = new GroomerService { GroomerId = business.Id, Name = "Baño", PriceMedium = 40 };
        db.Pets.Add(pet);
        db.Services.Add(service);
        db.SaveChanges();

        var appt = new Appointment
        {
            ClientId = client.Id,
            GroomerId = business.Id,
            PetId = pet.Id,
            ServiceId = service.Id,
            Status = status,
            ScheduledAt = scheduledAtUtc,
            TotalPrice = 40
        };
        db.Appointments.Add(appt);
        db.SaveChanges();
        return appt;
    }

    public static Review AddReview(AppDbContext db, AppUser client, GroomerProfile business, int rating, DateTime? createdAt = null)
    {
        var review = new Review
        {
            ClientId = client.Id,
            GroomerId = business.Id,
            Rating = rating,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.Reviews.Add(review);
        db.SaveChanges();
        return review;
    }
}
