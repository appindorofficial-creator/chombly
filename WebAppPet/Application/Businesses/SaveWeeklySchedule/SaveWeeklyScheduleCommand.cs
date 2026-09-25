using WebAppPet.Models;

namespace WebAppPet.Application.Businesses.SaveWeeklySchedule;

/// <param name="Week">One entry per day of the week, with DayOfWeek already set.</param>
public sealed record SaveWeeklyScheduleCommand(int BusinessId, IReadOnlyList<WeekDayInput> Week);
