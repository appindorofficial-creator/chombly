using System.Globalization;

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
        ["Baño básico"] = "Basic bath",
        ["Baño adicional"] = "Extra bath",
        ["Baño al final del día"] = "End-of-day bath",
        ["Grooming completo"] = "Full grooming",
        ["Grooming para gatos"] = "Cat grooming",
        ["Corte"] = "Haircut",
        ["Corte de pelo"] = "Haircut",
        ["Deshedding"] = "Deshedding",
        ["Consulta general"] = "General checkup",
        ["Chequeo general"] = "General checkup",
        ["Vacunas"] = "Vaccines",
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
        ["Mañana 9:00 AM"] = "Tomorrow 9:00 AM",
        ["Tarde 1:00 PM"] = "Afternoon 1:00 PM",
        ["Noche 6:00 PM"] = "Evening 6:00 PM",
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
        ["Paquete 4 sesiones"] = "4-session pack",
        ["Paquete 8 sesiones"] = "8-session pack",
        ["Recomendado"] = "Recommended",
        ["Consulta"] = "Checkup",
        ["Hoy"] = "Today",
        ["Mañana"] = "Tomorrow",
        ["Elegir fechas"] = "Pick dates",

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
    };

    public static string Text(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? "";
        if (!IsEnglish()) return text;
        var key = text.Trim();
        return Map.TryGetValue(key, out var en) ? en : text;
    }

    public static string Loc(string spanish, string english) =>
        IsEnglish() ? english : spanish;

    public static bool IsEnglish() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .StartsWith("en", StringComparison.OrdinalIgnoreCase);
}
