using WebAppPet.Models;

namespace WebAppPet.Services;

public static class BusinessExploreDemo
{
    public const string OwnerName = "María";
    public const string BusinessName = "Peluquería Luna";
    public const string City = "Neiva";
    public const string Phone = "3001234567";
    public const string About = "Baño, corte y spa para perros y gatos. Atención personalizada y productos hipoalergénicos.";

    public static readonly decimal TodayIncome = 185_000m;
    public static readonly int TodayCompleted = 3;
    public static readonly int UpcomingCount = 2;
    public static readonly int PendingCount = 1;
    public static readonly int OpenWeekDays = 6;
    public static readonly int ServiceCount = 4;

    public static readonly IReadOnlyList<string> CategoryLabels = new[]
    {
        "Peluquería", "Guardería"
    };

    public static readonly IReadOnlyList<(string Slug, string Name)> Categories = new[]
    {
        ("grooming", "Peluquería"),
        ("daycare", "Guardería")
    };

    public static readonly IReadOnlyList<(string Name, string Unit, decimal Price)> Services = new[]
    {
        ("Baño y cepillado", "sesión", 45_000m),
        ("Grooming completo", "sesión", 75_000m),
        ("Día completo", "día", 40_000m),
        ("Corte de uñas", "sesión", 18_000m)
    };

    public static readonly IReadOnlyList<WeekDayInput> Week = WeekDayInput.DefaultWeek();

    public static IReadOnlyList<DemoAppointment> Appointments(string tab)
    {
        var baseDate = DateTime.Today.AddDays(1);
        var all = new List<DemoAppointment>
        {
            new("DULCE", PetSpecies.Dog, "Grooming completo", "Carlos Ruiz", "3105550101", 75_000m,
                baseDate.AddHours(10), AppointmentStatus.Pending, false),
            new("THOR", PetSpecies.Cat, "Baño y cepillado", "Ana Gómez", "3155550202", 45_000m,
                baseDate.AddDays(1).AddHours(11), AppointmentStatus.Confirmed, true),
            new("MAX", PetSpecies.Dog, "Día completo", "Laura Pérez", "3205550303", 40_000m,
                baseDate.AddDays(2).AddHours(9), AppointmentStatus.Confirmed, false),
            new("LUNA", PetSpecies.Dog, "Corte de uñas", "Pedro Díaz", "3185550404", 18_000m,
                DateTime.Today.AddDays(-2).AddHours(15), AppointmentStatus.Completed, false)
        };

        return tab switch
        {
            "upcoming" => all.Where(a => a.Status == AppointmentStatus.Confirmed).ToList(),
            "history" => all.Where(a => a.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled).ToList(),
            _ => all.Where(a => a.Status == AppointmentStatus.Pending).ToList()
        };
    }

    public static IReadOnlyList<DemoDaySlot> CalendarDays()
    {
        var list = new List<DemoDaySlot>();
        for (var i = 0; i < 30; i++)
        {
            var day = DateTime.Today.AddDays(i);
            var open = day.DayOfWeek != DayOfWeek.Sunday;
            list.Add(new DemoDaySlot(day, open));
        }
        return list;
    }

    public record DemoAppointment(
        string PetName,
        string Species,
        string Service,
        string ClientName,
        string Phone,
        decimal Price,
        DateTime When,
        AppointmentStatus Status,
        bool Anxious);

    public record DemoDaySlot(DateTime Day, bool IsAvailable);
}
