/*
  Chombly — Esquema + Alta de negocios (Azure SQL)
  ------------------------------------------------
  IMPORTANTE: este archivo usa GO (separador de lotes).
  - En SSMS / Azure Data Studio: ejecuta el archivo completo.
  - En Azure Portal Query Editor: si falla, ejecuta primero
    hasta el primer GO (solo esquema) y luego el resto.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'=== PARTE 1: ESQUEMA ===';

/* ---- Groomers ---- */
IF OBJECT_ID(N'dbo.Groomers', N'U') IS NULL
  THROW 50001, N'No existe dbo.Groomers. Arranca la app una vez o revisa el nombre de la BD.', 1;

IF COL_LENGTH(N'dbo.Groomers', N'AcceptedSpecies') IS NULL
  ALTER TABLE dbo.Groomers ADD AcceptedSpecies nvarchar(200) NULL;

IF COL_LENGTH(N'dbo.Groomers', N'CategoryId') IS NULL
  ALTER TABLE dbo.Groomers ADD CategoryId int NULL;

IF COL_LENGTH(N'dbo.Groomers', N'IsActive') IS NULL
  ALTER TABLE dbo.Groomers ADD IsActive bit NOT NULL CONSTRAINT DF_Groomers_IsActive DEFAULT (1);

IF COL_LENGTH(N'dbo.Groomers', N'PriceUnit') IS NULL
  ALTER TABLE dbo.Groomers ADD PriceUnit nvarchar(40) NULL;

/* ---- Services ---- */
IF OBJECT_ID(N'dbo.Services', N'U') IS NULL
  THROW 50002, N'No existe dbo.Services.', 1;

IF COL_LENGTH(N'dbo.Services', N'BillingUnit') IS NULL
  ALTER TABLE dbo.Services ADD BillingUnit nvarchar(20) NULL;

/* ---- Appointments ---- */
IF OBJECT_ID(N'dbo.Appointments', N'U') IS NOT NULL
BEGIN
  IF COL_LENGTH(N'dbo.Appointments', N'EndAt') IS NULL
    ALTER TABLE dbo.Appointments ADD EndAt datetime2 NULL;
  IF COL_LENGTH(N'dbo.Appointments', N'Nights') IS NULL
    ALTER TABLE dbo.Appointments ADD Nights int NOT NULL CONSTRAINT DF_Appointments_Nights DEFAULT (0);
END

/* ---- Users ubicación ---- */
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
  IF COL_LENGTH(N'dbo.Users', N'Latitude') IS NULL
    ALTER TABLE dbo.Users ADD Latitude float NULL;
  IF COL_LENGTH(N'dbo.Users', N'Longitude') IS NULL
    ALTER TABLE dbo.Users ADD Longitude float NULL;
  IF COL_LENGTH(N'dbo.Users', N'LocationUpdatedAt') IS NULL
    ALTER TABLE dbo.Users ADD LocationUpdatedAt datetime2 NULL;
END

/* ---- Categories ---- */
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
ELSE IF COL_LENGTH(N'dbo.Categories', N'Icon') IS NOT NULL AND COL_LENGTH(N'dbo.Categories', N'Icon') < 120
  ALTER TABLE dbo.Categories ALTER COLUMN Icon nvarchar(120) NOT NULL;

/* ---- Amenities ---- */
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

/* ---- ServiceExtras ---- */
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

/* ---- DayAvailabilities ---- */
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

