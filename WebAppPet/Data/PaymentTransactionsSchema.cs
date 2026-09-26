using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Data;

/// <summary>
/// Creates the PaymentTransactions table on databases that predate it and, only in that same run,
/// records the charges that happened before the table existed as "legacy" transactions.
/// </summary>
public static partial class PaymentTransactionsSchema
{
    public const string LegacyGateway = "legacy";

    public static async Task EnsureAsync(AppDbContext db)
    {
        if (!db.Database.IsSqlServer() && !db.Database.IsSqlite()) return;
        if (await TableExistsAsync(db)) return;

        // Table and backfill commit together; a failure leaves no table so the next start retries both.
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync(db.Database.IsSqlServer() ? SqlServerDdl : SqliteDdl);
            var backfilled = await BackfillLegacyChargesAsync(db);
            await transaction.CommitAsync();
            Console.WriteLine($"[DbInitializer] PaymentTransactions created; {backfilled} legacy charges recorded.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
            Console.Error.WriteLine($"[DbInitializer] PaymentTransactions setup failed: {ex.Message}");
        }
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db)
    {
        var sql = db.Database.IsSqlServer()
            ? "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = N'PaymentTransactions'"
            : "SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type = 'table' AND name = 'PaymentTransactions'";
        var counts = await db.Database.SqlQueryRaw<int>(sql).ToListAsync();
        return counts.FirstOrDefault() > 0;
    }

    /// <summary>
    /// Appointments stored their charge in DepositPaid (full price for vet and behavior bookings);
    /// cancelled ones count as refunded. Each Care subscription gets its first monthly charge.
    /// </summary>
    public static async Task<int> BackfillLegacyChargesAsync(AppDbContext db)
    {
        var consultationByAppointment = await db.Consultations.AsNoTracking()
            .Where(c => c.AppointmentId != null)
            .Select(c => new { c.Id, AppointmentId = c.AppointmentId!.Value })
            .ToListAsync();
        var consultations = consultationByAppointment
            .GroupBy(c => c.AppointmentId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.TotalPrice > 0 && a.DepositPaid > 0)
            .Select(a => new
            {
                a.Id, a.ClientId, a.GroomerId, a.Status, a.TotalPrice, a.DepositPaid, a.Notes, a.CreatedAt,
                ClientCountry = a.Client.CountryCode
            })
            .ToListAsync();

        var rows = new List<PaymentTransaction>();
        foreach (var a in appointments)
        {
            var tx = new PaymentTransaction
            {
                UserId = a.ClientId,
                ProviderId = a.GroomerId,
                AppointmentId = a.Id,
                Purpose = PaymentPurpose.BookingDeposit,
                Status = a.Status == AppointmentStatus.Cancelled
                    ? PaymentTransactionStatus.Refunded
                    : PaymentTransactionStatus.Succeeded,
                Amount = a.DepositPaid,
                ServiceTotal = a.TotalPrice,
                Currency = AppMoney.Code(a.ClientCountry),
                Gateway = LegacyGateway,
                ExternalReference = $"legacy_appt_{a.Id}",
                Description = "Charge recorded before payment transactions existed",
                CreatedAt = a.CreatedAt
            };

            if (consultations.TryGetValue(a.Id, out var consultationId))
            {
                tx.Purpose = PaymentPurpose.VetConsultation;
                tx.ConsultationId = consultationId;
            }
            else if (a.Notes is not null && BehaviorNote().Match(a.Notes) is { Success: true } m)
            {
                tx.Purpose = PaymentPurpose.BehaviorSession;
                tx.BehaviorCaseId = int.Parse(m.Groups[1].Value);
            }

            if (tx.Status == PaymentTransactionStatus.Refunded)
            {
                tx.RefundedAt = a.CreatedAt;
                tx.RefundReference = $"legacy_refund_{a.Id}";
                tx.RefundReason = "Appointment cancelled before payment transactions existed";
            }
            rows.Add(tx);
        }

        var subscriptions = await db.CareSubscriptions.AsNoTracking()
            .Where(s => s.PricePerMonth > 0)
            .Select(s => new { s.Id, s.UserId, s.PricePerMonth, s.StartedAt, Country = s.User.CountryCode })
            .ToListAsync();
        rows.AddRange(subscriptions.Select(s => new PaymentTransaction
        {
            UserId = s.UserId,
            CareSubscriptionId = s.Id,
            Purpose = PaymentPurpose.CareSubscription,
            Status = PaymentTransactionStatus.Succeeded,
            Amount = s.PricePerMonth,
            ServiceTotal = s.PricePerMonth,
            Currency = AppMoney.Code(s.Country),
            Gateway = LegacyGateway,
            ExternalReference = $"legacy_care_{s.Id}",
            Description = "Charge recorded before payment transactions existed",
            CreatedAt = s.StartedAt
        }));

        db.PaymentTransactions.AddRange(rows);
        await db.SaveChangesAsync();
        return rows.Count;
    }

