using Microsoft.EntityFrameworkCore;
using WebAppPet.Models;

namespace WebAppPet.Data;

/// <summary>
/// Asegura esquema y catálogo de categorías. No inserta negocios, usuarios ni citas de demo.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        // T-SQL ALTERs are SQL Server only (Azure / LocalDB upgrades).
        if (db.Database.IsSqlServer())
        {
            await EnsureMarketplaceSchemaAsync(db);
            await EnsureVetEcosystemSchemaAsync(db);
            await BackfillAcceptedSpeciesAsync(db);
        }

        await EnsureCategoriesAsync(db);
        await EnsureAdminAsync(db);
        await EnsureVetEcosystemSeedAsync(db);
        await EnsureCountryCatalogSeedAsync(db);
        await EnsureCompensationDefaultsAsync(db);
    }

    private static async Task EnsureCategoriesAsync(AppDbContext db)
    {
        var catalog = new (string Slug, string Name, string NameEn, string Subtitle, string SubtitleEn, string Icon, string Emoji, bool Overnight, int Order)[]
        {
            ("grooming", "Peluquería", "Grooming", "Baño, corte y estética", "Bath, cut & styling", "/images/categories/cat-grooming.png", "✂️", false, 1),
            ("hotel", "Hotel", "Hotel", "Hospedaje para tu mascota", "Overnight pet boarding", "/images/categories/cat-hotel.png", "🏨", true, 2),
            ("vet", "Veterinario", "Veterinary", "Consultas y cuidados", "Checkups & care", "/images/categories/cat-vet.png", "🩺", false, 3),
            ("daycare", "Guardería", "Daycare", "Guardería diurna", "Daytime pet care", "/images/categories/cat-daycare.png", "☀️", true, 4),
            ("walkers", "Paseadores", "Walkers", "Paseos profesionales", "Professional dog walks", "/images/categories/cat-walkers.png", "🦮", false, 5),
            ("trainers", "Entrenadores", "Trainers", "Entrenamiento", "Training sessions", "/images/categories/cat-trainers.png", "🎓", false, 6)
        };

        foreach (var item in catalog)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.Slug == item.Slug);
            if (existing == null)
            {
                db.Categories.Add(new ServiceCategory
                {
                    Slug = item.Slug,
                    Name = item.Name,
                    NameEn = item.NameEn,
                    Subtitle = item.Subtitle,
                    SubtitleEn = item.SubtitleEn,
                    Icon = item.Icon,
                    Emoji = item.Emoji,
                    IsOvernight = item.Overnight,
                    SortOrder = item.Order,
                    IsActive = true
                });
            }
            else
            {
                existing.Name = item.Name;
                existing.NameEn = item.NameEn;
                existing.Subtitle = item.Subtitle;
                existing.SubtitleEn = item.SubtitleEn;
                existing.Icon = item.Icon;
                existing.Emoji = item.Emoji;
                existing.IsOvernight = item.Overnight;
                existing.SortOrder = item.Order;
                existing.IsActive = true;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureMarketplaceSchemaAsync(AppDbContext db)
    {
        // Columnas nuevas en tablas existentes (Azure / LocalDB ya creadas)
        var alters = new[]
        {
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'AcceptedSpecies') IS NULL
                ALTER TABLE [Groomers] ADD [AcceptedSpecies] nvarchar(200) NULL;
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'CategoryId') IS NULL
                ALTER TABLE [Groomers] ADD [CategoryId] int NULL;
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'IsActive') IS NULL
                ALTER TABLE [Groomers] ADD [IsActive] bit NOT NULL CONSTRAINT DF_Groomers_IsActive DEFAULT(1);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'PriceUnit') IS NULL
                ALTER TABLE [Groomers] ADD [PriceUnit] nvarchar(40) NULL;
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'PriceUnit') IS NOT NULL
                UPDATE [Groomers] SET [PriceUnit] = N'' WHERE [PriceUnit] IS NULL;
            """,
            """
            IF OBJECT_ID(N'[Services]', N'U') IS NOT NULL AND COL_LENGTH(N'Services', N'BillingUnit') IS NULL
                ALTER TABLE [Services] ADD [BillingUnit] nvarchar(20) NULL;
            """,
            """
            IF OBJECT_ID(N'[Appointments]', N'U') IS NOT NULL AND COL_LENGTH(N'Appointments', N'EndAt') IS NULL
                ALTER TABLE [Appointments] ADD [EndAt] datetime2 NULL;
            """,
            """
            IF OBJECT_ID(N'[Appointments]', N'U') IS NOT NULL AND COL_LENGTH(N'Appointments', N'Nights') IS NULL
                ALTER TABLE [Appointments] ADD [Nights] int NOT NULL CONSTRAINT DF_Appointments_Nights DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL AND COL_LENGTH(N'Users', N'Latitude') IS NULL
                ALTER TABLE [Users] ADD [Latitude] float NULL;
            """,
            """
            IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL AND COL_LENGTH(N'Users', N'Longitude') IS NULL
                ALTER TABLE [Users] ADD [Longitude] float NULL;
            """,
            """
            IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL AND COL_LENGTH(N'Users', N'LocationUpdatedAt') IS NULL
                ALTER TABLE [Users] ADD [LocationUpdatedAt] datetime2 NULL;
            """,
            """
            IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL AND COL_LENGTH(N'Users', N'PreferredLanguage') IS NULL
                ALTER TABLE [Users] ADD [PreferredLanguage] nvarchar(10) NOT NULL CONSTRAINT DF_Users_PreferredLanguage DEFAULT(N'es');
            """,
            """
            IF OBJECT_ID(N'[Categories]', N'U') IS NOT NULL AND COL_LENGTH(N'Categories', N'NameEn') IS NULL
                ALTER TABLE [Categories] ADD [NameEn] nvarchar(80) NOT NULL CONSTRAINT DF_Categories_NameEn DEFAULT(N'');
            """,
            """
            IF OBJECT_ID(N'[Categories]', N'U') IS NOT NULL AND COL_LENGTH(N'Categories', N'SubtitleEn') IS NULL
                ALTER TABLE [Categories] ADD [SubtitleEn] nvarchar(160) NOT NULL CONSTRAINT DF_Categories_SubtitleEn DEFAULT(N'');
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'PublishStatus') IS NULL
                ALTER TABLE [Groomers] ADD [PublishStatus] int NOT NULL CONSTRAINT DF_Groomers_PublishStatus DEFAULT(2);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'ProviderKind') IS NULL
                ALTER TABLE [Groomers] ADD [ProviderKind] int NOT NULL CONSTRAINT DF_Groomers_ProviderKind DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'WorkMode') IS NULL
                ALTER TABLE [Groomers] ADD [WorkMode] int NOT NULL CONSTRAINT DF_Groomers_WorkMode DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'ServiceAreaMiles') IS NULL
                ALTER TABLE [Groomers] ADD [ServiceAreaMiles] int NOT NULL CONSTRAINT DF_Groomers_ServiceAreaMiles DEFAULT(10);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'LogoUrl') IS NULL
                ALTER TABLE [Groomers] ADD [LogoUrl] nvarchar(260) NULL;
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'CoverUrl') IS NULL
                ALTER TABLE [Groomers] ADD [CoverUrl] nvarchar(260) NULL;
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'VerifiedIdentity') IS NULL
                ALTER TABLE [Groomers] ADD [VerifiedIdentity] bit NOT NULL CONSTRAINT DF_Groomers_VerifiedIdentity DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'VerifiedLicense') IS NULL
                ALTER TABLE [Groomers] ADD [VerifiedLicense] bit NOT NULL CONSTRAINT DF_Groomers_VerifiedLicense DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'VerifiedInsurance') IS NULL
                ALTER TABLE [Groomers] ADD [VerifiedInsurance] bit NOT NULL CONSTRAINT DF_Groomers_VerifiedInsurance DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'VerifiedBank') IS NULL
                ALTER TABLE [Groomers] ADD [VerifiedBank] bit NOT NULL CONSTRAINT DF_Groomers_VerifiedBank DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'SupportCallAt') IS NULL
                ALTER TABLE [Groomers] ADD [SupportCallAt] datetime2 NULL;
            """,
            """
            IF OBJECT_ID(N'[Appointments]', N'U') IS NOT NULL AND COL_LENGTH(N'Appointments', N'PromoCode') IS NULL
                ALTER TABLE [Appointments] ADD [PromoCode] nvarchar(40) NULL;
            """,
            """
            IF OBJECT_ID(N'[Appointments]', N'U') IS NOT NULL AND COL_LENGTH(N'Appointments', N'DiscountAmount') IS NULL
                ALTER TABLE [Appointments] ADD [DiscountAmount] decimal(10,2) NOT NULL CONSTRAINT DF_Appointments_DiscountAmount DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'VetProviderKind') IS NULL
                ALTER TABLE [Groomers] ADD [VetProviderKind] int NOT NULL CONSTRAINT DF_Groomers_VetProviderKind DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'LicenseCountry') IS NULL
                ALTER TABLE [Groomers] ADD [LicenseCountry] nvarchar(8) NULL;
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'SpokenLanguages') IS NULL
                ALTER TABLE [Groomers] ADD [SpokenLanguages] nvarchar(120) NULL;
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'OffersEmergency24x7') IS NULL
                ALTER TABLE [Groomers] ADD [OffersEmergency24x7] bit NOT NULL CONSTRAINT DF_Groomers_OffersEmergency24x7 DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'BehaviorRole') IS NULL
                ALTER TABLE [Groomers] ADD [BehaviorRole] int NULL;
            """,
            """
            IF OBJECT_ID(N'[Pets]', N'U') IS NOT NULL AND COL_LENGTH(N'Pets', N'WeightLbs') IS NULL
                ALTER TABLE [Pets] ADD [WeightLbs] decimal(8,2) NULL;
            """
        };

        foreach (var sql in alters)
        {
            try { await db.Database.ExecuteSqlRawAsync(sql); } catch { /* ignore */ }
        }

        // Tablas nuevas si EnsureCreated no las creó (DB ya existente)
        var creates = new[]
        {
            """
            IF OBJECT_ID(N'[Categories]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Categories] (
                    [Id] int NOT NULL IDENTITY,
                    [Slug] nvarchar(40) NOT NULL,
                    [Name] nvarchar(80) NOT NULL,
                    [NameEn] nvarchar(80) NOT NULL CONSTRAINT DF_Categories_NameEn_New DEFAULT(N''),
                    [Subtitle] nvarchar(160) NOT NULL,
                    [SubtitleEn] nvarchar(160) NOT NULL CONSTRAINT DF_Categories_SubtitleEn_New DEFAULT(N''),
                    [Icon] nvarchar(120) NOT NULL,
                    [Emoji] nvarchar(40) NOT NULL,
                    [IsOvernight] bit NOT NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
            END
            """,
            """
            IF OBJECT_ID(N'[Amenities]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Amenities] (
                    [Id] int NOT NULL IDENTITY,
                    [GroomerId] int NOT NULL,
                    [Label] nvarchar(80) NOT NULL,
                    [Icon] nvarchar(40) NOT NULL,
                    [SortOrder] int NOT NULL,
                    CONSTRAINT [PK_Amenities] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_Amenities_Groomers] FOREIGN KEY ([GroomerId]) REFERENCES [Groomers] ([Id]) ON DELETE CASCADE
                );
            END
            """,
            """
            IF OBJECT_ID(N'[ServiceExtras]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ServiceExtras] (
                    [Id] int NOT NULL IDENTITY,
                    [GroomerId] int NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    [Description] nvarchar(200) NULL,
                    [Price] decimal(10,2) NOT NULL,
                    [IsActive] bit NOT NULL,
                    CONSTRAINT [PK_ServiceExtras] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ServiceExtras_Groomers] FOREIGN KEY ([GroomerId]) REFERENCES [Groomers] ([Id]) ON DELETE CASCADE
                );
            END
            """,
            """
            IF OBJECT_ID(N'[AppointmentExtras]', N'U') IS NULL
            BEGIN
                CREATE TABLE [AppointmentExtras] (
                    [Id] int NOT NULL IDENTITY,
                    [AppointmentId] int NOT NULL,
                    [ServiceExtraId] int NOT NULL,
                    [Name] nvarchar(max) NOT NULL,
                    [Price] decimal(10,2) NOT NULL,
                    CONSTRAINT [PK_AppointmentExtras] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_AppointmentExtras_Appointments] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_AppointmentExtras_ServiceExtras] FOREIGN KEY ([ServiceExtraId]) REFERENCES [ServiceExtras] ([Id]) ON DELETE NO ACTION
                );
            END
            """,
            """
            IF OBJECT_ID(N'[Conversations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Conversations] (
                    [Id] int NOT NULL IDENTITY,
                    [ClientId] int NOT NULL,
                    [GroomerId] int NOT NULL,
                    [AppointmentId] int NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [LastMessageAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_Conversations_Users] FOREIGN KEY ([ClientId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_Conversations_Groomers] FOREIGN KEY ([GroomerId]) REFERENCES [Groomers] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_Conversations_Appointments] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]) ON DELETE SET NULL
                );
            END
            """,
            """
            IF OBJECT_ID(N'[ChatMessages]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ChatMessages] (
                    [Id] int NOT NULL IDENTITY,
                    [ConversationId] int NOT NULL,
                    [SenderUserId] int NOT NULL,
                    [Body] nvarchar(2000) NOT NULL,
                    [SentAt] datetime2 NOT NULL,
                    [IsRead] bit NOT NULL,
                    CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ChatMessages_Conversations] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_ChatMessages_Users] FOREIGN KEY ([SenderUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                );
            END
            """,
            """
            IF OBJECT_ID(N'[DayAvailabilities]', N'U') IS NULL
            BEGIN
                CREATE TABLE [DayAvailabilities] (
                    [Id] int NOT NULL IDENTITY,
                    [GroomerId] int NOT NULL,
                    [Day] datetime2 NOT NULL,
                    [IsAvailable] bit NOT NULL,
                    [Note] nvarchar(120) NULL,
                    CONSTRAINT [PK_DayAvailabilities] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_DayAvailabilities_Groomers] FOREIGN KEY ([GroomerId]) REFERENCES [Groomers] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_DayAvailabilities_GroomerId_Day] ON [DayAvailabilities] ([GroomerId], [Day]);
            END
            """,
            """
            IF OBJECT_ID(N'[WeeklyHours]', N'U') IS NULL
            BEGIN
                CREATE TABLE [WeeklyHours] (
                    [Id] int NOT NULL IDENTITY,
                    [GroomerId] int NOT NULL,
                    [DayOfWeek] int NOT NULL,
                    [IsOpen] bit NOT NULL,
                    [OpenMinutes] int NOT NULL,
                    [CloseMinutes] int NOT NULL,
                    CONSTRAINT [PK_WeeklyHours] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_WeeklyHours_Groomers] FOREIGN KEY ([GroomerId]) REFERENCES [Groomers] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_WeeklyHours_GroomerId_DayOfWeek] ON [WeeklyHours] ([GroomerId], [DayOfWeek]);
            END
            """
        };

        foreach (var sql in creates)
        {
            try { await db.Database.ExecuteSqlRawAsync(sql); } catch { /* ignore */ }
        }

        // Ampliar Icon si la columna es corta
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'Categories', N'Icon') IS NOT NULL AND COL_LENGTH(N'Categories', N'Icon') < 120
                    ALTER TABLE [Categories] ALTER COLUMN [Icon] nvarchar(120) NOT NULL;
                """);
        }
        catch { /* ignore */ }

        // FK CategoryId si falta
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[Categories]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Groomers_Categories')
                   AND COL_LENGTH(N'Groomers', N'CategoryId') IS NOT NULL
                BEGIN
                    ALTER TABLE [Groomers] WITH NOCHECK
                    ADD CONSTRAINT [FK_Groomers_Categories]
                    FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE SET NULL;
                END
                """);
        }
        catch { /* ignore */ }
    }

    private static async Task EnsureVetEcosystemSchemaAsync(AppDbContext db)
    {
        var creates = new[]
        {
            """
            IF OBJECT_ID(N'[ServiceCatalog]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ServiceCatalog] (
                    [Id] int NOT NULL IDENTITY,
                    [Code] nvarchar(40) NOT NULL,
                    [NameEs] nvarchar(120) NOT NULL,
                    [NameEn] nvarchar(120) NOT NULL,
                    [ScopeEs] nvarchar(400) NOT NULL,
                    [ScopeEn] nvarchar(400) NOT NULL,
                    [Price] decimal(10,2) NOT NULL,
                    [Currency] nvarchar(8) NOT NULL,
                    [DurationMinutes] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [IsBookable] bit NOT NULL,
                    [EffectiveFrom] datetime2 NOT NULL,
                    CONSTRAINT [PK_ServiceCatalog] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_ServiceCatalog_Code] ON [ServiceCatalog] ([Code]);
            END
            """,
            """
            IF OBJECT_ID(N'[ProviderLicenses]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ProviderLicenses] (
                    [Id] int NOT NULL IDENTITY,
                    [GroomerId] int NOT NULL,
                    [Jurisdiction] nvarchar(8) NOT NULL,
                    [LicenseNumber] nvarchar(80) NOT NULL,
                    [ExpiresAt] datetime2 NULL,
                    [IsVerified] bit NOT NULL,
                    [IsUsState] bit NOT NULL,
                    CONSTRAINT [PK_ProviderLicenses] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ProviderLicenses_Groomers] FOREIGN KEY ([GroomerId]) REFERENCES [Groomers] ([Id]) ON DELETE CASCADE
                );
            END
            """,
            """
            IF OBJECT_ID(N'[VcprRecords]', N'U') IS NULL
            BEGIN
                CREATE TABLE [VcprRecords] (
                    [Id] int NOT NULL IDENTITY,
                    [PetId] int NOT NULL,
                    [ProviderId] int NOT NULL,
                    [UsState] nvarchar(8) NOT NULL,
                    [ExamDate] datetime2 NOT NULL,
                    [EvidenceNote] nvarchar(400) NULL,
                    [EvidenceUrl] nvarchar(260) NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_VcprRecords] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_VcprRecords_Pets] FOREIGN KEY ([PetId]) REFERENCES [Pets] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_VcprRecords_Groomers] FOREIGN KEY ([ProviderId]) REFERENCES [Groomers] ([Id]) ON DELETE NO ACTION
                );
            END
            """,
            """
            IF OBJECT_ID(N'[Consultations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Consultations] (
                    [Id] int NOT NULL IDENTITY,
                    [ClientId] int NOT NULL,
                    [PetId] int NULL,
                    [ProviderId] int NULL,
                    [AppointmentId] int NULL,
                    [Modality] int NOT NULL,
                    [Status] int NOT NULL,
                    [PetUsState] nvarchar(8) NOT NULL,
                    [ContextCountry] nvarchar(8) NULL,
                    [ServiceCatalogCode] nvarchar(40) NULL,
                    [Symptoms] nvarchar(500) NULL,
                    [SafetyAnswersJson] nvarchar(1000) NULL,
                    [HasRedFlags] bit NOT NULL,
                    [HasActiveVcpr] bit NOT NULL,
                    [MediaUrl1] nvarchar(260) NULL,
                    [MediaUrl2] nvarchar(260) NULL,
                    [ScheduledAt] datetime2 NULL,
                    [ClinicalNotes] nvarchar(800) NULL,
                    [ResponsibleName] nvarchar(200) NULL,
                    [PriceCharged] decimal(10,2) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_Consultations] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_Consultations_Users] FOREIGN KEY ([ClientId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_Consultations_Pets] FOREIGN KEY ([PetId]) REFERENCES [Pets] ([Id]) ON DELETE SET NULL,
                    CONSTRAINT [FK_Consultations_Providers] FOREIGN KEY ([ProviderId]) REFERENCES [Groomers] ([Id]) ON DELETE SET NULL,
                    CONSTRAINT [FK_Consultations_Appointments] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]) ON DELETE SET NULL
                );
            END
            """,
            """
            IF OBJECT_ID(N'[ConsentRecords]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ConsentRecords] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [ConsultationId] int NULL,
                    [DocumentKey] nvarchar(80) NOT NULL,
                    [DocumentVersion] nvarchar(20) NOT NULL,
                    [Accepted] bit NOT NULL,
                    [AcceptedAt] datetime2 NOT NULL,
                    [IpAddress] nvarchar(64) NULL,
                    [UserAgent] nvarchar(260) NULL,
                    CONSTRAINT [PK_ConsentRecords] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ConsentRecords_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_ConsentRecords_Consultations] FOREIGN KEY ([ConsultationId]) REFERENCES [Consultations] ([Id]) ON DELETE SET NULL
                );
            END
            """,
            """
            IF OBJECT_ID(N'[AuditLogs]', N'U') IS NULL
            BEGIN
                CREATE TABLE [AuditLogs] (
                    [Id] int NOT NULL IDENTITY,
                    [ActorUserId] int NULL,
                    [Action] nvarchar(80) NOT NULL,
                    [EntityType] nvarchar(80) NULL,
                    [EntityId] int NULL,
                    [PayloadJson] nvarchar(2000) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
                );
            END
            """,
            """
            IF OBJECT_ID(N'[Consultations]', N'U') IS NOT NULL AND COL_LENGTH(N'Consultations', N'UsesCareBenefit') IS NULL
                ALTER TABLE [Consultations] ADD [UsesCareBenefit] bit NOT NULL CONSTRAINT DF_Consultations_UsesCareBenefit DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[CareSubscriptions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [CareSubscriptions] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [Status] int NOT NULL,
                    [PricePerMonth] decimal(10,2) NOT NULL,
                    [Currency] nvarchar(8) NOT NULL,
                    [StartedAt] datetime2 NOT NULL,
                    [CurrentPeriodStart] datetime2 NOT NULL,
                    [CurrentPeriodEnd] datetime2 NOT NULL,
                    [CancelAtPeriodEnd] bit NOT NULL,
                    [QuickConsultsPerCycle] int NOT NULL,
                    CONSTRAINT [PK_CareSubscriptions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CareSubscriptions_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );
            END
            """,
            """
            IF OBJECT_ID(N'[CareBenefitUses]', N'U') IS NULL
            BEGIN
                CREATE TABLE [CareBenefitUses] (
                    [Id] int NOT NULL IDENTITY,
                    [SubscriptionId] int NOT NULL,
                    [ConsultationId] int NULL,
                    [PeriodStart] datetime2 NOT NULL,
                    [UsedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CareBenefitUses] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CareBenefitUses_Subs] FOREIGN KEY ([SubscriptionId]) REFERENCES [CareSubscriptions] ([Id]) ON DELETE CASCADE
                );
            END
            """,
            """
            IF OBJECT_ID(N'[BehaviorCases]', N'U') IS NULL
            BEGIN
                CREATE TABLE [BehaviorCases] (
                    [Id] int NOT NULL IDENTITY,
                    [ClientId] int NOT NULL,
                    [PetId] int NULL,
                    [ProviderId] int NULL,
                    [AppointmentId] int NULL,
                    [Status] int NOT NULL,
                    [ProblemType] nvarchar(120) NULL,
                    [Frequency] nvarchar(40) NULL,
                    [ContextNotes] nvarchar(800) NULL,
                    [VideoUrl] nvarchar(260) NULL,
                    [ClinicalRedFlag] bit NOT NULL,
                    [ScheduledAt] datetime2 NULL,
                    [PriceCharged] decimal(10,2) NOT NULL,
                    [Goals] nvarchar(400) NULL,
                    [Exercises] nvarchar(800) NULL,
                    [FollowUpNotes] nvarchar(400) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_BehaviorCases] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_BehaviorCases_Users] FOREIGN KEY ([ClientId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_BehaviorCases_Pets] FOREIGN KEY ([PetId]) REFERENCES [Pets] ([Id]) ON DELETE SET NULL,
                    CONSTRAINT [FK_BehaviorCases_Providers] FOREIGN KEY ([ProviderId]) REFERENCES [Groomers] ([Id]) ON DELETE SET NULL,
                    CONSTRAINT [FK_BehaviorCases_Appointments] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]) ON DELETE SET NULL
                );
            END
            """,
            """
            IF OBJECT_ID(N'[Consultations]', N'U') IS NOT NULL AND COL_LENGTH(N'Consultations', N'MatchMode') IS NULL
                ALTER TABLE [Consultations] ADD [MatchMode] int NOT NULL CONSTRAINT DF_Consultations_MatchMode DEFAULT(0);
            """,
            """
            IF OBJECT_ID(N'[Consultations]', N'U') IS NOT NULL AND COL_LENGTH(N'Consultations', N'PreferredCountry') IS NULL
                ALTER TABLE [Consultations] ADD [PreferredCountry] nvarchar(2) NULL;
            """,
            """
            IF OBJECT_ID(N'[Consultations]', N'U') IS NOT NULL AND COL_LENGTH(N'Consultations', N'PreferredBreed') IS NULL
                ALTER TABLE [Consultations] ADD [PreferredBreed] nvarchar(80) NULL;
            """,
            """
            IF OBJECT_ID(N'[CountryCatalog]', N'U') IS NULL
            BEGIN
                CREATE TABLE [CountryCatalog] (
                    [Id] int NOT NULL IDENTITY,
                    [Iso2] nvarchar(2) NOT NULL,
                    [NameEs] nvarchar(80) NOT NULL,
                    [NameEn] nvarchar(80) NOT NULL,
                    [PrimaryLanguage] nvarchar(40) NULL,
                    [Status] int NOT NULL,
                    [OpenedAt] datetime2 NULL,
                    CONSTRAINT [PK_CountryCatalog] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_CountryCatalog_Iso2] ON [CountryCatalog] ([Iso2]);
            END
            """,
            """
            IF OBJECT_ID(N'[CountryWaitlist]', N'U') IS NULL
            BEGIN
                CREATE TABLE [CountryWaitlist] (
                    [Id] int NOT NULL IDENTITY,
                    [Iso2] nvarchar(2) NOT NULL,
                    [UserId] int NULL,
                    [Email] nvarchar(150) NULL,
                    [PreferredLanguage] nvarchar(10) NOT NULL,
                    [ConsentMarketing] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CountryWaitlist] PRIMARY KEY ([Id])
                );
            END
            """,
            """
            IF OBJECT_ID(N'[ProviderBreedExpertises]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ProviderBreedExpertises] (
                    [Id] int NOT NULL IDENTITY,
                    [GroomerId] int NOT NULL,
                    [Breed] nvarchar(80) NOT NULL,
                    [EvidenceNote] nvarchar(200) NULL,
                    [IsVerified] bit NOT NULL,
                    [VerifiedAt] datetime2 NULL,
                    CONSTRAINT [PK_ProviderBreedExpertises] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_BreedExp_Groomers] FOREIGN KEY ([GroomerId]) REFERENCES [Groomers] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_BreedExp_Groomer_Breed] ON [ProviderBreedExpertises] ([GroomerId], [Breed]);
            END
            """,
            """
            IF OBJECT_ID(N'[ProviderCompensationRules]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ProviderCompensationRules] (
                    [Id] int NOT NULL IDENTITY,
                    [ProviderUserId] int NULL,
                    [ServiceType] int NOT NULL,
                    [CommissionPercent] decimal(5,2) NOT NULL,
                    [FlatFeeUsd] decimal(10,2) NULL,
                    [PayoutCurrency] nvarchar(8) NOT NULL,
                    [IsActive] bit NOT NULL,
                    [EffectiveFrom] datetime2 NOT NULL,
                    [EffectiveTo] datetime2 NULL,
                    [Notes] nvarchar(400) NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_ProviderCompensationRules] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CompRules_Users] FOREIGN KEY ([ProviderUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );
            END
            """,
            """
            IF OBJECT_ID(N'[ProviderPayouts]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ProviderPayouts] (
                    [Id] int NOT NULL IDENTITY,
                    [ProviderUserId] int NOT NULL,
                    [PeriodStart] datetime2 NOT NULL,
                    [PeriodEnd] datetime2 NOT NULL,
                    [GrossAmountUsd] decimal(10,2) NOT NULL,
                    [CommissionAmountUsd] decimal(10,2) NOT NULL,
                    [NetAmountUsd] decimal(10,2) NOT NULL,
                    [Status] int NOT NULL,
                    [ConsultationCount] int NOT NULL,
                    [CompensationRuleId] int NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    [PaidUtc] datetime2 NULL,
                    [ExternalReference] nvarchar(80) NULL,
                    [Notes] nvarchar(400) NULL,
                    CONSTRAINT [PK_ProviderPayouts] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_Payouts_Users] FOREIGN KEY ([ProviderUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_Payouts_Rules] FOREIGN KEY ([CompensationRuleId]) REFERENCES [ProviderCompensationRules] ([Id]) ON DELETE SET NULL
                );
            END
            """,
            """
            IF OBJECT_ID(N'[ProfessionalOnboardingApplications]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ProfessionalOnboardingApplications] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [Track] int NOT NULL,
                    [Status] int NOT NULL,
                    [LegalName] nvarchar(160) NOT NULL,
                    [ClinicOrPracticeName] nvarchar(160) NOT NULL,
                    [LicenseNumber] nvarchar(80) NOT NULL,
                    [LicenseJurisdiction] nvarchar(40) NOT NULL,
                    [LicenseExpiry] datetime2 NULL,
                    [Languages] nvarchar(200) NOT NULL,
                    [Specialties] nvarchar(300) NOT NULL,
                    [BreedExpertiseCsv] nvarchar(400) NOT NULL,
                    [AcceptsInternationalClients] bit NOT NULL,
                    [HasPhysicalClinic] bit NOT NULL,
                    [VcprCapable] bit NOT NULL,
                    [DocumentsNote] nvarchar(400) NULL,
                    [UploadPath] nvarchar(260) NULL,
                    [SubmittedUtc] datetime2 NULL,
                    [ReviewedUtc] datetime2 NULL,
                    [ReviewerNotes] nvarchar(600) NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_ProfessionalOnboardingApplications] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_Onboarding_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );
            END
            """,
            """
            IF OBJECT_ID(N'[ReminderSchedules]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ReminderSchedules] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [PetId] int NULL,
                    [Type] int NOT NULL,
                    [Title] nvarchar(160) NOT NULL,
                    [Notes] nvarchar(400) NULL,
                    [FrequencyDays] int NULL,
                    [NextDueUtc] datetime2 NOT NULL,
                    [LastSentUtc] datetime2 NULL,
                    [QuietHoursStartLocal] time NULL,
                    [QuietHoursEndLocal] time NULL,
                    [TimeZoneId] nvarchar(64) NOT NULL,
                    [IsActive] bit NOT NULL,
                    [Channel] int NOT NULL,
                    [CreatedUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_ReminderSchedules] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_Reminders_Users] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_Reminders_Pets] FOREIGN KEY ([PetId]) REFERENCES [Pets] ([Id]) ON DELETE SET NULL
                );
            END
            """,
            """
            IF OBJECT_ID(N'[ReminderDeliveries]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ReminderDeliveries] (
                    [Id] int NOT NULL IDENTITY,
                    [ReminderScheduleId] int NOT NULL,
                    [ScheduledForUtc] datetime2 NOT NULL,
                    [SentUtc] datetime2 NULL,
                    [Status] int NOT NULL,
                    [Message] nvarchar(400) NOT NULL,
                    [AppNotificationId] int NULL,
                    CONSTRAINT [PK_ReminderDeliveries] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_Deliveries_Schedules] FOREIGN KEY ([ReminderScheduleId]) REFERENCES [ReminderSchedules] ([Id]) ON DELETE CASCADE
                );
            END
            """
        };

        foreach (var sql in creates)
        {
            try { await db.Database.ExecuteSqlRawAsync(sql); } catch { /* ignore */ }
        }
    }

    private static async Task EnsureVetEcosystemSeedAsync(AppDbContext db)
    {
        await EnsureServiceCatalogSeedAsync(db);
        await EnsureVetDemoProvidersAsync(db);
    }

    private static async Task EnsureServiceCatalogSeedAsync(AppDbContext db)
    {
        var items = new[]
        {
            new ServiceCatalogItem
            {
                Code = ServiceCatalogCodes.VetLocal30,
                NameEs = "Consulta local licenciada",
                NameEn = "Licensed local consultation",
                ScopeEs = "Veterinario licenciado en el estado de la mascota. Diagnóstico o receta solo cuando la ley y la VCPR lo permitan. No es emergencia.",
                ScopeEn = "Veterinarian licensed in the pet's state. Diagnosis or prescription only when law and VCPR allow. Not an emergency.",
                Price = 70m,
                DurationMinutes = 30,
                IsBookable = true
            },
            new ServiceCatalogItem
            {
                Code = ServiceCatalogCodes.VetIntl30,
                NameEs = "Orientación internacional",
                NameEn = "International guidance",
                ScopeEs = "Orientación general con veterinario licenciado fuera de EE.UU. No emite recetas de EE.UU. ni sustituye consulta local o emergencia.",
                ScopeEn = "General guidance with a veterinarian licensed outside the U.S. Does not issue U.S. prescriptions or replace local care or emergency.",
                Price = 30m,
                DurationMinutes = 30,
                IsBookable = true
            },
            new ServiceCatalogItem
            {
                Code = ServiceCatalogCodes.ChomblyCare,
                NameEs = "Chombly Care",
                NameEn = "Chombly Care",
                ScopeEs = "Plan de orientación y bienestar ($14.99/mes). 1 consulta rápida internacional por ciclo. No es seguro médico; no incluye consulta local ni urgencias.",
                ScopeEn = "Guidance and wellness plan ($14.99/mo). 1 quick international consult per cycle. Not pet insurance; local consults and emergencies not included.",
                Price = 14.99m,
                DurationMinutes = 15,
                IsBookable = false
            },
            new ServiceCatalogItem
            {
                Code = ServiceCatalogCodes.BehaviorSession,
                NameEs = "Sesión de comportamiento",
                NameEn = "Behavior session",
                ScopeEs = "Evaluación y plan educativo de conducta. No es diagnóstico veterinario ni medicación. Derivación clínica si hay banderas rojas.",
                ScopeEn = "Behavior evaluation and educational plan. Not a veterinary diagnosis or medication. Clinical referral if red flags.",
                Price = 55m,
                DurationMinutes = 45,
                IsBookable = true
            }
        };

        foreach (var item in items)
        {
            var existing = await db.ServiceCatalog.FirstOrDefaultAsync(s => s.Code == item.Code);
            if (existing == null)
            {
                item.Currency = "USD";
                item.IsActive = true;
                item.EffectiveFrom = DateTime.UtcNow;
                db.ServiceCatalog.Add(item);
            }
            else
            {
                existing.NameEs = item.NameEs;
                existing.NameEn = item.NameEn;
                existing.ScopeEs = item.ScopeEs;
                existing.ScopeEn = item.ScopeEn;
                existing.Price = item.Price;
                existing.DurationMinutes = item.DurationMinutes;
                existing.IsBookable = item.IsBookable;
                existing.IsActive = true;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureVetDemoProvidersAsync(AppDbContext db)
    {
        var vetCat = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "vet");
        if (vetCat == null) return;

        async Task<AppUser> EnsureUserAsync(string email, string name)
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (u != null) return u;
            u = new AppUser
            {
                FullName = name,
                Email = email,
                PasswordHash = Services.PasswordHasher.Hash("123456"),
                City = "Charlotte, NC",
                Role = UserRole.Groomer,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            return u;
        }

        // Local NC vet
        var localUser = await EnsureUserAsync("vet.local@chombly.com", "Dra. Ana Rivera");
        var local = await db.Groomers.FirstOrDefaultAsync(g => g.UserId == localUser.Id);
        if (local == null)
        {
            local = new GroomerProfile
            {
                UserId = localUser.Id,
                CategoryId = vetCat.Id,
                BusinessName = "Chombly Vet Clinic NC",
                Address = "200 Medical Dr",
                City = "Charlotte, NC",
                Latitude = 35.2271,
                Longitude = -80.8431,
                About = "Veterinario local licenciado en Carolina del Norte.",
                Phone = "+17045550101",
                StartingPrice = 70m,
                PriceUnit = "/ 30 min",
                Rating = 4.9,
                ReviewCount = 42,
                IsVerified = true,
                VerifiedLicense = true,
                PublishStatus = BusinessPublishStatus.Approved,
                IsActive = true,
                VetProviderKind = VetProviderKind.LocalVet,
                SpokenLanguages = "es,en",
                OffersEmergency24x7 = false,
                AcceptedSpecies = PetSpecies.DefaultAcceptedList
            };
            db.Groomers.Add(local);
            await db.SaveChangesAsync();
            db.Services.Add(new GroomerService
            {
                GroomerId = local.Id,
                Name = "Consulta local 30 min",
                Description = "Teleconsulta clínica local",
                DurationMinutes = 30,
                PriceSmall = 70, PriceMedium = 70, PriceLarge = 70, PriceGiant = 70
            });
            db.ProviderLicenses.Add(new ProviderLicense
            {
                GroomerId = local.Id,
                Jurisdiction = "NC",
                LicenseNumber = "NC-VET-1001",
                IsVerified = true,
                IsUsState = true,
                ExpiresAt = DateTime.UtcNow.AddYears(2)
            });
            await db.SaveChangesAsync();
        }
        else
        {
            local.VetProviderKind = VetProviderKind.LocalVet;
            local.CategoryId = vetCat.Id;
            local.VerifiedLicense = true;
            if (!await db.ProviderLicenses.AnyAsync(l => l.GroomerId == local.Id && l.Jurisdiction == "NC"))
            {
                db.ProviderLicenses.Add(new ProviderLicense
                {
                    GroomerId = local.Id,
                    Jurisdiction = "NC",
                    LicenseNumber = "NC-VET-1001",
                    IsVerified = true,
                    IsUsState = true,
                    ExpiresAt = DateTime.UtcNow.AddYears(2)
                });
            }
            await db.SaveChangesAsync();
        }

        // International advisor (Colombia)
        var intlUser = await EnsureUserAsync("vet.intl@chombly.com", "Dr. Carlos Méndez");
        var intl = await db.Groomers.FirstOrDefaultAsync(g => g.UserId == intlUser.Id);
        if (intl == null)
        {
            intl = new GroomerProfile
            {
                UserId = intlUser.Id,
                CategoryId = vetCat.Id,
                BusinessName = "Dr. Carlos Méndez",
                Address = "Bogotá",
                City = "Bogotá, CO",
                Latitude = 4.7110,
                Longitude = -74.0721,
                About = "Orientación internacional. Licenciado en Colombia.",
                Phone = "+573001112233",
                StartingPrice = 30m,
                PriceUnit = "/ 30 min",
                Rating = 4.8,
                ReviewCount = 88,
                IsVerified = true,
                VerifiedLicense = true,
                PublishStatus = BusinessPublishStatus.Approved,
                IsActive = true,
                VetProviderKind = VetProviderKind.InternationalAdvisor,
                LicenseCountry = "CO",
                SpokenLanguages = "es,en",
                AcceptedSpecies = PetSpecies.DefaultAcceptedList
            };
            db.Groomers.Add(intl);
            await db.SaveChangesAsync();
            db.Services.Add(new GroomerService
            {
                GroomerId = intl.Id,
                Name = "Orientación internacional 30 min",
                Description = "No emite recetas de EE.UU.",
                DurationMinutes = 30,
                PriceSmall = 30, PriceMedium = 30, PriceLarge = 30, PriceGiant = 30
            });
            db.ProviderLicenses.Add(new ProviderLicense
            {
                GroomerId = intl.Id,
                Jurisdiction = "CO",
                LicenseNumber = "CO-VET-7788",
                IsVerified = true,
                IsUsState = false,
                ExpiresAt = DateTime.UtcNow.AddYears(2)
            });
            await db.SaveChangesAsync();
        }
        else
        {
            intl.VetProviderKind = VetProviderKind.InternationalAdvisor;
            intl.LicenseCountry = "CO";
            intl.CategoryId = vetCat.Id;
            await db.SaveChangesAsync();
        }

        // International advisor (El Salvador)
        var svUser = await EnsureUserAsync("vet.sv@chombly.com", "Dra. Sofía Rivas");
        var sv = await db.Groomers.FirstOrDefaultAsync(g => g.UserId == svUser.Id);
        if (sv == null)
        {
            sv = new GroomerProfile
            {
                UserId = svUser.Id,
                CategoryId = vetCat.Id,
                BusinessName = "Dra. Sofía Rivas",
                Address = "San Salvador",
                City = "San Salvador, SV",
                Latitude = 13.6929,
                Longitude = -89.2182,
                About = "Orientación internacional. Licenciada en El Salvador. Experiencia con razas pequeñas.",
                Phone = "+50370112233",
                StartingPrice = 30m,
                PriceUnit = "/ 30 min",
                Rating = 4.9,
                ReviewCount = 64,
                IsVerified = true,
                VerifiedLicense = true,
                PublishStatus = BusinessPublishStatus.Approved,
                IsActive = true,
                VetProviderKind = VetProviderKind.InternationalAdvisor,
                LicenseCountry = "SV",
                SpokenLanguages = "es",
                AcceptedSpecies = PetSpecies.DefaultAcceptedList
            };
            db.Groomers.Add(sv);
            await db.SaveChangesAsync();
            db.Services.Add(new GroomerService
            {
                GroomerId = sv.Id,
                Name = "Orientación internacional 30 min",
                Description = "No emite recetas de EE.UU.",
                DurationMinutes = 30,
                PriceSmall = 30, PriceMedium = 30, PriceLarge = 30, PriceGiant = 30
            });
            db.ProviderLicenses.Add(new ProviderLicense
            {
                GroomerId = sv.Id,
                Jurisdiction = "SV",
                LicenseNumber = "SV-VET-2201",
                IsVerified = true,
                IsUsState = false,
                ExpiresAt = DateTime.UtcNow.AddYears(2)
            });
            await db.SaveChangesAsync();
        }
        else
        {
            sv.VetProviderKind = VetProviderKind.InternationalAdvisor;
            sv.LicenseCountry = "SV";
            await db.SaveChangesAsync();
        }

        // International advisor (Mexico)
        var mxUser = await EnsureUserAsync("vet.mx@chombly.com", "Dr. Diego Herrera");
        var mx = await db.Groomers.FirstOrDefaultAsync(g => g.UserId == mxUser.Id);
        if (mx == null)
        {
            mx = new GroomerProfile
            {
                UserId = mxUser.Id,
                CategoryId = vetCat.Id,
                BusinessName = "Dr. Diego Herrera",
                Address = "Ciudad de México",
                City = "CDMX, MX",
                Latitude = 19.4326,
                Longitude = -99.1332,
                About = "Orientación internacional. Licenciado en México. Enfoque en nutrición y razas grandes.",
                Phone = "+525551112233",
                StartingPrice = 30m,
                PriceUnit = "/ 30 min",
                Rating = 4.7,
                ReviewCount = 51,
                IsVerified = true,
                VerifiedLicense = true,
                PublishStatus = BusinessPublishStatus.Approved,
                IsActive = true,
                VetProviderKind = VetProviderKind.InternationalAdvisor,
                LicenseCountry = "MX",
                SpokenLanguages = "es,en",
                AcceptedSpecies = PetSpecies.DefaultAcceptedList
            };
            db.Groomers.Add(mx);
            await db.SaveChangesAsync();
            db.Services.Add(new GroomerService
            {
                GroomerId = mx.Id,
                Name = "Orientación internacional 30 min",
                Description = "No emite recetas de EE.UU.",
                DurationMinutes = 30,
                PriceSmall = 30, PriceMedium = 30, PriceLarge = 30, PriceGiant = 30
            });
            db.ProviderLicenses.Add(new ProviderLicense
            {
                GroomerId = mx.Id,
                Jurisdiction = "MX",
                LicenseNumber = "MX-VET-4410",
                IsVerified = true,
                IsUsState = false,
                ExpiresAt = DateTime.UtcNow.AddYears(2)
            });
            await db.SaveChangesAsync();
        }
        else
        {
            mx.VetProviderKind = VetProviderKind.InternationalAdvisor;
            mx.LicenseCountry = "MX";
            await db.SaveChangesAsync();
        }

        // Emergency 24/7 clinics
        var erUser = await EnsureUserAsync("vet.er@chombly.com", "Charlotte Pet ER");
        var er = await db.Groomers.FirstOrDefaultAsync(g => g.UserId == erUser.Id);
        if (er == null)
        {
            er = new GroomerProfile
            {
                UserId = erUser.Id,
                CategoryId = vetCat.Id,
                BusinessName = "Charlotte Pet Emergency 24/7",
                Address = "900 Emergency Way",
                City = "Charlotte, NC",
                Latitude = 35.2275,
                Longitude = -80.8432,
                About = "Clínica de urgencias veterinarias 24/7.",
                Phone = "+17045550911",
                StartingPrice = 0,
                Rating = 4.7,
                ReviewCount = 210,
                IsVerified = true,
                PublishStatus = BusinessPublishStatus.Approved,
                IsActive = true,
                OffersEmergency24x7 = true,
                VetProviderKind = VetProviderKind.LocalVet,
                AcceptedSpecies = PetSpecies.DefaultAcceptedList
            };
            db.Groomers.Add(er);
            await db.SaveChangesAsync();
            db.Services.Add(new GroomerService
            {
                GroomerId = er.Id,
                Name = "Urgencias",
                Description = "Atención de emergencia presencial",
                DurationMinutes = 30,
                PriceSmall = 0, PriceMedium = 0, PriceLarge = 0, PriceGiant = 0
            });
            await db.SaveChangesAsync();
        }
        else
        {
            er.OffersEmergency24x7 = true;
            await db.SaveChangesAsync();
        }

        // Behavior specialist demo
        var behUser = await EnsureUserAsync("behavior@chombly.com", "Laura Campos");
        var beh = await db.Groomers.FirstOrDefaultAsync(g => g.UserId == behUser.Id);
        var trainersCat = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "trainers");
        if (beh == null)
        {
            beh = new GroomerProfile
            {
                UserId = behUser.Id,
                CategoryId = trainersCat?.Id ?? vetCat.Id,
                BusinessName = "Laura Campos — Conducta",
                Address = "Charlotte",
                City = "Charlotte, NC",
                Latitude = 35.23,
                Longitude = -80.84,
                About = "Especialista en comportamiento canino. Plan educativo; no diagnostica ni medica.",
                Phone = "+17045550333",
                StartingPrice = 55m,
                PriceUnit = "/ sesión",
                Rating = 4.9,
                ReviewCount = 36,
                IsVerified = true,
                PublishStatus = BusinessPublishStatus.Approved,
                IsActive = true,
                VetProviderKind = VetProviderKind.BehaviorSpecialist,
                BehaviorRole = BehaviorSpecialistRole.Consultant,
                SpokenLanguages = "es,en",
                AcceptedSpecies = PetSpecies.Dog
            };
            db.Groomers.Add(beh);
            await db.SaveChangesAsync();
            db.Services.Add(new GroomerService
            {
                GroomerId = beh.Id,
                Name = "Sesión de comportamiento",
                Description = "Evaluación y plan de conducta",
                DurationMinutes = 45,
                PriceSmall = 55, PriceMedium = 55, PriceLarge = 55, PriceGiant = 55
            });
            await db.SaveChangesAsync();
        }
        else
        {
            beh.VetProviderKind = VetProviderKind.BehaviorSpecialist;
            beh.BehaviorRole = BehaviorSpecialistRole.Consultant;
            await db.SaveChangesAsync();
        }

        // Demo VCPR so local telehealth path can be tested (one pet if none exist)
        if (!await db.VcprRecords.AnyAsync() && local != null)
        {
            var pet = await db.Pets.OrderBy(p => p.Id).FirstOrDefaultAsync();
            if (pet != null)
            {
                db.VcprRecords.Add(new VcprRecord
                {
                    PetId = pet.Id,
                    ProviderId = local.Id,
                    UsState = "NC",
                    ExamDate = DateTime.UtcNow.AddMonths(-2),
                    EvidenceNote = "Demo in-person exam (seed)",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task EnsureCountryCatalogSeedAsync(AppDbContext db)
    {
        foreach (var item in CountryCatalogSeed.Items)
        {
            var existing = await db.CountryCatalog.FirstOrDefaultAsync(c => c.Iso2 == item.Iso);
            if (existing == null)
            {
                db.CountryCatalog.Add(new CountryCatalogEntry
                {
                    Iso2 = item.Iso,
                    NameEs = item.Es,
                    NameEn = item.En,
                    PrimaryLanguage = item.Lang,
                    Status = item.Status,
                    OpenedAt = item.Status == CountryMarketStatus.Available ? DateTime.UtcNow : null
                });
            }
            else
            {
                existing.NameEs = item.Es;
                existing.NameEn = item.En;
                existing.PrimaryLanguage = item.Lang;
                // Keep Available if already open; only upgrade ComingSoon→Available from seed when marked Available
                if (item.Status == CountryMarketStatus.Available)
                {
                    existing.Status = CountryMarketStatus.Available;
                    existing.OpenedAt ??= DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync();

        async Task EnsureBreedsAsync(string email, params (string Breed, string Note)[] breeds)
        {
            var g = await db.Groomers.Include(x => x.User)
                .FirstOrDefaultAsync(x => x.User.Email == email);
            if (g == null) return;
            foreach (var (breed, note) in breeds)
            {
                if (await db.ProviderBreedExpertises.AnyAsync(e => e.GroomerId == g.Id && e.Breed == breed))
                    continue;
                db.ProviderBreedExpertises.Add(new ProviderBreedExpertise
                {
                    GroomerId = g.Id,
                    Breed = breed,
                    EvidenceNote = note,
                    IsVerified = true,
                    VerifiedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        await EnsureBreedsAsync("vet.intl@chombly.com",
            ("Pastor alemán", "Casos clínicos verificados (demo)"),
            ("Labrador", "Experiencia clínica (demo)"),
            ("Golden Retriever", "Seguimiento nutricional (demo)"));
        await EnsureBreedsAsync("vet.sv@chombly.com",
            ("Chihuahua", "Razas toy (demo)"),
            ("Poodle", "Experiencia clínica (demo)"),
            ("Mestizo", "Casos generales (demo)"));
        await EnsureBreedsAsync("vet.mx@chombly.com",
            ("Pastor alemán", "Ortopedia y raza (demo)"),
            ("Husky siberiano", "Experiencia verificada (demo)"),
            ("Bulldog", "Cuidados respiratorios (demo)"));
    }

    private static async Task BackfillAcceptedSpeciesAsync(AppDbContext db)
    {
        try
        {
            var all = PetSpecies.DefaultAcceptedList;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [Groomers]
                SET [AcceptedSpecies] = {all}
                WHERE [AcceptedSpecies] IS NULL OR LTRIM(RTRIM([AcceptedSpecies])) = N''
                """);
        }
        catch { /* ignore */ }
    }

    private static async Task EnsureAdminAsync(AppDbContext db)
    {
        const string email = "admin@chombly.com";
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (admin == null)
        {
            db.Users.Add(new AppUser
            {
                FullName = "Admin Chombly",
                Email = email,
                PasswordHash = Services.PasswordHasher.Hash("123456"),
                City = "Charlotte, NC",
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return;
        }

        if (admin.Role != UserRole.Admin)
        {
            admin.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seeds platform-default compensation rules (ProviderUserId null = applies to all providers of that ServiceType).
    /// </summary>
    private static async Task EnsureCompensationDefaultsAsync(AppDbContext db)
    {
        try
        {
            async Task Ensure(CompensationServiceType type, decimal pct, string note)
            {
                if (await db.ProviderCompensationRules.AnyAsync(r =>
                        r.ProviderUserId == null && r.ServiceType == type && r.IsActive))
                    return;

                db.ProviderCompensationRules.Add(new ProviderCompensationRule
                {
                    ProviderUserId = null,
                    ServiceType = type,
                    CommissionPercent = pct,
                    PayoutCurrency = "USD",
                    IsActive = true,
                    EffectiveFrom = DateTime.UtcNow.Date,
                    Notes = note,
                    CreatedUtc = DateTime.UtcNow
                });
            }

            await Ensure(CompensationServiceType.LocalVet, 20m, "Platform default LocalVet 20%");
            await Ensure(CompensationServiceType.InternationalVet, 25m, "Platform default InternationalVet 25%");
            await Ensure(CompensationServiceType.Behavior, 20m, "Platform default Behavior 20%");
            await db.SaveChangesAsync();
        }
        catch { /* ignore if tables not ready */ }
    }
}
