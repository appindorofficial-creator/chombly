using WebAppPet.Models;

namespace WebAppPet.Services;

/// <summary>
/// Maps in-app notification types to a destination the user can open from the list.
/// </summary>
public static class NotificationDeepLink
{
    public static string? Resolve(string? type, bool businessShell, bool isAdmin = false)
    {
        var t = (type ?? "").Trim().ToLowerInvariant();

        if (t.StartsWith("reminder-", StringComparison.Ordinal))
            return businessShell ? "/Groomer/Dashboard" : "/Pets";

        return t switch
        {
            "appointment" => businessShell ? "/Groomer/Appointments" : "/Appointments",
            "business" => isAdmin ? "/Admin/Approvals"
                : businessShell ? "/Groomer/Dashboard"
                : "/Account/Profile",
            "professional" => "/Professional/Onboarding/Status",
            "chat" => "/Chat/Inbox",
            "promo" => "/Pets",
            "vet-consultation" or "vet-followup" => businessShell ? "/Groomer/Appointments" : "/Appointments",
            "behavior-plan" => businessShell ? "/Groomer/Appointments" : "/Pets",
            "payout" or "payment" => "/Professional/Payouts",
            "info" => businessShell ? "/Groomer/Dashboard" : "/Account/Profile",
            _ => businessShell ? "/Groomer/Dashboard" : "/Account/Profile"
        };
    }

    public static string? Resolve(AppNotification n, bool businessShell, bool isAdmin = false) =>
        Resolve(n.Type, businessShell, isAdmin);
}
