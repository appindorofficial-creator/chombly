using Microsoft.AspNetCore.Mvc.ModelBinding;
using WebAppPet.Application.Accounts.Shared;

namespace WebAppPet.Infrastructure.Web;

/// <summary>
/// Binds a coordinate posted as "4.711" or "4,711" regardless of the request culture. The default
/// binder under "es" reads "." as a thousands separator and turns 4.711000 into 4711000.
/// </summary>
public sealed class InvariantCoordinateBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);
        var raw = value.FirstValue;
        if (string.IsNullOrWhiteSpace(raw)) return Task.CompletedTask;

        if (Coordinates.TryParse(raw, out var parsed))
            bindingContext.Result = ModelBindingResult.Success(parsed);
        else
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Invalid coordinate '{raw}'.");
        return Task.CompletedTask;
    }
}
