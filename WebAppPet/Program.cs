using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddLocalization();
builder.Services.AddRazorPages()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<GoogleMapsOptions>(builder.Configuration.GetSection(GoogleMapsOptions.SectionName));
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
    options.UseSqlServer(connectionString));

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
app.UseRouting();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
