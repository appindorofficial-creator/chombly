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

    public static Pet AddPet(AppDbContext db, AppUser owner, string species = PetSpecies.Dog, PetSize size = PetSize.Medium)
    {
        var pet = new Pet { OwnerId = owner.Id, Name = $"Pet {Interlocked.Increment(ref _seq)}", Species = species, Size = size };
        db.Pets.Add(pet);
        db.SaveChanges();
        return pet;
    }

    public static GroomerService AddService(AppDbContext db, GroomerProfile business, decimal price = 40)
    {
        var service = new GroomerService
        {
            GroomerId = business.Id,
            Name = "Baño",
            PriceSmall = price,
            PriceMedium = price,
            PriceLarge = price,
            PriceGiant = price
        };
        db.Services.Add(service);
        db.SaveChanges();
        return service;
    }

    public static ServiceExtra AddExtra(AppDbContext db, GroomerProfile business, string name, decimal price)
    {
        var extra = new ServiceExtra { GroomerId = business.Id, Name = name, Price = price };
        db.ServiceExtras.Add(extra);
        db.SaveChanges();
        return extra;
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
