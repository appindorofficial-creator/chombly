using System.Text.RegularExpressions;

namespace WebAppPet.Localization;

/// <summary>
/// Localizes notification titles/messages stored in Spanish for EN UI.
/// Dynamic names (business, pet) are preserved; fixed phrases go through Loc/Map.
/// </summary>
public static class NotificationLocalizer
{
    private static readonly Dictionary<string, string> Titles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Reserva enviada"] = "Booking sent",
        ["Nueva solicitud de reserva"] = "New booking request",
        ["Reserva de hotel enviada"] = "Hotel booking sent",
        ["Nueva reserva de hotel"] = "New hotel booking",
        ["Reserva de daycare enviada"] = "Daycare booking sent",
        ["Nueva reserva de daycare"] = "New daycare booking",
        ["¡Bienvenido a Chombly!"] = "Welcome to Chombly!",
        ["Nuevo negocio pendiente"] = "New business pending",
        ["¡Negocio publicado!"] = "Business published!",
        ["Solicitud no aprobada"] = "Request not approved",
        ["Cita confirmada"] = "Appointment confirmed",
        ["Nueva cita"] = "New appointment",
        ["Cita cancelada"] = "Appointment cancelled",
        ["Recordatorio de cuidado"] = "Care reminder",
        ["Recordatorio de vacuna"] = "Vaccine reminder",
        ["Recordatorio de medicamento"] = "Medication reminder",
        ["Recordatorio de cita"] = "Appointment reminder",
        ["Vacuna antirrábica"] = "Rabies vaccine",
        ["Vacuna antirrabica"] = "Rabies vaccine",
        ["Antirrábica"] = "Rabies",
        ["Antirrabica"] = "Rabies",
        ["Prueba eliminar"] = "Delete test",
        ["Tienes una actualización de tu reserva"] = "You have a booking update",
    };

    private static readonly Dictionary<string, string> ExactMessages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tu cita está lista"] = "Your appointment is ready",
        ["Tienes una actualización de tu reserva"] = "You have a booking update",
        ["Puedes borrar esta notificación"] = "You can delete this notification",
        ["Tu perfil fue creado. Completa la verificación para publicar más rápido."] =
            "Your profile was created. Complete verification to publish faster.",
        ["Tu cuenta fue creada. Agrega tu perro, gato u otra mascota y reserva tu primera cita."] =
            "Your account was created. Add your dog, cat, or other pet and book your first appointment.",
        ["Tu negocio no fue aprobado. Revisa el perfil y vuelve a solicitar desde el panel."] =
            "Your business was not approved. Review the profile and request again from the panel.",
    };

    private static readonly Regex PendingRequest = new(
        @"^Tu solicitud en (.+) está pendiente de confirmación\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CancelledAppt = new(
        @"^Cancelaste tu cita en (.+)\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CareCheck = new(
        @"^Revisa vacunas y chequeo de (.+)\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PublishedBiz = new(
        @"^(.+) ya aparece en Chombly\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NightsPending = new(
        @"^(.+) · (\d+) noche\(s\) · pendiente de confirmación\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NightsOnly = new(
        @"^(.+) · (\d+) noche\(s\)\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PendingTail = new(
        @"^(.+) · pendiente de confirmación\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Title(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        if (!CatalogLocalizer.IsEnglish()) return title;
        var t = title.Trim();
        if (Titles.TryGetValue(t, out var en)) return en;
        return CatalogLocalizer.Text(t);
    }

    public static string Message(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "";
        if (!CatalogLocalizer.IsEnglish()) return message;

        var m = message.Trim();
        if (ExactMessages.TryGetValue(m, out var exact)) return exact;

        var pending = PendingRequest.Match(m);
        if (pending.Success)
            return $"Your request at {pending.Groups[1].Value} is pending confirmation.";

        var cancelled = CancelledAppt.Match(m);
        if (cancelled.Success)
            return $"You cancelled your appointment at {cancelled.Groups[1].Value}.";

        var care = CareCheck.Match(m);
        if (care.Success)
            return $"Review vaccines and checkup for {care.Groups[1].Value}.";

        var published = PublishedBiz.Match(m);
        if (published.Success)
            return $"{published.Groups[1].Value} now appears on Chombly.";

        var nightsPending = NightsPending.Match(m);
        if (nightsPending.Success)
            return $"{nightsPending.Groups[1].Value} · {nightsPending.Groups[2].Value} night(s) · pending confirmation.";

        var nightsOnly = NightsOnly.Match(m);
        if (nightsOnly.Success)
            return $"{nightsOnly.Groups[1].Value} · {nightsOnly.Groups[2].Value} night(s).";

        var pendingTail = PendingTail.Match(m);
        if (pendingTail.Success)
            return $"{LocalizeMessagePart(pendingTail.Groups[1].Value)} · pending confirmation.";

        if (m.Contains(" · ", StringComparison.Ordinal))
        {
            var parts = m.Split(" · ", StringSplitOptions.None);
            for (var i = 0; i < parts.Length; i++)
                parts[i] = LocalizeMessagePart(parts[i]);
            return string.Join(" · ", parts);
        }

        return CatalogLocalizer.Text(m);
    }

    private static string LocalizeMessagePart(string part)
    {
        var t = part.Trim();
        if (t.Length == 0) return t;

        if (t.Equals("pendiente de confirmación", StringComparison.OrdinalIgnoreCase))
            return "pending confirmation";
        if (t.Equals("pendiente de confirmación.", StringComparison.OrdinalIgnoreCase))
            return "pending confirmation.";

        var nights = Regex.Match(t, @"^(\d+)\s*noche\(s\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (nights.Success)
            return $"{nights.Groups[1].Value} night(s)";

        // "Peluquería" category suffix in business registration notifs
        if (t.Contains("Peluquería", StringComparison.OrdinalIgnoreCase))
            t = t.Replace("Peluquería", "Grooming", StringComparison.OrdinalIgnoreCase);
        if (t.EndsWith(" · Business", StringComparison.OrdinalIgnoreCase) ||
            t.EndsWith(" · Negocio", StringComparison.OrdinalIgnoreCase))
        {
            // keep business name; translate trailing label
            t = t.Replace(" · Negocio", " · Business", StringComparison.OrdinalIgnoreCase);
        }

        var mapped = CatalogLocalizer.Text(t);
        if (!string.Equals(mapped, t, StringComparison.Ordinal))
            return mapped;

        return CatalogLocalizer.Notes(t);
    }
}
