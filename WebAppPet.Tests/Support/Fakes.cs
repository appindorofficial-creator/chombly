using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using WebAppPet.Localization;

namespace WebAppPet.Tests.Support;

/// <summary>Returns the resource key as the value so tests can assert which message was chosen.</summary>
public sealed class KeyLocalizer : IStringLocalizer<SharedResource>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, name);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
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
