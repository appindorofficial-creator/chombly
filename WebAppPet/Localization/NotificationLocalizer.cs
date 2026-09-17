using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WebAppPet.Localization;

/// <summary>
/// Localizes notification titles/messages for any wording:
/// 1) exact phrases, 2) structural templates with wildcards, 3) multi-word phrases,
/// 4) word-by-word vocab (proper nouns left as-is).
/// </summary>
public static class NotificationLocalizer
{
    private static readonly Dictionary<string, string> ExactEsToEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Reserva enviada"] = "Booking sent",
        ["Nueva solicitud de reserva"] = "New booking request",
        ["¡Bienvenido a Chombly!"] = "Welcome to Chombly!",
        ["Bienvenido a Chombly!"] = "Welcome to Chombly!",
        ["Nuevo negocio pendiente"] = "New business pending",
        ["¡Negocio publicado!"] = "Business published!",
        ["Solicitud no aprobada"] = "Request not approved",
        ["Cita confirmada"] = "Appointment confirmed",
        ["¡Cita confirmada!"] = "Booking confirmed!",
        ["Cita rechazada"] = "Booking declined",
        ["Servicio completado"] = "Service completed",
        ["Nueva cita"] = "New appointment",
        ["Cita cancelada"] = "Appointment cancelled",
        ["Prueba eliminar"] = "Delete test",
        ["Tienes una actualización de tu reserva"] = "You have a booking update",
        ["Onboarding profesional aprobado"] = "Professional onboarding approved",
        ["Onboarding profesional no aprobado"] = "Professional onboarding not approved",
        ["Negocio reenviado a revisión"] = "Business resubmitted for review",
        ["Nuevo mensaje"] = "New message",
        ["Consulta reservada"] = "Consultation booked",
        ["Seguimiento veterinario"] = "Vet follow-up",
        ["Plan de conducta"] = "Behavior plan",
        ["Tu cita está lista"] = "Your appointment is ready",
        ["Puedes borrar esta notificación"] = "You can delete this notification",
        ["Tu perfil fue creado. Completa la verificación para publicar más rápido."] =
            "Your profile was created. Complete verification to publish faster.",
        ["Tu cuenta fue creada. Agrega tu perro, gato u otra mascota y reserva tu primera cita."] =
            "Your account was created. Add your dog, cat, or other pet and book your first appointment.",
        ["Tu negocio no fue aprobado. Revisa el perfil y vuelve a solicitar desde el panel."] =
            "Your business was not approved. Review the profile and request again from the panel.",
        ["Tu solicitud profesional fue aprobada. Ya puedes atender en Chombly."] =
            "Your professional application was approved. You can now serve on Chombly.",
        ["Tu solicitud necesita correcciones. Revisa las notas del revisor."] =
            "Your application needs corrections. Review the reviewer's notes.",
    };

    /// <summary>Templates: Spanish regex → English format with {0},{1}… (captures localized).</summary>
    private static readonly (Regex Es, string EnFmt)[] TitleTemplates =
    [
        (Rx(@"^Reserva de (.+) enviada!?$"), "{0} booking sent"),
        (Rx(@"^Nueva reserva de (.+)!?$"), "New {0} booking"),
        (Rx(@"^Nueva solicitud de (.+)!?$"), "New {0} request"),
        (Rx(@"^(.+) solicitado!?$"), "{0} requested"),
        (Rx(@"^(.+) solicitada!?$"), "{0} requested"),
        (Rx(@"^Recordatorio de (.+)!?$"), "{0} reminder"),
        (Rx(@"^Vacuna (.+)!?$"), "{0} vaccine"),
        (Rx(@"^Reserva de (.+) enviada$"), "{0} booking sent"),
    ];

    private static readonly (Regex Es, string EnFmt)[] MessageTemplates =
    [
        (Rx(@"^Tu solicitud en (.+) está pendiente de confirmación\.?$"),
            "Your request at {0} is pending confirmation."),
        (Rx(@"^Cancelaste tu cita en (.+)\.?$"),
            "You cancelled your appointment at {0}."),
        (Rx(@"^Revisa vacunas y chequeo de (.+)\.?$"),
            "Review vaccines and checkup for {0}."),
        (Rx(@"^(.+) ya aparece en Chombly\.?$"),
            "{0} now appears on Chombly."),
        (Rx(@"^(.+) · (\d+)\s*noche\(s\) · pendiente de confirmación\.?$"),
            "{0} · {1} night(s) · pending confirmation."),
        (Rx(@"^(.+) · (\d+)\s*noche\(s\)\.?$"),
            "{0} · {1} night(s)."),
        (Rx(@"^(.+) · pendiente de confirmación\.?$"),
            "{0} · pending confirmation."),
        (Rx(@"^(.+) confirmó tu cita \((.+)\)\.?$"),
            "{0} confirmed your booking ({1})."),
        (Rx(@"^(.+) no pudo aceptar tu cita \((.+)\)\.?$"),
            "{0} could not accept your booking ({1})."),
        (Rx(@"^(.+) completó el servicio\.?\s*¡?Cuéntanos cómo quedó tu mascota!? \((.+)\)\.?$"),
            "{0} completed the service. Tell us how your pet looks! ({1})."),
        (Rx(@"^Tu consulta \((.+)\) quedó pendiente de confirmación\.?$"),
            "Your consultation ({0}) is pending confirmation."),
    ];

    /// <summary>Longest-first multi-word replacements (any remaining Spanish chunks).</summary>
    private static readonly (string Es, string En)[] Phrases =
    [
        ("pendiente de confirmación", "pending confirmation"),
        ("noche(s)", "night(s)"),
        ("onboarding profesional", "professional onboarding"),
        ("solicitud de reserva", "booking request"),
        ("solicitud de paseo", "walk request"),
        ("actualización de tu reserva", "booking update"),
        ("primera cita", "first appointment"),
        ("medio día", "half day"),
        ("día completo", "full day"),
        ("vacuna antirrábica", "rabies vaccine"),
        ("vacuna antirrabica", "rabies vaccine"),
    ];

    /// <summary>Single-token vocab so any Spanish word in notifs can flip to EN.</summary>
    private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        // articles / prep / pronouns
        ["el"] = "the", ["la"] = "the", ["los"] = "the", ["las"] = "the",
        ["un"] = "a", ["una"] = "a", ["unos"] = "some", ["unas"] = "some",
        ["de"] = "of", ["del"] = "of the", ["al"] = "to the",
        ["a"] = "to", ["en"] = "in", ["con"] = "with", ["sin"] = "without",
        ["por"] = "by", ["para"] = "for", ["y"] = "and", ["o"] = "or",
        ["tu"] = "your", ["su"] = "your", ["tus"] = "your", ["sus"] = "your",
        ["mi"] = "my", ["mis"] = "my", ["más"] = "more", ["mas"] = "more",
        ["no"] = "not", ["ya"] = "already", ["también"] = "also", ["tambien"] = "also",
        // common notif nouns / verbs / adjectives
        ["nueva"] = "new", ["nuevo"] = "new", ["nuevas"] = "new", ["nuevos"] = "new",
        ["reserva"] = "booking", ["reservas"] = "bookings",
        ["solicitud"] = "request", ["solicitudes"] = "requests",
        ["enviada"] = "sent", ["enviado"] = "sent", ["enviadas"] = "sent", ["enviados"] = "sent",
        ["solicitado"] = "requested", ["solicitada"] = "requested",
        ["confirmada"] = "confirmed", ["confirmado"] = "confirmed",
        ["confirmación"] = "confirmation", ["confirmacion"] = "confirmation",
        ["pendiente"] = "pending", ["pendientes"] = "pending",
        ["cancelada"] = "cancelled", ["cancelado"] = "cancelled", ["cancelaste"] = "you cancelled",
        ["rechazada"] = "declined", ["rechazado"] = "declined",
        ["aprobada"] = "approved", ["aprobado"] = "approved",
        ["publicado"] = "published", ["publicada"] = "published",
        ["negocio"] = "business", ["negocios"] = "businesses",
        ["cita"] = "appointment", ["citas"] = "appointments",
        ["mensaje"] = "message", ["mensajes"] = "messages",
        ["recordatorio"] = "reminder", ["recordatorios"] = "reminders",
        ["cuidado"] = "care", ["vacuna"] = "vaccine", ["vacunas"] = "vaccines",
        ["medicamento"] = "medication", ["medicamentos"] = "medications",
        ["paseo"] = "walk", ["paseos"] = "walks",
        ["training"] = "training", ["entrenamiento"] = "training",
        ["daycare"] = "daycare", ["guardería"] = "daycare", ["guarderia"] = "daycare",
        ["hotel"] = "hotel", ["peluquería"] = "grooming", ["peluqueria"] = "grooming",
        ["grooming"] = "grooming", ["veterinaria"] = "veterinary", ["veterinario"] = "veterinary",
        ["consulta"] = "consultation", ["seguimiento"] = "follow-up",
        ["plan"] = "plan", ["conducta"] = "behavior", ["comportamiento"] = "behavior",
        ["perfil"] = "profile", ["cuenta"] = "account", ["mascota"] = "pet", ["mascotas"] = "pets",
        ["perro"] = "dog", ["gato"] = "cat", ["noche"] = "night", ["noches"] = "nights",
        ["día"] = "day", ["dia"] = "day", ["días"] = "days", ["dias"] = "days",
        ["sesión"] = "session", ["sesion"] = "session", ["min"] = "min",
        ["completado"] = "completed", ["completó"] = "completed", ["completo"] = "completed",
        ["servicio"] = "service", ["profesional"] = "professional",
        ["onboarding"] = "onboarding", ["revisión"] = "review", ["revision"] = "review",
        ["reenviado"] = "resubmitted", ["verificación"] = "verification", ["verificacion"] = "verification",
        ["bienvenido"] = "welcome", ["antirrábica"] = "rabies", ["antirrabica"] = "rabies",
        ["urgente"] = "urgent", ["urgencias"] = "emergency",
        ["tienes"] = "you have", ["tuviste"] = "you had",
        ["agrega"] = "add", ["elige"] = "choose", ["revisa"] = "review",
        ["puedes"] = "you can", ["atender"] = "serve", ["borrar"] = "delete",
        ["esta"] = "this", ["este"] = "this", ["estos"] = "these", ["estas"] = "these",
        ["notificación"] = "notification", ["notificacion"] = "notification",
        ["actualización"] = "update", ["actualizacion"] = "update",
        ["primera"] = "first", ["primer"] = "first", ["otra"] = "other", ["otro"] = "other",
        ["lista"] = "ready", ["listo"] = "ready",
        ["independent"] = "independent", ["independiente"] = "independent",
        ["business"] = "business", ["walkers"] = "walkers", ["paseadores"] = "walkers",
        ["entrenadores"] = "trainers", ["trainers"] = "trainers",
        ["canino"] = "canine", ["canina"] = "canine", ["felino"] = "feline", ["felina"] = "feline",
        ["baño"] = "bath", ["bano"] = "bath", ["corte"] = "haircut",
        ["chequeo"] = "checkup", ["cómo"] = "how", ["como"] = "how",
        ["quedó"] = "looks", ["quedo"] = "looks", ["cuéntanos"] = "tell us", ["cuentanos"] = "tell us",
    };

    private static readonly Dictionary<string, string> ExactEnToEs;
    private static readonly (string En, string Es)[] PhrasesEnToEs;

    static NotificationLocalizer()
    {
        ExactEnToEs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (es, en) in ExactEsToEn)
            ExactEnToEs.TryAdd(en, es);

        PhrasesEnToEs = Phrases
            .Select(p => (p.En, p.Es))
            .GroupBy(p => p.En, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(p => p.En.Length)
            .ToArray();
    }

    private static Regex Rx(string pattern) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string Title(string? title) => Localize(title, isTitle: true);

    public static string Message(string? message) => Localize(message, isTitle: false);

    private static string Localize(string? text, bool isTitle)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var raw = text.Trim();
        var en = CatalogLocalizer.IsEnglish();

        if (en)
        {
            if (ExactEsToEn.TryGetValue(raw, out var exact)) return exact;
            if (TryTemplates(raw, isTitle ? TitleTemplates : MessageTemplates, out var fromTpl))
                return fromTpl;
            if (!isTitle && raw.Contains(" · ", StringComparison.Ordinal))
                return string.Join(" · ", raw.Split(" · ").Select(LocalizeChunkToEn));
            return LocalizeChunkToEn(raw);
        }

        // Spanish UI: reverse exact / light reverse of English stored via Loc
        if (ExactEnToEs.TryGetValue(raw, out var esExact)) return esExact;
        return ReverseEnglishStored(raw);
    }

    private static bool TryTemplates(string raw, (Regex Es, string EnFmt)[] templates, out string result)
    {
        foreach (var (rx, fmt) in templates)
        {
            var m = rx.Match(raw);
            if (!m.Success) continue;
            var args = new object[m.Groups.Count - 1];
            for (var i = 1; i < m.Groups.Count; i++)
            {
                var g = m.Groups[i].Value.Trim();
                // Numeric captures stay; names/categories get word localization.
                args[i - 1] = LooksNumeric(g) ? g : LocalizeChunkToEn(g);
            }
            result = CapitalizeFirst(string.Format(CultureInfo.InvariantCulture, fmt, args));
            return true;
        }
        result = "";
        return false;
    }

    private static string LocalizeChunkToEn(string chunk)
    {
        var t = chunk.Trim();
        if (t.Length == 0) return t;
        if (ExactEsToEn.TryGetValue(t, out var exact)) return exact;

        // Parenthetical category: Name (Peluquería)
        var paren = Regex.Match(t, @"^(.+?)\((.+)\)\s*$");
        if (paren.Success)
        {
            var name = paren.Groups[1].Value.TrimEnd();
            var cat = LocalizeChunkToEn(paren.Groups[2].Value.Trim());
            return $"{name} ({cat})";
        }

        // Multi-word phrases first (longest)
        var work = t;
        foreach (var (es, en) in Phrases.OrderByDescending(p => p.Es.Length))
            work = ReplaceInsensitive(work, es, en);

        // Catalog notes (schedule labels, etc.) when exact
        var notes = CatalogLocalizer.Notes(work);
        if (!string.Equals(notes, work, StringComparison.Ordinal))
            work = notes;

        return TranslateWords(work);
    }

    private static string TranslateWords(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (IsWordChar(c))
            {
                var start = i;
                while (i < text.Length && IsWordChar(text[i])) i++;
                var token = text[start..i];
                if (Words.TryGetValue(token, out var en))
                    sb.Append(MatchCase(token, en));
                else
                    sb.Append(token); // proper noun / unknown / already EN
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        return sb.ToString();
    }

    private static string ReverseEnglishStored(string raw)
    {
        // Best-effort for messages written with Loc() while UI was EN.
        var work = raw;
        foreach (var (en, es) in PhrasesEnToEs)
            work = ReplaceInsensitive(work, en, es);

        work = Regex.Replace(work, @"^Your request at (.+) is pending confirmation\.?$",
            "Tu solicitud en $1 está pendiente de confirmación.", RegexOptions.IgnoreCase);
        work = Regex.Replace(work, @"^You cancelled your appointment at (.+)\.?$",
            "Cancelaste tu cita en $1.", RegexOptions.IgnoreCase);
        work = Regex.Replace(work, @"^(.+) · pending confirmation\.?$",
            "$1 · pendiente de confirmación.", RegexOptions.IgnoreCase);
        work = Regex.Replace(work, @"^(.+) booking sent$",
            "Reserva de $1 enviada", RegexOptions.IgnoreCase);
        work = Regex.Replace(work, @"^New (.+) booking$",
            "Nueva reserva de $1", RegexOptions.IgnoreCase);
        work = Regex.Replace(work, @"^New (.+) request$",
            "Nueva solicitud de $1", RegexOptions.IgnoreCase);
        work = Regex.Replace(work, @"^(.+) requested$",
            "$1 solicitado", RegexOptions.IgnoreCase);
        work = Regex.Replace(work, @"^(.+) reminder$",
            "Recordatorio de $1", RegexOptions.IgnoreCase);

        return work;
    }

    private static string ReplaceInsensitive(string input, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(oldValue) || input.Length == 0) return input;
        var idx = input.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return input;
        var sb = new StringBuilder();
        var last = 0;
        while (idx >= 0)
        {
            sb.Append(input, last, idx - last);
            sb.Append(newValue);
            last = idx + oldValue.Length;
            idx = input.IndexOf(oldValue, last, StringComparison.OrdinalIgnoreCase);
        }
        sb.Append(input, last, input.Length - last);
        return sb.ToString();
    }

    private static bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || c is '_' or '\'';

    private static bool LooksNumeric(string s) =>
        s.Length > 0 && s.All(c => char.IsDigit(c) || c is '/' or ':' or '-' or ' ' or '.');

    private static string MatchCase(string original, string replacement)
    {
        if (original.Length == 0 || replacement.Length == 0) return replacement;
        if (original.All(char.IsUpper)) return replacement.ToUpperInvariant();
        if (char.IsUpper(original[0]))
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];
        return replacement;
    }

    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        for (var i = 0; i < s.Length; i++)
        {
            if (!char.IsLetter(s[i])) continue;
            if (char.IsUpper(s[i])) return s;
            return string.Concat(s.AsSpan(0, i), char.ToUpperInvariant(s[i]).ToString(), s.AsSpan(i + 1));
        }
        return s;
    }
}
