/*
  SOLO ESQUEMA — ejecuta ESTE archivo primero si el Query Editor de Azure
  no respeta GO. Luego ejecuta alta-negocios-datos.sql
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Groomers', N'U') IS NULL
  THROW 50001, N'No existe dbo.Groomers.', 1;

IF COL_LENGTH(N'dbo.Groomers', N'AcceptedSpecies') IS NULL
  ALTER TABLE dbo.Groomers ADD AcceptedSpecies nvarchar(200) NULL;
IF COL_LENGTH(N'dbo.Groomers', N'CategoryId') IS NULL
  ALTER TABLE dbo.Groomers ADD CategoryId int NULL;
IF COL_LENGTH(N'dbo.Groomers', N'IsActive') IS NULL
  ALTER TABLE dbo.Groomers ADD IsActive bit NOT NULL CONSTRAINT DF_Groomers_IsActive DEFAULT (1);
IF COL_LENGTH(N'dbo.Groomers', N'PriceUnit') IS NULL
  ALTER TABLE dbo.Groomers ADD PriceUnit nvarchar(40) NULL;

IF COL_LENGTH(N'dbo.Services', N'BillingUnit') IS NULL
  ALTER TABLE dbo.Services ADD BillingUnit nvarchar(20) NULL;

IF OBJECT_ID(N'dbo.Appointments', N'U') IS NOT NULL
BEGIN
  IF COL_LENGTH(N'dbo.Appointments', N'EndAt') IS NULL
    ALTER TABLE dbo.Appointments ADD EndAt datetime2 NULL;
  IF COL_LENGTH(N'dbo.Appointments', N'Nights') IS NULL
    ALTER TABLE dbo.Appointments ADD Nights int NOT NULL CONSTRAINT DF_Appointments_Nights DEFAULT (0);
END

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
  IF COL_LENGTH(N'dbo.Users', N'Latitude') IS NULL
    ALTER TABLE dbo.Users ADD Latitude float NULL;
  IF COL_LENGTH(N'dbo.Users', N'Longitude') IS NULL
    ALTER TABLE dbo.Users ADD Longitude float NULL;
  IF COL_LENGTH(N'dbo.Users', N'LocationUpdatedAt') IS NULL
    ALTER TABLE dbo.Users ADD LocationUpdatedAt datetime2 NULL;
END

IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.Categories (
    Id int NOT NULL IDENTITY(1,1),
    Slug nvarchar(40) NOT NULL,
    Name nvarchar(80) NOT NULL,
    Subtitle nvarchar(160) NOT NULL,
    Icon nvarchar(120) NOT NULL,
    Emoji nvarchar(40) NOT NULL,
    IsOvernight bit NOT NULL,
    SortOrder int NOT NULL,
    IsActive bit NOT NULL,
    CONSTRAINT PK_Categories PRIMARY KEY (Id)
  );
  CREATE UNIQUE INDEX IX_Categories_Slug ON dbo.Categories (Slug);
END

IF OBJECT_ID(N'dbo.Amenities', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.Amenities (
    Id int NOT NULL IDENTITY(1,1),
    GroomerId int NOT NULL,
    Label nvarchar(80) NOT NULL,
    Icon nvarchar(40) NOT NULL,
    SortOrder int NOT NULL,
    CONSTRAINT PK_Amenities PRIMARY KEY (Id),
    CONSTRAINT FK_Amenities_Groomers FOREIGN KEY (GroomerId) REFERENCES dbo.Groomers (Id) ON DELETE CASCADE
  );
END

IF OBJECT_ID(N'dbo.ServiceExtras', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.ServiceExtras (
    Id int NOT NULL IDENTITY(1,1),
    GroomerId int NOT NULL,
    Name nvarchar(100) NOT NULL,
    Description nvarchar(200) NULL,
    Price decimal(10,2) NOT NULL,
    IsActive bit NOT NULL,
    CONSTRAINT PK_ServiceExtras PRIMARY KEY (Id),
    CONSTRAINT FK_ServiceExtras_Groomers FOREIGN KEY (GroomerId) REFERENCES dbo.Groomers (Id) ON DELETE CASCADE
  );
END

IF OBJECT_ID(N'dbo.DayAvailabilities', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.DayAvailabilities (
    Id int NOT NULL IDENTITY(1,1),
    GroomerId int NOT NULL,
    Day datetime2 NOT NULL,
    IsAvailable bit NOT NULL,
    Note nvarchar(120) NULL,
    CONSTRAINT PK_DayAvailabilities PRIMARY KEY (Id),
    CONSTRAINT FK_DayAvailabilities_Groomers FOREIGN KEY (GroomerId) REFERENCES dbo.Groomers (Id) ON DELETE CASCADE
  );
  CREATE UNIQUE INDEX IX_DayAvailabilities_GroomerId_Day ON dbo.DayAvailabilities (GroomerId, Day);
END

IF COL_LENGTH(N'dbo.Groomers', N'PublishStatus') IS NULL
  ALTER TABLE dbo.Groomers ADD PublishStatus int NOT NULL CONSTRAINT DF_Groomers_PublishStatus DEFAULT (2);

IF COL_LENGTH(N'dbo.Groomers', N'ProviderKind') IS NULL
  ALTER TABLE dbo.Groomers ADD ProviderKind int NOT NULL CONSTRAINT DF_Groomers_ProviderKind DEFAULT (0);
IF COL_LENGTH(N'dbo.Groomers', N'WorkMode') IS NULL
  ALTER TABLE dbo.Groomers ADD WorkMode int NOT NULL CONSTRAINT DF_Groomers_WorkMode DEFAULT (0);
IF COL_LENGTH(N'dbo.Groomers', N'ServiceAreaMiles') IS NULL
  ALTER TABLE dbo.Groomers ADD ServiceAreaMiles int NOT NULL CONSTRAINT DF_Groomers_ServiceAreaMiles DEFAULT (10);
IF COL_LENGTH(N'dbo.Groomers', N'LogoUrl') IS NULL
  ALTER TABLE dbo.Groomers ADD LogoUrl nvarchar(260) NULL;
IF COL_LENGTH(N'dbo.Groomers', N'CoverUrl') IS NULL
  ALTER TABLE dbo.Groomers ADD CoverUrl nvarchar(260) NULL;
IF COL_LENGTH(N'dbo.Groomers', N'VerifiedIdentity') IS NULL
  ALTER TABLE dbo.Groomers ADD VerifiedIdentity bit NOT NULL CONSTRAINT DF_Groomers_VerifiedIdentity DEFAULT (0);
IF COL_LENGTH(N'dbo.Groomers', N'VerifiedLicense') IS NULL
  ALTER TABLE dbo.Groomers ADD VerifiedLicense bit NOT NULL CONSTRAINT DF_Groomers_VerifiedLicense DEFAULT (0);
IF COL_LENGTH(N'dbo.Groomers', N'VerifiedInsurance') IS NULL
  ALTER TABLE dbo.Groomers ADD VerifiedInsurance bit NOT NULL CONSTRAINT DF_Groomers_VerifiedInsurance DEFAULT (0);
IF COL_LENGTH(N'dbo.Groomers', N'VerifiedBank') IS NULL
  ALTER TABLE dbo.Groomers ADD VerifiedBank bit NOT NULL CONSTRAINT DF_Groomers_VerifiedBank DEFAULT (0);
IF COL_LENGTH(N'dbo.Groomers', N'SupportCallAt') IS NULL
  ALTER TABLE dbo.Groomers ADD SupportCallAt datetime2 NULL;

IF OBJECT_ID(N'dbo.WeeklyHours', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.WeeklyHours (
    Id int NOT NULL IDENTITY,
    GroomerId int NOT NULL,
    DayOfWeek int NOT NULL,
    IsOpen bit NOT NULL,
    OpenMinutes int NOT NULL,
    CloseMinutes int NOT NULL,
    CONSTRAINT PK_WeeklyHours PRIMARY KEY (Id),
    CONSTRAINT FK_WeeklyHours_Groomers FOREIGN KEY (GroomerId) REFERENCES dbo.Groomers (Id) ON DELETE CASCADE
  );
  CREATE UNIQUE INDEX IX_WeeklyHours_GroomerId_DayOfWeek ON dbo.WeeklyHours (GroomerId, DayOfWeek);
END

IF COL_LENGTH(N'dbo.Groomers', N'CategoryId') IS NOT NULL
   AND OBJECT_ID(N'dbo.Categories', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Groomers_Categories')
  ALTER TABLE dbo.Groomers WITH NOCHECK
  ADD CONSTRAINT FK_Groomers_Categories
  FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (Id) ON DELETE SET NULL;

SELECT
  CategoryId  = COL_LENGTH(N'dbo.Groomers', N'CategoryId'),
  IsActive    = COL_LENGTH(N'dbo.Groomers', N'IsActive'),
  PriceUnit   = COL_LENGTH(N'dbo.Groomers', N'PriceUnit'),
  BillingUnit = COL_LENGTH(N'dbo.Services', N'BillingUnit'),
  PublishStatus = COL_LENGTH(N'dbo.Groomers', N'PublishStatus');

PRINT N'Esquema OK. Ahora ejecuta alta-negocios-datos.sql';
