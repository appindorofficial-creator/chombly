using Microsoft.EntityFrameworkCore;
using WebAppPet.Models;

namespace WebAppPet.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<GroomerProfile> Groomers => Set<GroomerProfile>();
    public DbSet<GroomerService> Services => Set<GroomerService>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<GroomerPhoto> GroomerPhotos => Set<GroomerPhoto>();
    public DbSet<ServiceCategory> Categories => Set<ServiceCategory>();
    public DbSet<BusinessAmenity> Amenities => Set<BusinessAmenity>();
    public DbSet<ServiceExtra> ServiceExtras => Set<ServiceExtra>();
    public DbSet<AppointmentExtra> AppointmentExtras => Set<AppointmentExtra>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<BusinessDayAvailability> DayAvailabilities => Set<BusinessDayAvailability>();
    public DbSet<BusinessWeeklyHour> WeeklyHours => Set<BusinessWeeklyHour>();
    public DbSet<ServiceCatalogItem> ServiceCatalog => Set<ServiceCatalogItem>();
    public DbSet<ProviderLicense> ProviderLicenses => Set<ProviderLicense>();
    public DbSet<VcprRecord> VcprRecords => Set<VcprRecord>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<CareSubscription> CareSubscriptions => Set<CareSubscription>();
    public DbSet<CareBenefitUse> CareBenefitUses => Set<CareBenefitUse>();
    public DbSet<BehaviorCase> BehaviorCases => Set<BehaviorCase>();
    public DbSet<CountryCatalogEntry> CountryCatalog => Set<CountryCatalogEntry>();
    public DbSet<CountryWaitlistEntry> CountryWaitlist => Set<CountryWaitlistEntry>();
    public DbSet<ProviderBreedExpertise> ProviderBreedExpertises => Set<ProviderBreedExpertise>();
    public DbSet<ProviderCompensationRule> ProviderCompensationRules => Set<ProviderCompensationRule>();
    public DbSet<ProviderPayout> ProviderPayouts => Set<ProviderPayout>();
    public DbSet<ProfessionalOnboardingApplication> ProfessionalOnboardingApplications => Set<ProfessionalOnboardingApplication>();
    public DbSet<ReminderSchedule> ReminderSchedules => Set<ReminderSchedule>();
    public DbSet<ReminderDelivery> ReminderDeliveries => Set<ReminderDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<ServiceCategory>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        modelBuilder.Entity<Pet>()
            .HasOne(p => p.Owner)
            .WithMany(u => u.Pets)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroomerProfile>()
            .HasOne(g => g.User)
            .WithOne(u => u.GroomerProfile)
            .HasForeignKey<GroomerProfile>(g => g.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroomerProfile>()
            .HasOne(g => g.Category)
            .WithMany(c => c.Businesses)
            .HasForeignKey(g => g.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<GroomerService>()
            .HasOne(s => s.Groomer)
            .WithMany(g => g.Services)
            .HasForeignKey(s => s.GroomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BusinessAmenity>()
            .HasOne(a => a.Groomer)
            .WithMany(g => g.Amenities)
            .HasForeignKey(a => a.GroomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServiceExtra>()
            .HasOne(e => e.Groomer)
            .WithMany(g => g.Extras)
            .HasForeignKey(e => e.GroomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Client)
            .WithMany(u => u.Appointments)
            .HasForeignKey(a => a.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Pet)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Groomer)
            .WithMany(g => g.Appointments)
            .HasForeignKey(a => a.GroomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Service)
            .WithMany()
            .HasForeignKey(a => a.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AppointmentExtra>()
            .HasOne(x => x.Appointment)
            .WithMany(a => a.Extras)
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AppointmentExtra>()
            .HasOne(x => x.ServiceExtra)
            .WithMany()
            .HasForeignKey(x => x.ServiceExtraId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Groomer)
            .WithMany(g => g.Reviews)
            .HasForeignKey(r => r.GroomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Client)
            .WithMany()
            .HasForeignKey(r => r.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Favorite>()
            .HasIndex(f => new { f.UserId, f.GroomerId })
            .IsUnique();

        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.User)
            .WithMany(u => u.Favorites)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.Groomer)
            .WithMany(g => g.FavoritedBy)
            .HasForeignKey(f => f.GroomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AppNotification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PaymentMethod>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroomerPhoto>()
            .HasOne(p => p.Groomer)
            .WithMany()
            .HasForeignKey(p => p.GroomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Conversation>()
            .HasOne(c => c.Client)
            .WithMany()
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Conversation>()
            .HasOne(c => c.Groomer)
            .WithMany()
            .HasForeignKey(c => c.GroomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Conversation>()
            .HasOne(c => c.Appointment)
            .WithMany()
            .HasForeignKey(c => c.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BusinessDayAvailability>()
            .HasIndex(a => new { a.GroomerId, a.Day })
            .IsUnique();

        modelBuilder.Entity<BusinessDayAvailability>()
            .HasOne(a => a.Groomer)
            .WithMany(g => g.DayAvailabilities)
            .HasForeignKey(a => a.GroomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BusinessWeeklyHour>()
            .HasIndex(h => new { h.GroomerId, h.DayOfWeek })
            .IsUnique();

        modelBuilder.Entity<BusinessWeeklyHour>()
            .HasOne(h => h.Groomer)
            .WithMany(g => g.WeeklyHours)
            .HasForeignKey(h => h.GroomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Appointment>().Property(a => a.TotalPrice).HasPrecision(10, 2);
        modelBuilder.Entity<Appointment>().Property(a => a.DepositPaid).HasPrecision(10, 2);
        modelBuilder.Entity<Appointment>().Property(a => a.DiscountAmount).HasPrecision(10, 2);
        modelBuilder.Entity<GroomerProfile>().Property(g => g.StartingPrice).HasPrecision(10, 2);
        modelBuilder.Entity<GroomerService>().Property(s => s.PriceSmall).HasPrecision(10, 2);
        modelBuilder.Entity<GroomerService>().Property(s => s.PriceMedium).HasPrecision(10, 2);
        modelBuilder.Entity<GroomerService>().Property(s => s.PriceLarge).HasPrecision(10, 2);
        modelBuilder.Entity<GroomerService>().Property(s => s.PriceGiant).HasPrecision(10, 2);
        modelBuilder.Entity<ServiceExtra>().Property(e => e.Price).HasPrecision(10, 2);
        modelBuilder.Entity<AppointmentExtra>().Property(e => e.Price).HasPrecision(10, 2);
        modelBuilder.Entity<Pet>().Property(p => p.WeightLbs).HasPrecision(8, 2);
        modelBuilder.Entity<ServiceCatalogItem>().Property(s => s.Price).HasPrecision(10, 2);
        modelBuilder.Entity<Consultation>().Property(c => c.PriceCharged).HasPrecision(10, 2);

        modelBuilder.Entity<ServiceCatalogItem>()
            .HasIndex(s => s.Code)
            .IsUnique();

        modelBuilder.Entity<ProviderLicense>()
            .HasOne(l => l.Groomer)
            .WithMany(g => g.Licenses)
            .HasForeignKey(l => l.GroomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VcprRecord>()
            .HasOne(v => v.Pet)
            .WithMany()
            .HasForeignKey(v => v.PetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VcprRecord>()
            .HasOne(v => v.Provider)
            .WithMany()
            .HasForeignKey(v => v.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Consultation>()
            .HasOne(c => c.Client)
            .WithMany()
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict (not SetNull): SQL Server forbids multiple cascade paths via Users→Pets / Users→Groomers.
        modelBuilder.Entity<Consultation>()
            .HasOne(c => c.Pet)
            .WithMany()
            .HasForeignKey(c => c.PetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Consultation>()
            .HasOne(c => c.Provider)
            .WithMany()
            .HasForeignKey(c => c.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Consultation>()
            .HasOne(c => c.Appointment)
            .WithMany()
            .HasForeignKey(c => c.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ConsentRecord>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ConsentRecord>()
            .HasOne(c => c.Consultation)
            .WithMany()
            .HasForeignKey(c => c.ConsultationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CareSubscription>().Property(s => s.PricePerMonth).HasPrecision(10, 2);

        modelBuilder.Entity<CareSubscription>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CareBenefitUse>()
            .HasOne(u => u.Subscription)
            .WithMany(s => s.BenefitUses)
            .HasForeignKey(u => u.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BehaviorCase>().Property(b => b.PriceCharged).HasPrecision(10, 2);

        modelBuilder.Entity<BehaviorCase>()
            .HasOne(b => b.Client)
            .WithMany()
            .HasForeignKey(b => b.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BehaviorCase>()
            .HasOne(b => b.Pet)
            .WithMany()
            .HasForeignKey(b => b.PetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BehaviorCase>()
            .HasOne(b => b.Provider)
            .WithMany()
            .HasForeignKey(b => b.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BehaviorCase>()
            .HasOne(b => b.Appointment)
            .WithMany()
            .HasForeignKey(b => b.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CountryCatalogEntry>()
            .HasIndex(c => c.Iso2)
            .IsUnique();

        modelBuilder.Entity<ProviderBreedExpertise>()
            .HasOne(e => e.Groomer)
            .WithMany()
            .HasForeignKey(e => e.GroomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderBreedExpertise>()
            .HasIndex(e => new { e.GroomerId, e.Breed })
            .IsUnique();

        modelBuilder.Entity<ProviderCompensationRule>().Property(r => r.CommissionPercent).HasPrecision(5, 2);
        modelBuilder.Entity<ProviderCompensationRule>().Property(r => r.FlatFeeUsd).HasPrecision(10, 2);
        modelBuilder.Entity<ProviderCompensationRule>()
            .HasOne(r => r.ProviderUser)
            .WithMany()
            .HasForeignKey(r => r.ProviderUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderPayout>().Property(p => p.GrossAmountUsd).HasPrecision(10, 2);
        modelBuilder.Entity<ProviderPayout>().Property(p => p.CommissionAmountUsd).HasPrecision(10, 2);
        modelBuilder.Entity<ProviderPayout>().Property(p => p.NetAmountUsd).HasPrecision(10, 2);
        modelBuilder.Entity<ProviderPayout>()
            .HasOne(p => p.ProviderUser)
            .WithMany()
            .HasForeignKey(p => p.ProviderUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProviderPayout>()
            .HasOne(p => p.CompensationRule)
            .WithMany()
            .HasForeignKey(p => p.CompensationRuleId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProfessionalOnboardingApplication>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReminderSchedule>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReminderSchedule>()
            .HasOne(r => r.Pet)
            .WithMany()
            .HasForeignKey(r => r.PetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ReminderDelivery>()
            .HasOne(d => d.ReminderSchedule)
            .WithMany(r => r.Deliveries)
            .HasForeignKey(d => d.ReminderScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