    [GeneratedRegex(@"^Behavior case #(\d+)")]
    private static partial Regex BehaviorNote();

    private const string SqlServerDdl = """
        CREATE TABLE [PaymentTransactions] (
            [Id] int NOT NULL IDENTITY,
            [UserId] int NOT NULL,
            [ProviderId] int NULL,
            [AppointmentId] int NULL,
            [ConsultationId] int NULL,
            [BehaviorCaseId] int NULL,
            [CareSubscriptionId] int NULL,
            [Purpose] int NOT NULL,
            [Status] int NOT NULL,
            [Amount] decimal(12,2) NOT NULL,
            [ServiceTotal] decimal(12,2) NOT NULL,
            [Currency] nvarchar(8) NOT NULL,
            [Gateway] nvarchar(30) NOT NULL,
            [ExternalReference] nvarchar(80) NOT NULL,
            [PaymentMethodId] int NULL,
            [CardBrand] nvarchar(20) NULL,
            [CardLast4] nvarchar(4) NULL,
            [FailureCode] nvarchar(40) NULL,
            [Description] nvarchar(200) NULL,
            [CreatedAt] datetime2 NOT NULL,
            [RefundedAt] datetime2 NULL,
            [RefundReference] nvarchar(80) NULL,
            [RefundReason] nvarchar(200) NULL,
            CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([Id])
        );
        CREATE INDEX [IX_PaymentTransactions_ProviderId_Status_CreatedAt] ON [PaymentTransactions] ([ProviderId], [Status], [CreatedAt]);
        CREATE INDEX [IX_PaymentTransactions_AppointmentId] ON [PaymentTransactions] ([AppointmentId]);
        CREATE INDEX [IX_PaymentTransactions_UserId] ON [PaymentTransactions] ([UserId]);
        """;

    private const string SqliteDdl = """
        CREATE TABLE "PaymentTransactions" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_PaymentTransactions" PRIMARY KEY AUTOINCREMENT,
            "UserId" INTEGER NOT NULL,
            "ProviderId" INTEGER NULL,
            "AppointmentId" INTEGER NULL,
            "ConsultationId" INTEGER NULL,
            "BehaviorCaseId" INTEGER NULL,
            "CareSubscriptionId" INTEGER NULL,
            "Purpose" INTEGER NOT NULL,
            "Status" INTEGER NOT NULL,
            "Amount" TEXT NOT NULL,
            "ServiceTotal" TEXT NOT NULL,
            "Currency" TEXT NOT NULL,
            "Gateway" TEXT NOT NULL,
            "ExternalReference" TEXT NOT NULL,
            "PaymentMethodId" INTEGER NULL,
            "CardBrand" TEXT NULL,
            "CardLast4" TEXT NULL,
            "FailureCode" TEXT NULL,
            "Description" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "RefundedAt" TEXT NULL,
            "RefundReference" TEXT NULL,
            "RefundReason" TEXT NULL
        );
        CREATE INDEX "IX_PaymentTransactions_ProviderId_Status_CreatedAt" ON "PaymentTransactions" ("ProviderId", "Status", "CreatedAt");
        CREATE INDEX "IX_PaymentTransactions_AppointmentId" ON "PaymentTransactions" ("AppointmentId");
        CREATE INDEX "IX_PaymentTransactions_UserId" ON "PaymentTransactions" ("UserId");
        """;
}
