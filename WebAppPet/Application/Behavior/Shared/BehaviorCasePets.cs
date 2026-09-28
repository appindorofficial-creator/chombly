using WebAppPet.Domain;


namespace WebAppPet.Application.Behavior.Shared;

/// <summary>
/// A case covers one or more dogs: the first in <see cref="BehaviorCase.PetId"/> and the rest,
/// comma-separated, in <see cref="BehaviorCase.ExtraPetIds"/>.
/// </summary>
public static class BehaviorCasePets
{
    public static List<int> SelectedIds(BehaviorCase behaviorCase)
    {
        var ids = new List<int>();
        if (behaviorCase.PetId is int primary && primary > 0)
            ids.Add(primary);

        if (!string.IsNullOrWhiteSpace(behaviorCase.ExtraPetIds))
        {
            foreach (var part in behaviorCase.ExtraPetIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var id) && id > 0 && !ids.Contains(id))
                    ids.Add(id);
            }
        }

        return ids;
    }

    public static void SetSelectedIds(BehaviorCase behaviorCase, IEnumerable<int> petIds)
    {
        var ids = petIds.Where(id => id > 0).Distinct().ToList();
        behaviorCase.PetId = ids.Count > 0 ? ids[0] : null;
        behaviorCase.ExtraPetIds = ids.Count > 1
            ? string.Join(",", ids.Skip(1))
            : null;
    }
}
