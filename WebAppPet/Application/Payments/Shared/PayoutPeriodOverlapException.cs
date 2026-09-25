namespace WebAppPet.Application.Payments.Shared;

public class PayoutPeriodOverlapException : InvalidOperationException
{
    public PayoutPeriodOverlapException()
        : base("A payout already exists for an overlapping period.")
    {
    }
}
