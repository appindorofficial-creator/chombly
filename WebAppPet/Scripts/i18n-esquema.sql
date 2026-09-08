-- ============================================================
-- Chombly i18n: columnas EN + idioma preferido del usuario
-- Ejecutar en Azure SQL / LocalDB si la app no pudo aplicar el esquema.
-- Es idempotente (seguro re-ejecutar).
-- ============================================================

SET NOCOUNT ON;

-- Preferencia de idioma del usuario (es | en)
IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL AND COL_LENGTH(N'Users', N'PreferredLanguage') IS NULL
BEGIN
    ALTER TABLE [Users] ADD [PreferredLanguage] nvarchar(10) NOT NULL
        CONSTRAINT DF_Users_PreferredLanguage DEFAULT(N'es');
    PRINT 'Added Users.PreferredLanguage';
END
GO

-- Traducciones de categorías
IF OBJECT_ID(N'[Categories]', N'U') IS NOT NULL AND COL_LENGTH(N'Categories', N'NameEn') IS NULL
BEGIN
    ALTER TABLE [Categories] ADD [NameEn] nvarchar(80) NOT NULL
        CONSTRAINT DF_Categories_NameEn DEFAULT(N'');
    PRINT 'Added Categories.NameEn';
END
GO

IF OBJECT_ID(N'[Categories]', N'U') IS NOT NULL AND COL_LENGTH(N'Categories', N'SubtitleEn') IS NULL
BEGIN
    ALTER TABLE [Categories] ADD [SubtitleEn] nvarchar(160) NOT NULL
        CONSTRAINT DF_Categories_SubtitleEn DEFAULT(N'');
    PRINT 'Added Categories.SubtitleEn';
END
GO

-- Rellenar textos ES + EN por slug
UPDATE c
SET
    c.Name = CASE c.Slug
        WHEN N'grooming' THEN N'Peluquería'
        WHEN N'hotel' THEN N'Hotel'
        WHEN N'vet' THEN N'Veterinario'
        WHEN N'daycare' THEN N'Guardería'
        WHEN N'walkers' THEN N'Paseadores'
        WHEN N'trainers' THEN N'Entrenadores'
        ELSE c.Name
    END,
    c.NameEn = CASE c.Slug
        WHEN N'grooming' THEN N'Grooming'
        WHEN N'hotel' THEN N'Hotel'
        WHEN N'vet' THEN N'Veterinary'
        WHEN N'daycare' THEN N'Daycare'
        WHEN N'walkers' THEN N'Walkers'
        WHEN N'trainers' THEN N'Trainers'
        ELSE CASE WHEN NULLIF(LTRIM(RTRIM(c.NameEn)), N'') IS NULL THEN c.Name ELSE c.NameEn END
    END,
    c.Subtitle = CASE c.Slug
        WHEN N'grooming' THEN N'Baño, corte y estética'
        WHEN N'hotel' THEN N'Hospedaje para tu mascota'
        WHEN N'vet' THEN N'Consultas y cuidados'
        WHEN N'daycare' THEN N'Guardería diurna'
        WHEN N'walkers' THEN N'Paseos profesionales'
        WHEN N'trainers' THEN N'Entrenamiento'
        ELSE c.Subtitle
    END,
    c.SubtitleEn = CASE c.Slug
        WHEN N'grooming' THEN N'Bath, cut & styling'
        WHEN N'hotel' THEN N'Overnight pet boarding'
        WHEN N'vet' THEN N'Checkups & care'
        WHEN N'daycare' THEN N'Daytime pet care'
        WHEN N'walkers' THEN N'Professional dog walks'
        WHEN N'trainers' THEN N'Training sessions'
        ELSE CASE WHEN NULLIF(LTRIM(RTRIM(c.SubtitleEn)), N'') IS NULL THEN c.Subtitle ELSE c.SubtitleEn END
    END
FROM dbo.Categories c
WHERE OBJECT_ID(N'dbo.Categories', N'U') IS NOT NULL;

PRINT 'i18n schema ready.';
GO
