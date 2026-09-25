using WebAppPet.Application.Payments.AddPaymentMethod;
using WebAppPet.Application.Payments.DeletePaymentMethod;
using WebAppPet.Application.Payments.GetPaymentMethods;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Payments;

public class PaymentMethodHandlersTests
{
    private static Task<AddPaymentMethodResult> Add(AppDbContext db, int userId, string card = "4111111111111111", bool makeDefault = false) =>
        new AddPaymentMethodHandler(db, new KeyLocalizer())
            .HandleAsync(new AddPaymentMethodCommand(userId, card, "12/35", card.StartsWith("37") ? "1234" : "123", "Ana López", makeDefault));

    private static Task<List<WebAppPet.Models.PaymentMethod>> List(AppDbContext db, int userId) =>
        new GetPaymentMethodsHandler(db).HandleAsync(new GetPaymentMethodsQuery(userId));

    [Fact]
    public async Task The_first_card_becomes_the_default_and_later_ones_do_not()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var user = TestData.AddUser(db);

        Assert.True((await Add(db, user.Id)).Success);
        Assert.True((await Add(db, user.Id, "5500000000000004")).Success);

        var cards = await List(db, user.Id);
        Assert.Equal(2, cards.Count);
        Assert.Equal(("Visa", "1111", true), (cards[0].Brand, cards[0].Last4, cards[0].IsDefault));
        Assert.False(cards[1].IsDefault);
        Assert.Equal((12, 2035, "Ana López"), (cards[1].ExpMonth, cards[1].ExpYear, cards[1].HolderName));
    }

    [Fact]
    public async Task Making_a_card_default_unsets_the_previous_default()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var user = TestData.AddUser(db);
        await Add(db, user.Id);

        await Add(db, user.Id, "378282246310005", makeDefault: true);

        var cards = await List(db, user.Id);
        Assert.Equal("Amex", Assert.Single(cards, c => c.IsDefault).Brand);
    }

    [Fact]
    public async Task An_unknown_brand_is_stored_with_the_localized_label()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var user = TestData.AddUser(db);

        await Add(db, user.Id, "9999999999999999");

        Assert.Equal("Pay_BrandUnknown", Assert.Single(await List(db, user.Id)).Brand);
    }

    [Fact]
    public async Task An_invalid_card_is_rejected_and_nothing_is_saved()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var user = TestData.AddUser(db);

        var result = await Add(db, user.Id, "4111");

        Assert.Equal(CardValidator.FailReason.CardNumberInvalid, result.Error);
        Assert.Empty(await List(db, user.Id));
    }

    [Fact]
    public async Task Only_the_owner_can_delete_a_card()
    {
        using var database = new TestDatabase();
        using var db = database.CreateContext();
        var owner = TestData.AddUser(db);
        var other = TestData.AddUser(db);
        await Add(db, owner.Id);
        var cardId = (await List(db, owner.Id))[0].Id;
        var delete = new DeletePaymentMethodHandler(db);

        Assert.False(await delete.HandleAsync(new DeletePaymentMethodCommand(other.Id, cardId)));
        Assert.Single(await List(db, owner.Id));

        Assert.True(await delete.HandleAsync(new DeletePaymentMethodCommand(owner.Id, cardId)));
        Assert.Empty(await List(db, owner.Id));
    }
}
