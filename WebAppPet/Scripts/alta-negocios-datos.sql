/*
  SOLO DATOS — ejecutar DESPUÉS de alta-negocios-esquema.sql
  (o después de la PARTE 1 de alta-negocios.sql)
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.Groomers', N'CategoryId') IS NULL
   OR COL_LENGTH(N'dbo.Groomers', N'IsActive') IS NULL
   OR COL_LENGTH(N'dbo.Groomers', N'PriceUnit') IS NULL
   OR COL_LENGTH(N'dbo.Services', N'BillingUnit') IS NULL
  THROW 50010, N'Faltan columnas. Ejecuta primero Scripts/alta-negocios-esquema.sql', 1;

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

DECLARE @PwdHash nvarchar(64) =
  CONVERT(nvarchar(64), HASHBYTES(N'SHA2_256', CONVERT(varchar(100), 'pawcare:123456')), 2);

DECLARE @GroomingId int = (SELECT TOP 1 Id FROM dbo.Categories WHERE Slug = N'grooming');
DECLARE @HotelId    int = (SELECT TOP 1 Id FROM dbo.Categories WHERE Slug = N'hotel');
DECLARE @VetId      int = (SELECT TOP 1 Id FROM dbo.Categories WHERE Slug = N'vet');
DECLARE @DaycareId  int = (SELECT TOP 1 Id FROM dbo.Categories WHERE Slug = N'daycare');
DECLARE @WalkersId  int = (SELECT TOP 1 Id FROM dbo.Categories WHERE Slug = N'walkers');
DECLARE @TrainersId int = (SELECT TOP 1 Id FROM dbo.Categories WHERE Slug = N'trainers');

BEGIN TRAN;

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
    IF NOT EXISTS (SELECT 1 FROM dbo.DayAvailabilities WHERE GroomerId = @gHotel AND [Day] = DATEADD(DAY, @i, CAST(GETDATE() AS date)))
      INSERT INTO dbo.DayAvailabilities (GroomerId, [Day], IsAvailable, Note)
      VALUES (@gHotel, DATEADD(DAY, @i, CAST(GETDATE() AS date)),
              CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 80 THEN 1 ELSE 0 END, N'SQL');
    SET @i += 1;
  END
  PRINT N'Hotel: hotel@chombly.com / 123456';
END

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

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'daycare@chombly.com')
BEGIN
  INSERT INTO dbo.Users (FullName, Email, PasswordHash, Phone, City, Role, CreatedAt)
  VALUES (N'Maya Daycare', N'daycare@chombly.com', @PwdHash, N'704-555-1004', N'Charlotte, NC', 1, SYSUTCDATETIME());
  DECLARE @uD int = SCOPE_IDENTITY();

  INSERT INTO dbo.Groomers (
    UserId, CategoryId, BusinessName, Type, Address, City, Latitude, Longitude,
    About, ImageUrl, Phone, AcceptsSeniorDogs, AcceptsAnxiousDogs, AcceptedSpecies,
    IsVerified, IsFeatured, IsActive, Rating, ReviewCount, StartingPrice, PriceUnit)
  VALUES (
    @uD, @DaycareId, N'Happy Paws Playhouse', 0,
    N'400 Play St, Charlotte, NC', N'Charlotte, NC', 35.2250, -80.8450,
    N'Daycare con áreas de juego, siesta y cámaras en vivo.', N'/images/categories/cat-daycare.png', N'704-555-1004',
    1, 1, N'Perro,Gato', 1, 1, 1, 4.9, 128, 35, N'/ día');
  DECLARE @gD int = SCOPE_IDENTITY();

  INSERT INTO dbo.Services (GroomerId, Name, Description, PriceSmall, PriceMedium, PriceLarge, PriceGiant, DurationMinutes, Icon, BillingUnit)
  VALUES
    (@gD, N'Día completo', N'7 AM – 7 PM', 35, 40, 45, 50, 720, N'daycare', N'dia'),
    (@gD, N'Medio día', N'Hasta 5 horas', 25, 28, 32, 35, 300, N'daycare', N'medio');

  INSERT INTO dbo.Amenities (GroomerId, Label, Icon, SortOrder) VALUES
    (@gD, N'Áreas de juego', N'play', 1),
    (@gD, N'Siesta / descanso', N'nap', 2),
    (@gD, N'Cámaras en vivo', N'cam', 3),
    (@gD, N'Acepta perros grandes', N'paw', 4);

  INSERT INTO dbo.ServiceExtras (GroomerId, Name, Description, Price, IsActive) VALUES
    (@gD, N'Transporte (ida y vuelta)', NULL, 15, 1),
    (@gD, N'Baño al final del día', NULL, 20, 1),
    (@gD, N'Administración de medicamentos', NULL, 10, 1);

  DECLARE @j int = 0;
  WHILE @j < 14
  BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.DayAvailabilities WHERE GroomerId = @gD AND [Day] = DATEADD(DAY, @j, CAST(GETDATE() AS date)))
      INSERT INTO dbo.DayAvailabilities (GroomerId, [Day], IsAvailable, Note)
      VALUES (@gD, DATEADD(DAY, @j, CAST(GETDATE() AS date)),
              CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 85 THEN 1 ELSE 0 END, N'SQL');
    SET @j += 1;
  END
  PRINT N'Daycare: daycare@chombly.com / 123456';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'walker@chombly.com')
BEGIN
  INSERT INTO dbo.Users (FullName, Email, PasswordHash, Phone, City, Role, CreatedAt)
  VALUES (N'Ana López', N'walker@chombly.com', @PwdHash, N'704-555-1005', N'Charlotte, NC', 1, SYSUTCDATETIME());
  DECLARE @uW int = SCOPE_IDENTITY();

  INSERT INTO dbo.Groomers (
    UserId, CategoryId, BusinessName, Type, Address, City, Latitude, Longitude,
    About, ImageUrl, Phone, AcceptsSeniorDogs, AcceptsAnxiousDogs, AcceptedSpecies,
    IsVerified, IsFeatured, IsActive, Rating, ReviewCount, StartingPrice, PriceUnit)
  VALUES (
    @uW, @WalkersId, N'Ana López', 1,
    N'Charlotte, NC', N'Charlotte, NC', 35.2280, -80.8400,
    N'Paseos individuales con foto del paseo. Acepta perros grandes. Sin escaleras.', N'/images/categories/cat-walkers.png', N'704-555-1005',
    1, 1, N'Perro', 1, 1, 1, 4.9, 128, 20, N'/ 60 min');
  DECLARE @gW int = SCOPE_IDENTITY();

  INSERT INTO dbo.Services (GroomerId, Name, Description, PriceSmall, PriceMedium, PriceLarge, PriceGiant, DurationMinutes, Icon, BillingUnit)
  VALUES
    (@gW, N'Paseo 30 min', N'Paseo corto', 12, 14, 16, 18, 30, N'walk', N'paseo'),
    (@gW, N'Paseo 60 min', N'Paseo estándar', 20, 22, 25, 28, 60, N'walk', N'paseo'),
    (@gW, N'Paseo 90 min', N'Paseo largo', 28, 32, 36, 40, 90, N'walk', N'paseo'),
    (@gW, N'Paseo 120 min', N'Paseo extendido', 36, 40, 45, 50, 120, N'walk', N'paseo');

  INSERT INTO dbo.Amenities (GroomerId, Label, Icon, SortOrder) VALUES
    (@gW, N'Paseo individual', N'one', 1),
    (@gW, N'Acepta perros grandes', N'paw', 2),
    (@gW, N'Sin escaleras', N'stairs', 3),
    (@gW, N'Foto del paseo', N'photo', 4);

  DECLARE @k int = 0;
  WHILE @k < 14
  BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.DayAvailabilities WHERE GroomerId = @gW AND [Day] = DATEADD(DAY, @k, CAST(GETDATE() AS date)))
      INSERT INTO dbo.DayAvailabilities (GroomerId, [Day], IsAvailable, Note)
      VALUES (@gW, DATEADD(DAY, @k, CAST(GETDATE() AS date)), 1, N'SQL');
    SET @k += 1;
  END
  PRINT N'Walker: walker@chombly.com / 123456';
END

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'trainer@chombly.com')
BEGIN
  INSERT INTO dbo.Users (FullName, Email, PasswordHash, Phone, City, Role, CreatedAt)
  VALUES (N'Juan Martínez', N'trainer@chombly.com', @PwdHash, N'704-555-1006', N'Charlotte, NC', 1, SYSUTCDATETIME());
  DECLARE @uT int = SCOPE_IDENTITY();

  INSERT INTO dbo.Groomers (
    UserId, CategoryId, BusinessName, Type, Address, City, Latitude, Longitude,
    About, ImageUrl, Phone, AcceptsSeniorDogs, AcceptsAnxiousDogs, AcceptedSpecies,
    IsVerified, IsFeatured, IsActive, Rating, ReviewCount, StartingPrice, PriceUnit)
  VALUES (
    @uT, @TrainersId, N'Juan Martínez', 2,
    N'Charlotte, NC', N'Charlotte, NC', 35.2200, -80.8350,
    N'Entrenador certificado. Obediencia, cachorros y modificación de conducta. A domicilio, centro o virtual. Clases en el parque. Flexible en horario.',
    N'/images/categories/cat-trainers.png', N'704-555-1006',
    1, 1, N'Perro', 1, 1, 1, 4.9, 128, 45, N'/ sesión');
  DECLARE @gT int = SCOPE_IDENTITY();

  INSERT INTO dbo.Services (GroomerId, Name, Description, PriceSmall, PriceMedium, PriceLarge, PriceGiant, DurationMinutes, Icon, BillingUnit)
  VALUES
    (@gT, N'Obediencia básica', N'Sesión individual', 45, 50, 55, 60, 60, N'train', N'sesion'),
    (@gT, N'Cachorros', N'Socialización y bases', 40, 45, 50, 55, 45, N'train', N'sesion'),
    (@gT, N'Modificación de conducta', N'Conducta reactiva o ansiedad', 55, 60, 65, 70, 60, N'train', N'sesion'),
    (@gT, N'Entrenamiento avanzado', N'Señales avanzadas', 60, 65, 70, 75, 60, N'train', N'sesion');

  INSERT INTO dbo.Amenities (GroomerId, Label, Icon, SortOrder) VALUES
    (@gT, N'Entrenador con certificación', N'cert', 1),
    (@gT, N'Experiencia con mi raza', N'breed', 2),
    (@gT, N'Clases en el parque', N'park', 3),
    (@gT, N'Flexible en horario', N'flex', 4),
    (@gT, N'A domicilio', N'home', 5),
    (@gT, N'En centro', N'center', 6),
    (@gT, N'Virtual', N'virtual', 7);

  DECLARE @t int = 0;
  WHILE @t < 14
  BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.DayAvailabilities WHERE GroomerId = @gT AND [Day] = DATEADD(DAY, @t, CAST(GETDATE() AS date)))
      INSERT INTO dbo.DayAvailabilities (GroomerId, [Day], IsAvailable, Note)
      VALUES (@gT, DATEADD(DAY, @t, CAST(GETDATE() AS date)), 1, N'SQL');
    SET @t += 1;
  END
  PRINT N'Trainer: trainer@chombly.com / 123456';
END

UPDATE dbo.Groomers
SET AcceptedSpecies = N'Perro,Gato,Conejo,Cobaya,Ave,Hurón,Otro'
WHERE AcceptedSpecies IS NULL OR LTRIM(RTRIM(AcceptedSpecies)) = N'';

COMMIT TRAN;
PRINT N'Listo.';
