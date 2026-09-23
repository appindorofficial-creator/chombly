using System.Globalization;
using WebAppPet.Models;

namespace WebAppPet.Localization;

/// <summary>
/// Traduce textos de catálogo guardados en español en la BD (servicios, amenities, unidades).
/// El valor original se mantiene para búsqueda/filtros; solo cambia la UI.
/// </summary>
public static class CatalogLocalizer
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Services
        ["Baño y cepillado"] = "Bath & brush",
        ["Baño y secado"] = "Bath & brush",
        ["Baño básico"] = "Basic bath",
        ["Baño adicional"] = "Extra bath",
        ["Baño extra durante la estadía"] = "Extra bath during the stay",
        ["Si tu mascota necesita medicación durante la estadía"] =
            "If your pet needs medication during the stay",
        ["Baño al final del día"] = "End-of-day bath",
        ["Grooming completo"] = "Full grooming",
        ["Grooming para gatos"] = "Cat grooming",
        ["Corte"] = "Haircut",
        ["Corte de pelo"] = "Haircut",
        ["Deshedding"] = "Deshedding",
        ["Consulta general"] = "General checkup",
        ["Chequeo general"] = "General checkup",
        ["Vacunas"] = "Vaccines",
        ["Vacuna"] = "Vaccine",
        ["Antirrábica"] = "Rabies",
        ["Antirrabica"] = "Rabies",
        ["Vacuna antirrábica"] = "Rabies vaccine",
        ["Vacuna antirrabica"] = "Rabies vaccine",
        ["Recordatorio de vacuna"] = "Vaccine reminder",
        ["Recordatorio de medicamento"] = "Medication reminder",
        ["Recordatorio de cita"] = "Appointment reminder",
        ["Urgencias"] = "Emergencies",
        ["Sesión de comportamiento"] = "Behavior session",
        ["Orientación internacional 30 min"] = "International orientation 30 min",
        ["Noche estándar"] = "Standard night",
        ["Noche premium"] = "Premium night",
        ["Día completo"] = "Full day",
        ["Medio día"] = "Half day",
        ["Paseo 30 min"] = "30-min walk",
        ["Paseo 60 min"] = "60-min walk",
        ["Paseo 90 min"] = "90-min walk",
        ["Paseo 120 min"] = "120-min walk",
        ["Obediencia básica"] = "Basic obedience",
        ["Cachorros"] = "Puppies",
        ["Modificación de conducta"] = "Behavior modification",
        ["Corrección de comportamiento"] = "Behavior correction",
        ["Entrenamiento avanzado"] = "Advanced training",
        ["Ladridos excesivos"] = "Excessive barking",
        ["Ansiedad por separación"] = "Separation anxiety",
        ["Tirones en paseo"] = "Leash pulling",
        ["Agresión a perros"] = "Dog aggression",
        ["Agresión a personas"] = "People aggression",
        ["Miedos / ruidos"] = "Fears / noises",
        ["Diario"] = "Daily",
        ["Varias veces/semana"] = "Several times/week",
        ["Ocasional"] = "Occasional",
        ["Primera vez"] = "First time",
        ["Otro"] = "Other",
        ["Administración de medicamentos"] = "Medication administration",
        ["Cámara privada"] = "Private camera",
        ["Acceso a cámara privada de la suite durante la estadía"] =
            "Private suite camera access during the stay",
        ["Transporte (ida y vuelta)"] = "Round-trip transport",

        // Amenities / prefs
        ["Cámaras 24/7"] = "24/7 cameras",
        ["Patio grande"] = "Large yard",
        ["Personal 24/7"] = "24/7 staff",
        ["Acepta mascotas grandes"] = "Accepts large pets",
        ["Acepta perros grandes"] = "Accepts large dogs",
        ["Abierto ahora"] = "Open now",
        ["Emergencias 24/7"] = "24/7 emergencies",
        ["Cerca de mí (5 mi)"] = "Near me (5 mi)",
        ["Quitar filtros"] = "Clear filters",
        ["Ver todos en Buscar"] = "See all in Search",
        ["Avisar / Dejar solicitud"] = "Notify / Leave a request",
        ["Elige una clínica para reservar, o deja una solicitud"] = "Pick a clinic to book, or leave a request",
        ["No hay clínicas veterinarias en la base de datos con esos filtros."] = "No veterinary clinics match these filters.",
        ["Aún no hay clínicas veterinarias publicadas."] = "No veterinary clinics are published yet.",
        ["Recibimos tu solicitud. Te avisaremos cuando haya clínicas disponibles."] = "We received your request. We'll notify you when clinics are available.",
        ["Áreas de juego"] = "Play areas",
        ["Siesta / descanso"] = "Nap / rest",
        ["Cámaras en vivo"] = "Live cameras",
        ["Paseo individual"] = "Solo walk",
        ["Sin escaleras"] = "No stairs",
        ["Foto del paseo"] = "Walk photo",
        ["Entrenador con certificación"] = "Certified trainer",
        ["Experiencia con mi raza"] = "Experience with my breed",
        ["Clases en el parque"] = "Park classes",
        ["Flexible en horario"] = "Flexible schedule",
        ["A domicilio"] = "At home",
        ["En centro"] = "At facility",
        ["En mi lugar / centro"] = "At my place / facility",
        ["Virtual"] = "Virtual",

        // Booking UI options
        ["Ahora"] = "Now",
        ["9:00 AM"] = "9:00 AM",
        ["1:00 PM"] = "1:00 PM",
        ["6:00 PM"] = "6:00 PM",
        ["Mañana 9:00 AM"] = "9:00 AM",
        ["Tarde 1:00 PM"] = "1:00 PM",
        ["Noche 6:00 PM"] = "6:00 PM",
        ["Enfermedad / Síntomas"] = "Illness / Symptoms",
        ["Emergencia"] = "Emergency",
        ["Dermatología"] = "Dermatology",
        ["Otro"] = "Other",
        ["Personalizado"] = "Custom",
        ["hasta 5 horas"] = "up to 5 hours",
        ["Entrenamiento"] = "Training",
        ["hasta 5 horas"] = "up to 5 hours",
        ["elige horario"] = "pick a time",
        ["Personalizado"] = "Custom",
        ["1 sesión"] = "1 session",
        ["Paquete 4 sesiones"] = "4-session pack",
        ["Paquete 8 sesiones"] = "8-session pack",
        ["Recomendado"] = "Recommended",
        ["Cerrado ahora"] = "Closed now",
        ["Desde"] = "From",
        ["Total estimado"] = "Estimated total",
        ["sesiones"] = "sessions",
        ["ocupado"] = "taken",
        ["Consulta"] = "Checkup",
        ["Hoy"] = "Today",
        ["Mañana"] = "Tomorrow",
        ["Elegir fechas"] = "Pick dates",
        ["Elegir guardería"] = "Choose daycare",
        ["Elegir daycare"] = "Choose daycare",
        ["Ver más guarderías"] = "See more daycares",
        ["Ver más daycares"] = "See more daycares",
        ["5. Guarderías disponibles cerca de ti"] = "5. Daycares available near you",
        ["5. Daycares disponibles cerca de ti"] = "5. Daycares available near you",
        ["No hay guarderías con esos filtros en la base de datos."] = "No daycares match these filters.",
        ["No hay daycares con esos filtros en la base de datos."] = "No daycares match these filters.",
        ["8. Notas para la guardería"] = "8. Notes for the daycare",
        ["8. Notas para el daycare"] = "8. Notes for the daycare",
        ["Elige guardería y mascota para continuar."] = "Choose a daycare and pet to continue.",
        ["Elige daycare y mascota para continuar."] = "Choose a daycare and pet to continue.",
        ["Guardería"] = "Daycare",
        ["guardería"] = "daycare",
        ["guarderías"] = "daycares",
        ["daycare"] = "daycare",
        ["daycares"] = "daycares",

        ["Para cualquier mascota"] = "For any pet",
        ["Baño suave felino"] = "Gentle feline bath",
        ["Revisión clínica"] = "Clinical checkup",
        ["Hospedaje por noche"] = "Overnight boarding",
        ["Suite con cámara privada"] = "Suite with private camera",
        ["7 AM – 7 PM"] = "7 AM – 7 PM",
        ["Hasta 5 horas"] = "Up to 5 hours",
        ["Paseo corto"] = "Short walk",
        ["Paseo estándar"] = "Standard walk",
        ["Paseo largo"] = "Long walk",
        ["Paseo extendido"] = "Extended walk",
        ["Sesión individual"] = "Private session",
        ["Socialización y bases"] = "Socialization & basics",
        ["Conducta reactiva o ansiedad"] = "Reactive behavior or anxiety",
        ["Señales avanzadas"] = "Advanced cues",
        ["/ noche"] = "/ night",
        ["/ sesión"] = "/ session",
        ["/ visita"] = "/ visit",
        ["/ día"] = "/ day",
        ["/ 60 min"] = "/ 60 min",
        ["/ baño"] = "/ bath",
        ["/ urgencia"] = "/ emergency",
        ["/ sesion"] = "/ session",
        ["/ consulta"] = "/ consult",
        ["/ servicio"] = "/ service",
        ["/ paseo"] = "/ walk",
        ["baño"] = "bath",
        ["urgencia"] = "emergency",
        ["consulta"] = "consult",
        ["servicio"] = "service",
        ["Urgencia veterinaria"] = "Veterinary emergency",
        ["Sesión de entrenamiento"] = "Training session",
        ["Peluquería completa"] = "Full grooming",
        ["Peluquería"] = "Grooming",
        ["Paseadores"] = "Walkers",
        ["Entrenadores"] = "Trainers",
        ["Veterinaria"] = "Veterinary",
        ["Guardería"] = "Daycare",
        ["Hotel"] = "Hotel",
        ["Servicio"] = "Service",
        ["Grooming profesional"] = "Professional grooming",
        ["Atención de urgencias 24/7"] = "24/7 emergency care",
        ["Paseo 40 min"] = "40-min walk",
        ["Hospedaje 1 noche"] = "1-night boarding",
        ["Hospedaje multi-mascota con cámaras 24/7."] = "Multi-pet boarding with 24/7 cameras.",
        ["Hospedaje multi-mascota con cámaras 24/7"] = "Multi-pet boarding with 24/7 cameras",
        ["Baño y corte"] = "Bath & haircut",
        ["noche"] = "night",
        ["sesion"] = "session",
        ["visita"] = "visit",
        ["dia"] = "day",
        ["medio"] = "half-day",
        ["paseo"] = "walk",

        // Pet defaults / free-text stored in ES
        ["Amigable"] = "Friendly",
        ["Mestizo / mixto"] = "Mixed / mix",
        ["Paseos individuales con foto del paseo. Acepta perros grandes. Sin escaleras."] =
            "Individual walks with a walk photo. Accepts large dogs. No stairs.",
        ["Grooming para perros, gatos y más."] =
            "Grooming for dogs, cats, and more.",
        ["Grooming para perros, gatos y más"] =
            "Grooming for dogs, cats, and more",
        ["Daycare con áreas de juego, siesta y cámaras en vivo."] =
            "Daycare with play areas, nap time, and live cameras.",
        ["Daycare con áreas de juego, siesta y cámaras en vivo"] =
            "Daycare with play areas, nap time, and live cameras",
        ["Entrenador certificado. Obediencia, cachorros y modificación de conducta. A domicilio, centro o virtual. Clases en el parque. Flexible en horario."] =
            "Certified trainer. Obedience, puppies, and behavior modification. At home, at a facility, or virtual. Park classes. Flexible schedule.",
        ["Entrenador certificado. Obediencia, cachorros y modificación de conducta. A domicilio, centro o virtual. Clases en el parque. Flexible en horario"] =
            "Certified trainer. Obedience, puppies, and behavior modification. At home, at a facility, or virtual. Park classes. Flexible schedule",
        ["Consultas multi-especie."] = "Multi-species checkups.",
        ["Consultas multi-especie"] = "Multi-species checkups",

        // About / Sobre nosotros (GroomerProfile.About — stored in Spanish)
        ["Veterinario local licenciado en Carolina del Norte."] =
            "Local veterinarian licensed in North Carolina.",
        ["Veterinario local licenciado en Carolina del Norte"] =
            "Local veterinarian licensed in North Carolina",
        ["Orientación internacional. Licenciado en Colombia."] =
            "International guidance. Licensed in Colombia.",
        ["Orientación internacional. Licenciado en Colombia"] =
            "International guidance. Licensed in Colombia",
        ["Orientación internacional. Licenciada en El Salvador. Experiencia con razas pequeñas."] =
            "International guidance. Licensed in El Salvador. Experience with small breeds.",
        ["Orientación internacional. Licenciada en El Salvador. Experiencia con razas pequeñas"] =
            "International guidance. Licensed in El Salvador. Experience with small breeds",
        ["Orientación internacional. Licenciado en México. Enfoque en nutrición y razas grandes."] =
            "International guidance. Licensed in Mexico. Focus on nutrition and large breeds.",
        ["Orientación internacional. Licenciado en México. Enfoque en nutrición y razas grandes"] =
            "International guidance. Licensed in Mexico. Focus on nutrition and large breeds",
        ["Clínica de urgencias veterinarias 24/7."] =
            "24/7 veterinary emergency clinic.",
        ["Clínica de urgencias veterinarias 24/7"] =
            "24/7 veterinary emergency clinic",
        ["Especialista en comportamiento canino. Plan educativo; no diagnostica ni medica."] =
            "Canine behavior specialist. Educational plan; does not diagnose or prescribe.",
        ["Especialista en comportamiento canino. Plan educativo; no diagnostica ni medica"] =
            "Canine behavior specialist. Educational plan; does not diagnose or prescribe",
        ["Clínica veterinaria en Neiva: consultas, vacunas y belleza canina. Datos demo Chombly."] =
            "Veterinary clinic in Neiva: checkups, vaccines, and dog grooming. Chombly demo data.",
        ["Clínica veterinaria en Neiva: consultas, vacunas y belleza canina. Datos demo Chombly"] =
            "Veterinary clinic in Neiva: checkups, vaccines, and dog grooming. Chombly demo data",
        ["Urgencias veterinarias 24/7 en Neiva. Hospitalización y cuidado crítico (demo)."] =
            "24/7 veterinary emergencies in Neiva. Hospitalization and critical care (demo).",
        ["Urgencias veterinarias 24/7 en Neiva. Hospitalización y cuidado crítico (demo)"] =
            "24/7 veterinary emergencies in Neiva. Hospitalization and critical care (demo)",
        ["Peluquería canina y felina en barrio La Rioja. Baños medicados y estética (demo)."] =
            "Dog and cat grooming in La Rioja. Medicated baths and styling (demo).",
        ["Peluquería canina y felina en barrio La Rioja. Baños medicados y estética (demo)"] =
            "Dog and cat grooming in La Rioja. Medicated baths and styling (demo)",
        ["Peluquería y spa en el centro de Neiva. Perros y gatos (demo)."] =
            "Grooming and spa in downtown Neiva. Dogs and cats (demo).",
        ["Peluquería y spa en el centro de Neiva. Perros y gatos (demo)"] =
            "Grooming and spa in downtown Neiva. Dogs and cats (demo)",
        ["Hospedaje nocturno y cuidado tipo hotel para mascotas en Neiva (demo)."] =
            "Overnight boarding and hotel-style pet care in Neiva (demo).",
        ["Hospedaje nocturno y cuidado tipo hotel para mascotas en Neiva (demo)"] =
            "Overnight boarding and hotel-style pet care in Neiva (demo)",
        ["Guardería diurna con recreación y supervisión en Neiva (demo)."] =
            "Daycare with recreation and supervision in Neiva (demo).",
        ["Guardería diurna con recreación y supervisión en Neiva (demo)"] =
            "Daycare with recreation and supervision in Neiva (demo)",
        ["Paseos profesionales por parques y zonas de Neiva (demo)."] =
            "Professional walks through parks and areas of Neiva (demo).",
        ["Paseos profesionales por parques y zonas de Neiva (demo)"] =
            "Professional walks through parks and areas of Neiva (demo)",
        ["Entrenamiento y modificación de conducta en Neiva. Plan educativo (demo)."] =
            "Training and behavior modification in Neiva. Educational plan (demo).",
        ["Entrenamiento y modificación de conducta en Neiva. Plan educativo (demo)"] =
            "Training and behavior modification in Neiva. Educational plan (demo)",
        ["Clínica, spa y pet shop al sur de Neiva, vía Caguán (demo)."] =
            "Clinic, spa, and pet shop in southern Neiva, Caguán road (demo).",
        ["Clínica, spa y pet shop al sur de Neiva, vía Caguán (demo)"] =
            "Clinic, spa, and pet shop in southern Neiva, Caguán road (demo)",
        ["Baño, peluquería, guardería y tip de adiestramiento en un solo lugar (demo)."] =
            "Bath, grooming, daycare, and training tips in one place (demo).",
        ["Baño, peluquería, guardería y tip de adiestramiento en un solo lugar (demo)"] =
            "Bath, grooming, daycare, and training tips in one place (demo)",
        ["El mejor centro de atenciòn para tu mascota"] =
            "The best care center for your pet",
        ["El mejor centro de atención para tu mascota"] =
            "The best care center for your pet",
        ["El mejor centro de atenciòn para tu mascota."] =
            "The best care center for your pet.",
        ["El mejor centro de atención para tu mascota."] =
            "The best care center for your pet.",
        ["Negocio de prueba en Neiva para validar el alta sin pedir cuenta otra vez."] =
            "Test business in Neiva to validate signup without creating another account.",
        ["Negocio de prueba en Neiva para validar el alta sin pedir cuenta otra vez"] =
            "Test business in Neiva to validate signup without creating another account",

        // Business Explore demo (Welcome → Negocios → Explorar)
        ["Peluquería Luna"] = "Luna Grooming",
        ["Baño, corte y spa para perros y gatos. Atención personalizada y productos hipoalergénicos."] =
            "Bath, haircut, and spa for dogs and cats. Personalized care and hypoallergenic products.",
        ["Baño, corte y spa para perros y gatos. Atención personalizada y productos hipoalergénicos"] =
            "Bath, haircut, and spa for dogs and cats. Personalized care and hypoallergenic products",
        ["Corte de uñas"] = "Nail trim",

        // Appointment notes (stored fragments)
        ["Horario:"] = "Schedule:",
        ["Pago:"] = "Payment:",
        ["Tipo:"] = "Type:",
        ["Lugar:"] = "Place:",
        ["Medio día (hasta 5 h)"] = "Half day (up to 5 h)",
        ["Día completo (7 AM – 7 PM)"] = "Full day (7 AM – 7 PM)",
        ["mascotas"] = "pets",
    };

    /// <summary>Word-level fallback for free-form catalog labels (e.g. extras like "Baño test").</summary>
    private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Baño"] = "Bath",
        ["Vacuna"] = "Vaccine",
        ["Vacunas"] = "Vaccines",
        ["Antirrábica"] = "Rabies",
        ["Antirrabica"] = "Rabies",
        ["Noche"] = "Night",
        ["Día"] = "Day",
        ["Dia"] = "Day",
        ["Medio"] = "Half",
        ["completo"] = "full",
        ["básico"] = "basic",
        ["basico"] = "basic",
        ["adicional"] = "extra",
        ["estándar"] = "standard",
        ["estandar"] = "standard",
        ["premium"] = "premium",
        ["Corte"] = "Haircut",
        ["pelo"] = "hair",
        ["sesión"] = "session",
        ["sesion"] = "session",
        ["visita"] = "visit",
        ["Paseo"] = "Walk",
        ["Grooming"] = "Grooming",
        ["suave"] = "gentle",
        ["felino"] = "feline",
        ["Hospedaje"] = "Boarding",
        ["multi-mascota"] = "multi-pet",
        ["con"] = "with",
        ["cámaras"] = "cameras",
        ["camaras"] = "cameras",
        // About word fallbacks
        ["Veterinario"] = "Veterinarian",
        ["Veterinaria"] = "Veterinary",
        ["local"] = "local",
        ["licenciado"] = "licensed",
        ["licenciada"] = "licensed",
        ["Orientación"] = "Guidance",
        ["internacional"] = "international",
        ["Clínica"] = "Clinic",
        ["urgencias"] = "emergencies",
        ["veterinarias"] = "veterinary",
        ["Especialista"] = "Specialist",
        ["comportamiento"] = "behavior",
        ["canino"] = "canine",
        ["canina"] = "canine",
        ["felina"] = "feline",
        ["Plan"] = "Plan",
        ["educativo"] = "educational",
        ["diagnostica"] = "diagnose",
        ["medica"] = "prescribe",
        ["consultas"] = "checkups",
        ["vacunas"] = "vaccines",
        ["belleza"] = "grooming",
        ["Hospitalización"] = "Hospitalization",
        ["cuidado"] = "care",
        ["crítico"] = "critical",
        ["critico"] = "critical",
        ["Peluquería"] = "Grooming",
        ["Baños"] = "Baths",
        ["medicados"] = "medicated",
        ["estética"] = "styling",
        ["estetica"] = "styling",
        ["nocturno"] = "overnight",
        ["tipo"] = "style",
        ["hotel"] = "hotel",
        ["mascotas"] = "pets",
        ["diurna"] = "daytime",
        ["recreación"] = "recreation",
        ["recreacion"] = "recreation",
        ["supervisión"] = "supervision",
        ["supervision"] = "supervision",
        ["Paseos"] = "Walks",
        ["profesionales"] = "professional",
        ["parques"] = "parks",
        ["zonas"] = "areas",
        ["Entrenamiento"] = "Training",
        ["modificación"] = "modification",
        ["modificacion"] = "modification",
        ["conducta"] = "behavior",
        ["adiestramiento"] = "training",
        ["atención"] = "care",
        ["atencion"] = "care",
        ["atenciòn"] = "care",
        ["mejor"] = "best",
        ["centro"] = "center",
        ["Negocio"] = "Business",
        ["prueba"] = "test",
        ["validar"] = "validate",
        ["alta"] = "signup",
        ["Experiencia"] = "Experience",
        ["Enfoque"] = "Focus",
        ["nutrición"] = "nutrition",
        ["nutricion"] = "nutrition",
        ["razas"] = "breeds",
        ["pequeñas"] = "small",
        ["pequenas"] = "small",
        ["grandes"] = "large",
    };

    private static readonly (string Es, string En)[] NotePrefixes =
    [
        ("Horario:", "Schedule:"),
        ("Pago:", "Payment:"),
        ("Tipo:", "Type:"),
        ("Lugar:", "Place:"),
    ];

    public static string Text(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? "";
        if (!IsEnglish()) return text;
        var key = text.Trim();
        if (Map.TryGetValue(key, out var en)) return en;

        // Same phrase without trailing sentence punctuation
        var trimmedEnd = key.TrimEnd('.', '!', '?', '…');
        if (trimmedEnd.Length > 0 && trimmedEnd.Length < key.Length
            && Map.TryGetValue(trimmedEnd, out en))
            return en + key[trimmedEnd.Length..];

        // Multi-sentence About / catalog blurbs: localize each sentence
        if (key.Contains('.') || key.Contains('!') || key.Contains('?'))
        {
            var sentences = SplitSentences(key);
            if (sentences.Count > 1)
            {
                var localized = new List<string>(sentences.Count);
                var any = false;
                foreach (var s in sentences)
                {
                    var piece = s.Trim();
                    if (piece.Length == 0) { localized.Add(s); continue; }
                    var body = piece.TrimEnd('.', '!', '?', '…');
                    var punct = piece[body.Length..];
                    if (Map.TryGetValue(piece, out var full)
                        || Map.TryGetValue(body, out full)
                        || Map.TryGetValue(body + ".", out full))
                    {
                        // Prefer mapped value; keep original trailing punct if map had none
                        if (full.Length > 0 && ".!?…".IndexOf(full[^1]) >= 0)
                            localized.Add(full);
                        else
                            localized.Add(full + (punct.Length > 0 ? punct : "."));
                        any = true;
                    }
                    else
                    {
                        localized.Add(TranslateWords(piece));
                        if (!string.Equals(localized[^1], piece, StringComparison.Ordinal))
                            any = true;
                    }
                }
                if (any) return string.Join(" ", localized).Replace("  ", " ").Trim();
            }
        }

        // Long About blurbs: prefer full map only — avoid mixed ES/EN from partial word swaps
        if (key.Length > 50 && key.Count(c => c == ' ') >= 5)
            return key;

        return TranslateWords(key);
    }

    private static List<string> SplitSentences(string text)
    {
        var list = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is not ('.' or '!' or '?' or '…')) continue;
            // Keep decimal-like tokens together (rare in About)
            if (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])) continue;
            list.Add(text[start..(i + 1)]);
            start = i + 1;
            while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
            i = start - 1;
        }
        if (start < text.Length) list.Add(text[start..]);
        return list;
    }

    private static string TranslateWords(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.None);
        var changed = false;
        for (var i = 0; i < parts.Length; i++)
        {
            var raw = parts[i];
            if (raw.Length == 0) continue;

            // Keep trailing punctuation (e.g. "cámaras." / "24/7.")
            var core = raw.TrimEnd('.', ',', ';', ':', '!', '?', '…');
            var suffix = raw[core.Length..];
            if (core.Length == 0) continue;
            if (!Words.TryGetValue(core, out var en)) continue;
            parts[i] = MatchCase(core, en) + suffix;
            changed = true;
        }
        return changed ? string.Join(' ', parts) : text;
    }

    private static string MatchCase(string original, string replacement)
    {
        if (original.Length == 0 || replacement.Length == 0) return replacement;
        if (char.IsUpper(original[0]))
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];
        return char.ToLowerInvariant(replacement[0]) + replacement[1..];
    }

    /// <summary>Localiza notas de cita compuestas (p. ej. "Horario: Medio día · Pago: Visa").</summary>
    public static string Notes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return notes ?? "";
        // Pet is shown separately on Confirm/Details — drop redundant "Mascotas:/Pets:" segments.
        notes = StripPetRosterSegments(notes);
        if (string.IsNullOrWhiteSpace(notes)) return "";
        if (!IsEnglish()) return notes;

        var parts = notes.Split(" · ", StringSplitOptions.None);
        for (var i = 0; i < parts.Length; i++)
            parts[i] = LocalizeNotePart(parts[i]);
        return string.Join(" · ", parts);
    }

    /// <summary>True when notes still have customer-facing content after dropping pet roster prefixes.</summary>
    public static bool HasCustomerNotes(string? notes) =>
        !string.IsNullOrWhiteSpace(StripPetRosterSegments(notes));

    /// <summary>
    /// Multi-pet roster stored in Notes ("Mascotas: …" / "Pets: …"), without the prefix.
    /// Null when the booking is a single pet (or roster missing).
    /// </summary>
    public static string? PetRosterText(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        foreach (var raw in notes.Split(" · ", StringSplitOptions.None))
        {
            var p = raw.Trim();
            if (p.StartsWith("Mascotas:", StringComparison.OrdinalIgnoreCase))
            {
                var body = p["Mascotas:".Length..].Trim();
                return string.IsNullOrWhiteSpace(body) ? null : body;
            }
            if (p.StartsWith("Pets:", StringComparison.OrdinalIgnoreCase))
            {
                var body = p["Pets:".Length..].Trim();
                return string.IsNullOrWhiteSpace(body) ? null : body;
            }
        }
        return null;
    }

    /// <summary>Label + value for Confirm/Details pet line (single or multi from Notes roster).</summary>
    public static (string Label, string Value) PetsLine(Pet primary, string? notes)
    {
        var roster = PetRosterText(notes);
        if (!string.IsNullOrWhiteSpace(roster))
            return (Loc("Mascotas", "Pets"), roster);

        return (
            Loc("Mascota", "Pet"),
            $"{PetSpecies.Emoji(primary.Species)} {primary.Name} ({PetSpecies.Label(primary.Species)})");
    }

    private static string StripPetRosterSegments(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return "";
        var kept = notes.Split(" · ", StringSplitOptions.None)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0
                && !p.StartsWith("Mascotas:", StringComparison.OrdinalIgnoreCase)
                && !p.StartsWith("Pets:", StringComparison.OrdinalIgnoreCase));
        return string.Join(" · ", kept);
    }

    private static string LocalizeNotePart(string part)
    {
        var t = part.Trim();
        if (t.Length == 0) return t;
        if (Map.TryGetValue(t, out var full)) return full;

        foreach (var (es, en) in NotePrefixes)
        {
            if (!t.StartsWith(es, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = t[es.Length..].TrimStart();
            return string.IsNullOrEmpty(rest) ? en : $"{en} {Text(rest)}";
        }

        // "2 mascotas" / "Paseo 30 min"
        if (t.EndsWith(" mascotas", StringComparison.OrdinalIgnoreCase))
            return t[..^" mascotas".Length] + " pets";
        if (t.StartsWith("Paseo ", StringComparison.OrdinalIgnoreCase) && t.EndsWith(" min", StringComparison.OrdinalIgnoreCase))
            return "Walk " + t["Paseo ".Length..];
        if (t.Equals("1 sesión / semana", StringComparison.OrdinalIgnoreCase))
            return "1 session / week";

        return Text(t);
    }

    public static string Loc(string spanish, string english) =>
        IsEnglish() ? english : spanish;

    public static bool IsEnglish() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .StartsWith("en", StringComparison.OrdinalIgnoreCase);
}
