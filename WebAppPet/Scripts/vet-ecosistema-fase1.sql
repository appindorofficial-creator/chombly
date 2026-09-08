-- Chombly Vet Ecosystem Phase 1 — idempotent schema helpers
-- Prefer app startup (DbInitializer). Use this if Azure SQL needs a manual apply.

SET NOCOUNT ON;

IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'VetProviderKind') IS NULL
    ALTER TABLE [Groomers] ADD [VetProviderKind] int NOT NULL CONSTRAINT DF_Groomers_VetProviderKind DEFAULT(0);
IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'LicenseCountry') IS NULL
    ALTER TABLE [Groomers] ADD [LicenseCountry] nvarchar(8) NULL;
IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'SpokenLanguages') IS NULL
    ALTER TABLE [Groomers] ADD [SpokenLanguages] nvarchar(120) NULL;
IF OBJECT_ID(N'[Groomers]', N'U') IS NOT NULL AND COL_LENGTH(N'Groomers', N'OffersEmergency24x7') IS NULL
    ALTER TABLE [Groomers] ADD [OffersEmergency24x7] bit NOT NULL CONSTRAINT DF_Groomers_OffersEmergency24x7 DEFAULT(0);
IF OBJECT_ID(N'[Pets]', N'U') IS NOT NULL AND COL_LENGTH(N'Pets', N'WeightLbs') IS NULL
    ALTER TABLE [Pets] ADD [WeightLbs] decimal(8,2) NULL;

IF OBJECT_ID(N'[Consultations]', N'U') IS NOT NULL AND COL_LENGTH(N'Consultations', N'UsesCareBenefit') IS NULL
    ALTER TABLE [Consultations] ADD [UsesCareBenefit] bit NOT NULL CONSTRAINT DF_Consultations_UsesCareBenefit DEFAULT(0);

PRINT 'Vet Phase 1+2 column alters done. Tables are created by app DbInitializer on startup.';
GO

