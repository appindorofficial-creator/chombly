namespace WebAppPet.Models;

public enum UserRole
{
    Client = 0,
    Groomer = 1,
    Admin = 2
}

public enum GroomerType
{
    Salon = 0,
    Mobile = 1,
    InHome = 2
}

public enum PetSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
    Giant = 3
}

public enum AppointmentStatus
{
    Pending = 0,
    Confirmed = 1,
    Completed = 2,
    Cancelled = 3
}

/// <summary>Estado de publicación del negocio en el marketplace.</summary>
public enum BusinessPublishStatus
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>Negocio con local vs proveedor independiente (walker, sitter…).</summary>
public enum ProviderKind
{
    Business = 0,
    Independent = 1
}

/// <summary>Cómo presta el servicio.</summary>
public enum WorkMode
{
    Local = 0,
    Mobile = 1,
    Both = 2
}
