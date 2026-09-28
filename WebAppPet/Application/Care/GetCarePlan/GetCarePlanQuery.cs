namespace WebAppPet.Application.Care.GetCarePlan;

/// <param name="UserId">Null for guests, who only see the plan and its price.</param>
public sealed record GetCarePlanQuery(int? UserId);