/* ---- Conversations / Chat ---- */
IF OBJECT_ID(N'dbo.Conversations', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.Conversations (
    Id int NOT NULL IDENTITY(1,1),
    ClientId int NOT NULL,
    GroomerId int NOT NULL,
    AppointmentId int NULL,
    CreatedAt datetime2 NOT NULL,
    LastMessageAt datetime2 NOT NULL,
    CONSTRAINT PK_Conversations PRIMARY KEY (Id)
  );
END

IF OBJECT_ID(N'dbo.ChatMessages', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.ChatMessages (
    Id int NOT NULL IDENTITY(1,1),
    ConversationId int NOT NULL,
    SenderUserId int NOT NULL,
    Body nvarchar(2000) NOT NULL,
    SentAt datetime2 NOT NULL,
    IsRead bit NOT NULL,
    CONSTRAINT PK_ChatMessages PRIMARY KEY (Id)
  );
END

/* ---- FK CategoryId ---- */
IF COL_LENGTH(N'dbo.Groomers', N'CategoryId') IS NOT NULL
   AND OBJECT_ID(N'dbo.Categories', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Groomers_Categories')
BEGIN
  ALTER TABLE dbo.Groomers WITH NOCHECK
  ADD CONSTRAINT FK_Groomers_Categories
  FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (Id) ON DELETE SET NULL;
END

PRINT N'Esquema aplicado.';
PRINT N'CategoryId  = ' + ISNULL(CONVERT(varchar(20), COL_LENGTH(N'dbo.Groomers', N'CategoryId')), N'NULL');
PRINT N'IsActive    = ' + ISNULL(CONVERT(varchar(20), COL_LENGTH(N'dbo.Groomers', N'IsActive')), N'NULL');
PRINT N'PriceUnit   = ' + ISNULL(CONVERT(varchar(20), COL_LENGTH(N'dbo.Groomers', N'PriceUnit')), N'NULL');
PRINT N'BillingUnit = ' + ISNULL(CONVERT(varchar(20), COL_LENGTH(N'dbo.Services', N'BillingUnit')), N'NULL');
GO

/* =========================================================
   PARTE 2: DATOS (lote nuevo — ya ve las columnas)
   ========================================================= */
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'=== PARTE 2: CATEGORÍAS + NEGOCIOS ===';

IF COL_LENGTH(N'dbo.Groomers', N'CategoryId') IS NULL
   OR COL_LENGTH(N'dbo.Groomers', N'IsActive') IS NULL
   OR COL_LENGTH(N'dbo.Groomers', N'PriceUnit') IS NULL
   OR COL_LENGTH(N'dbo.Services', N'BillingUnit') IS NULL
BEGIN
  THROW 50010, N'Faltan columnas. Ejecuta primero la PARTE 1 (hasta el GO) y luego esta parte.', 1;
END

MERGE dbo.Categories AS t
USING (VALUES
  (N'grooming', N'Grooming', N'Baño, corte y estética', N'/images/categories/cat-grooming.png', N'✂️', 0, 1, 1),
  (N'hotel',    N'Hotel', N'Hospedaje para tu mascota', N'/images/categories/cat-hotel.png', N'🏨', 1, 2, 1),
  (N'vet',      N'Veterinario', N'Consultas y cuidados', N'/images/categories/cat-vet.png', N'🩺', 0, 3, 1),
  (N'daycare',  N'Daycare', N'Guardería diurna', N'/images/categories/cat-daycare.png', N'☀️', 1, 4, 1),
  (N'walkers',  N'Walkers', N'Paseos profesionales', N'/images/categories/cat-walkers.png', N'🦮', 0, 5, 1),
  (N'trainers', N'Trainers', N'Entrenamiento', N'/images/categories/cat-trainers.png', N'🎓', 0, 6, 1)
) AS s (Slug, Name, Subtitle, Icon, Emoji, IsOvernight, SortOrder, IsActive)
ON t.Slug = s.Slug
WHEN MATCHED THEN UPDATE SET
  Name = s.Name, Subtitle = s.Subtitle, Icon = s.Icon, Emoji = s.Emoji,
  IsOvernight = s.IsOvernight, SortOrder = s.SortOrder, IsActive = s.IsActive
WHEN NOT MATCHED THEN
  INSERT (Slug, Name, Subtitle, Icon, Emoji, IsOvernight, SortOrder, IsActive)
  VALUES (s.Slug, s.Name, s.Subtitle, s.Icon, s.Emoji, s.IsOvernight, s.SortOrder, s.IsActive);

-- Hash SHA256 UTF-8/ASCII de "pawcare:123456" (igual que PasswordHasher de la app)
DECLARE @PwdHash nvarchar(64) =
  CONVERT(nvarchar(64), HASHBYTES(N'SHA2_256', CONVERT(varchar(100), 'pawcare:123456')), 2);

DECLARE @GroomingId int = (SELECT TOP 1 Id FROM dbo.Categories WHERE Slug = N'grooming');
DECLARE @HotelId    int = (SELECT TOP 1 Id FROM dbo.Categories WHERE Slug = N'hotel');
DECLARE @VetId      int = (SELECT TOP 1 Id FROM dbo.Categories WHERE Slug = N'vet');

BEGIN TRAN;

/* ---------- Hotel ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'hotel@chombly.com')
BEGIN
  INSERT INTO dbo.Users (FullName, Email, PasswordHash, Phone, City, Role, CreatedAt)
  VALUES (N'Ana Hotel', N'hotel@chombly.com', @PwdHash, N'704-555-1001', N'Charlotte, NC', 1, SYSUTCDATETIME());

  DECLARE @uHotel int = SCOPE_IDENTITY();

  INSERT INTO dbo.Groomers (
    UserId, CategoryId, BusinessName, Type, Address, City, Latitude, Longitude,
    About, ImageUrl, Phone, AcceptsSeniorDogs, AcceptsAnxiousDogs, AcceptedSpecies,
    IsVerified, IsFeatured, IsActive, Rating, ReviewCount, StartingPrice, PriceUnit)
  VALUES (
    @uHotel, @HotelId, N'Paw Palace Hotel', 0,
    N'100 Pet Lane, Charlotte, NC', N'Charlotte, NC', 35.2271, -80.8431,
    N'Hospedaje multi-mascota con cámaras 24/7.', N'/images/groomer-athome.png', N'704-555-1001',
    1, 1, N'Perro,Gato,Conejo', 1, 1, 1, 4.9, 0, 45, N'/ noche');

  DECLARE @gHotel int = SCOPE_IDENTITY();

  INSERT INTO dbo.Services (GroomerId, Name, Description, PriceSmall, PriceMedium, PriceLarge, PriceGiant, DurationMinutes, Icon, BillingUnit)
  VALUES
    (@gHotel, N'Noche estándar', N'Hospedaje por noche', 45, 55, 65, 80, 1440, N'hotel', N'noche'),
    (@gHotel, N'Noche premium', N'Suite con cámara privada', 60, 70, 85, 100, 1440, N'hotel', N'noche');

  INSERT INTO dbo.Amenities (GroomerId, Label, Icon, SortOrder) VALUES
    (@gHotel, N'Cámaras 24/7', N'cam', 1),
    (@gHotel, N'Patio grande', N'yard', 2),
    (@gHotel, N'Personal 24/7', N'staff', 3),
    (@gHotel, N'Acepta mascotas grandes', N'paw', 4);

  INSERT INTO dbo.ServiceExtras (GroomerId, Name, Description, Price, IsActive) VALUES
    (@gHotel, N'Baño adicional', NULL, 20, 1),
    (@gHotel, N'Administración de medicamentos', NULL, 15, 1),
    (@gHotel, N'Cámara privada', NULL, 10, 1);

  DECLARE @i int = 0;
  WHILE @i < 14
  BEGIN
    IF NOT EXISTS (
      SELECT 1 FROM dbo.DayAvailabilities
      WHERE GroomerId = @gHotel AND [Day] = DATEADD(DAY, @i, CAST(GETDATE() AS date)))
    BEGIN
      INSERT INTO dbo.DayAvailabilities (GroomerId, [Day], IsAvailable, Note)
      VALUES (
        @gHotel,
        DATEADD(DAY, @i, CAST(GETDATE() AS date)),
        CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 80 THEN 1 ELSE 0 END,
        N'SQL');
    END
    SET @i += 1;
  END

  PRINT N'Hotel: hotel@chombly.com / 123456';
END
ELSE PRINT N'Hotel ya existía.';

/* ---------- Grooming ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'grooming@chombly.com')
BEGIN
  INSERT INTO dbo.Users (FullName, Email, PasswordHash, Phone, City, Role, CreatedAt)
  VALUES (N'Luis Grooming', N'grooming@chombly.com', @PwdHash, N'704-555-1002', N'Charlotte, NC', 1, SYSUTCDATETIME());

  DECLARE @uG int = SCOPE_IDENTITY();

  INSERT INTO dbo.Groomers (
    UserId, CategoryId, BusinessName, Type, Address, City, Latitude, Longitude,
    About, ImageUrl, Phone, AcceptsSeniorDogs, AcceptsAnxiousDogs, AcceptedSpecies,
    IsVerified, IsFeatured, IsActive, Rating, ReviewCount, StartingPrice, PriceUnit)
  VALUES (
    @uG, @GroomingId, N'Happy Paws Grooming', 0,
    N'200 Style Ave, Charlotte, NC', N'Charlotte, NC', 35.2300, -80.8500,
    N'Grooming para perros, gatos y más.', N'/images/groomer-happypaws.png', N'704-555-1002',
    1, 1, N'Perro,Gato,Conejo,Cobaya,Ave,Hurón,Otro', 1, 1, 1, 4.8, 0, 35, N'/ sesión');

  DECLARE @gG int = SCOPE_IDENTITY();

  INSERT INTO dbo.Services (GroomerId, Name, Description, PriceSmall, PriceMedium, PriceLarge, PriceGiant, DurationMinutes, Icon, BillingUnit)
  VALUES
    (@gG, N'Baño y cepillado', N'Para cualquier mascota', 35, 45, 55, 70, 60, N'bath', N'sesion'),
    (@gG, N'Grooming para gatos', N'Baño suave felino', 40, 50, 60, 70, 50, N'cat', N'sesion');

  PRINT N'Grooming: grooming@chombly.com / 123456';
END
ELSE PRINT N'Grooming ya existía.';

/* ---------- Vet ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'vet@chombly.com')
BEGIN
  INSERT INTO dbo.Users (FullName, Email, PasswordHash, Phone, City, Role, CreatedAt)
  VALUES (N'Dra. Vet', N'vet@chombly.com', @PwdHash, N'704-555-1003', N'Charlotte, NC', 1, SYSUTCDATETIME());

  DECLARE @uV int = SCOPE_IDENTITY();

  INSERT INTO dbo.Groomers (
    UserId, CategoryId, BusinessName, Type, Address, City, Latitude, Longitude,
    About, Phone, AcceptsSeniorDogs, AcceptsAnxiousDogs, AcceptedSpecies,
    IsVerified, IsFeatured, IsActive, Rating, ReviewCount, StartingPrice, PriceUnit)
  VALUES (
    @uV, @VetId, N'Chombly Vet Clinic', 0,
    N'300 Care Rd, Charlotte, NC', N'Charlotte, NC', 35.2100, -80.8300,
    N'Consultas multi-especie.', N'704-555-1003',
    1, 1, N'Perro,Gato,Conejo,Ave,Hurón', 1, 0, 1, 0, 0, 50, N'/ visita');

  DECLARE @gV int = SCOPE_IDENTITY();

  INSERT INTO dbo.Services (GroomerId, Name, Description, PriceSmall, PriceMedium, PriceLarge, PriceGiant, DurationMinutes, Icon, BillingUnit)
  VALUES (@gV, N'Consulta general', N'Revisión clínica', 50, 60, 70, 80, 30, N'vet', N'visita');

  PRINT N'Vet: vet@chombly.com / 123456';
END
ELSE PRINT N'Vet ya existía.';

UPDATE dbo.Groomers
SET AcceptedSpecies = N'Perro,Gato,Conejo,Cobaya,Ave,Hurón,Otro'
WHERE AcceptedSpecies IS NULL OR LTRIM(RTRIM(AcceptedSpecies)) = N'';

COMMIT TRAN;
PRINT N'Listo.';
GO
