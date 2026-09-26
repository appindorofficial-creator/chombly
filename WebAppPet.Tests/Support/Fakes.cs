using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using WebAppPet.Localization;
using WebAppPet.Services;

namespace WebAppPet.Tests.Support;

/// <summary>Returns the resource key as the value so tests can assert which message was chosen.</summary>
public sealed class KeyLocalizer : IStringLocalizer<SharedResource>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, name);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}

public sealed class FakeEmailService : IEmailService
{
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        Sent.Add((to, subject, htmlBody));
        return Task.FromResult(true);
    }
}

public sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "WebAppPet.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    public static FakeHostEnvironment Production() => new(Environments.Production);
    public static FakeHostEnvironment Development() => new(Environments.Development);
}

public sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "WebAppPet.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "chombly-tests-wwwroot");
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
