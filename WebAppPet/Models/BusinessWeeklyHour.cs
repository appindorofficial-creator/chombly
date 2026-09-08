using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

/// <summary>Horario semanal recurrente del negocio (Lun–Dom).</summary>
public class BusinessWeeklyHour
{
    public int Id { get; set; }

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    /// <summary>0 = Sunday … 6 = Saturday (igual que DayOfWeek).</summary>
    public int DayOfWeek { get; set; }

    public bool IsOpen { get; set; }

    /// <summary>Minutos desde medianoche (ej. 9:00 = 540).</summary>
    public int OpenMinutes { get; set; } = 9 * 60;

    /// <summary>Minutos desde medianoche (ej. 18:00 = 1080).</summary>
    public int CloseMinutes { get; set; } = 18 * 60;

    public string OpenLabel => TimeSpan.FromMinutes(OpenMinutes).ToString(@"hh\:mm");
    public string CloseLabel => TimeSpan.FromMinutes(CloseMinutes).ToString(@"hh\:mm");
}

/// <summary>Fila editable del wizard / panel (no entidad EF).</summary>
public class WeekDayInput
{
    public int DayOfWeek { get; set; }
    public string Label { get; set; } = "";
    public bool IsOpen { get; set; } = true;

    [Display(Name = "Abre")]
    public string OpenTime { get; set; } = "09:00";

    [Display(Name = "Cierra")]
    public string CloseTime { get; set; } = "18:00";

    public static List<WeekDayInput> DefaultWeek()
    {
        var names = new[] { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };
        var list = new List<WeekDayInput>();
        for (var i = 0; i < 7; i++)
        {
            var isWeekend = i is 0 or 6;
            list.Add(new WeekDayInput
            {
                DayOfWeek = i,
                Label = names[i],
                IsOpen = i is >= 1 and <= 6, // Lun–Sáb; Dom cerrado
                OpenTime = "08:00",
                CloseTime = "18:00"
            });
        }
        return list;
    }

    public static int ParseTimeToMinutes(string? time)
    {
        if (TimeSpan.TryParse(time, out var ts))
            return (int)ts.TotalMinutes;
        return 9 * 60;
    }
}
