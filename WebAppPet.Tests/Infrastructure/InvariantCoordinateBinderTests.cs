using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using WebAppPet.Infrastructure.Web;

namespace WebAppPet.Tests.Infrastructure;

public class InvariantCoordinateBinderTests
{
    private static async Task<ModelBindingContext> BindAsync(string? posted)
    {
        var values = new Dictionary<string, StringValues>();
        if (posted is not null) values["Latitude"] = posted;

        var context = new DefaultModelBindingContext
        {
            ModelName = "Latitude",
            ModelState = new ModelStateDictionary(),
            ValueProvider = new FormValueProvider(
                BindingSource.Form, new FormCollection(values), new CultureInfo("es"))
        };
        await new InvariantCoordinateBinder().BindModelAsync(context);
        return context;
    }

    [Theory]
    [InlineData("4.711000", 4.711)]
    [InlineData("4,711", 4.711)]
    [InlineData("-74.0721", -74.0721)]
    public async Task Reads_dot_or_comma_as_the_decimal_point_under_spanish_culture(string posted, double expected)
    {
        var context = await BindAsync(posted);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal(expected, (double)context.Result.Model!, 6);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Leaves_the_property_untouched_when_nothing_is_posted(string? posted)
    {
        var context = await BindAsync(posted);

        Assert.False(context.Result.IsModelSet);
        Assert.Equal(0, context.ModelState.ErrorCount);
    }

    [Fact]
    public async Task Reports_garbage_as_a_model_error()
    {
        var context = await BindAsync("abc");

        Assert.False(context.Result.IsModelSet);
        Assert.Equal(1, context.ModelState.ErrorCount);
    }
}
