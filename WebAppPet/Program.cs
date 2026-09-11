using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebAppPet.Data;
using WebAppPet.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddLocalization();
builder.Services.AddRazorPages()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(WebAppPet.Localization.SharedResource));
    });
builder.Services.AddSingleton<IConfigureOptions<MvcOptions>, WebAppPet.Localization.ConfigureMvcLocalization>();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<GoogleMapsOptions>(builder.Configuration.GetSection(GoogleMapsOptions.SectionName));
builder.Services.AddHttpClient("nominatim", client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ChomblyPetCare/1.0 (https://chombly.app; places-suggest)");
    client.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddScoped<PlacesSuggestService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<PromoCodeService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<VetAuditService>();
builder.Services.AddScoped<ServiceCatalogService>();
builder.Services.AddScoped<VcprService>();
builder.Services.AddScoped<SafetyScreeningService>();
builder.Services.AddScoped<ConsentService>();
builder.Services.AddScoped<ConsultationFlowService>();
builder.Services.AddScoped<ChomblyCareService>();
builder.Services.AddScoped<BehaviorFlowService>();
builder.Services.AddScoped<CountryCatalogService>();
builder.Services.AddScoped<ProviderPayoutService>();
builder.Services.AddScoped<ProfessionalOnboardingService>();
builder.Services.AddScoped<ReminderEngineService>();
builder.Services.AddHostedService<ReminderBackgroundService>();

var supportedCultures = new[] { new CultureInfo("es"), new CultureInfo("en") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("es");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta ConnectionStrings:DefaultConnection. " +
        "En Azure App Service > Configuration > Connection strings, agrega 'DefaultConnection' (tipo SQLAzure).");
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Local Mac/Linux: SQLite (no LocalDB). Azure/Windows: SQL Server.
    if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        logger.LogInformation("Conectando a SQL Server y asegurando esquema...");
        await DbInitializer.InitializeAsync(db);
        logger.LogInformation("Base de datos lista.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "ERROR al conectar/crear la base de datos. " +
            "Revisa: 1) Connection string DefaultConnection en Azure, " +
            "2) Firewall de Azure SQL (Allow Azure services), " +
            "3) Usuario/password correctos.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// wwwroot + /uploads desde carpeta persistente (Azure %HOME%/data)
UploadPaths.MapUploadStaticFiles(app);

app.UseRouting();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

// Entrada pública: invitados ven la presentación; usuarios logueados van al marketplace.
app.MapGet("/", (HttpContext ctx) =>
    Results.Redirect(ctx.User.Identity?.IsAuthenticated == true ? "/Index" : "/Welcome"));

// City/address suggestions (Google Maps when key is set; OSM Nominatim fallback otherwise).
app.MapGet("/api/places/suggest", async (
    string? q,
    string? type,
    PlacesSuggestService places,
    CancellationToken ct) =>
{
    var mode = (type ?? "city").Trim().ToLowerInvariant();
    var items = mode == "address"
        ? await places.SuggestAddressesAsync(q ?? "", ct)
        : await places.SuggestCitiesAsync(q ?? "", ct);

    return Results.Json(items.Select(i => new
    {
        label = i.Label,
        city = i.City,
        address = i.Address,
        lat = i.Lat,
        lng = i.Lng
    }));
});

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
