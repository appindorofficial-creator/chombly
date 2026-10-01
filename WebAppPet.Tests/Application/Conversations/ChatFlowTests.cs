using WebAppPet.Application.Conversations.GetInbox;
using WebAppPet.Application.Conversations.OpenConversation;
using WebAppPet.Application.Conversations.SendMessage;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Conversations;

public class ChatFlowTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _client;
    private readonly GroomerProfile _business;

    public ChatFlowTests()
    {
        _db = _database.CreateContext();
        _client = TestData.AddUser(_db);
        _business = TestData.AddBusiness(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private async Task<ChatThread?> Open(int userId, int? conversationId = null, int? appointmentId = null, int? groomerId = null)
    {
        await using var db = _database.CreateContext();
        return await new OpenConversationHandler(db).HandleAsync(
            new OpenConversationCommand(userId, groomerId ?? _business.Id, conversationId, appointmentId));
    }

    private async Task<SendMessageOutcome> Send(int userId, int conversationId, string? body)
    {
        await using var db = _database.CreateContext();
        return await new SendMessageHandler(db).HandleAsync(new SendMessageCommand(userId, conversationId, body));
    }

    private async Task<Conversation> StartConversation()
    {
        var thread = await Open(_client.Id);
        return thread!.Conversation;
    }

    private T Verify<T>(Func<AppDbContext, T> read)
    {
        using var db = _database.CreateContext();
        return read(db);
    }

    [Fact]
    public async Task A_client_writing_to_a_business_for_the_first_time_starts_a_conversation()
    {
        var appointment = TestData.AddAppointment(_db, _client, _business, AppointmentStatus.Confirmed, DateTime.UtcNow.AddDays(2));

        var thread = await Open(_client.Id, appointmentId: appointment.Id);

        Assert.NotNull(thread);
        Assert.Equal(_business.Id, thread.Groomer.Id);
        Assert.Empty(thread.Messages);
        var saved = Verify(db => db.Conversations.Single());
        Assert.Equal(_client.Id, saved.ClientId);
        Assert.Equal(_business.Id, saved.GroomerId);
        Assert.Equal(appointment.Id, saved.AppointmentId);
    }

    [Fact]
    public async Task Opening_the_chat_again_resumes_the_same_conversation_and_links_the_appointment()
    {
        var first = await StartConversation();
        var appointment = TestData.AddAppointment(_db, _client, _business, AppointmentStatus.Confirmed, DateTime.UtcNow.AddDays(2));

        var again = await Open(_client.Id, appointmentId: appointment.Id);

        Assert.Equal(first.Id, again!.Conversation.Id);
        Assert.Equal(1, Verify(db => db.Conversations.Count()));
        Assert.Equal(appointment.Id, Verify(db => db.Conversations.Single().AppointmentId));
    }

    [Fact]
    public async Task A_business_cannot_start_a_conversation_with_itself()
    {
        var thread = await Open(_business.UserId);

        Assert.Null(thread);
        Assert.Equal(0, Verify(db => db.Conversations.Count()));
    }

    [Fact]
    public async Task The_business_opens_a_client_conversation_from_its_inbox()
    {
        var conversation = await StartConversation();

        var thread = await Open(_business.UserId, conversationId: conversation.Id);

        Assert.Equal(conversation.Id, thread!.Conversation.Id);
    }

    [Fact]
    public async Task Another_user_cannot_open_someone_elses_conversation()
    {
        var conversation = await StartConversation();
        await Send(_client.Id, conversation.Id, "Hola");
        var stranger = TestData.AddUser(_db);

        var thread = await Open(stranger.Id, conversationId: conversation.Id);

        Assert.Null(thread);
        Assert.False(Verify(db => db.ChatMessages.Single().IsRead));
    }

    [Fact]
    public async Task An_unknown_business_opens_nothing()
    {
        Assert.Null(await Open(_client.Id, groomerId: 99_999));
    }

    [Fact]
    public async Task Opening_marks_the_received_messages_as_read_but_not_the_own_ones()
    {
        var conversation = await StartConversation();
        await Send(_client.Id, conversation.Id, "¿Tienen cupo el sábado?");
        await Send(_business.UserId, conversation.Id, "Sí, a las 10.");

        var thread = await Open(_client.Id, conversationId: conversation.Id);

        Assert.Equal(new[] { "¿Tienen cupo el sábado?", "Sí, a las 10." }, thread!.Messages.Select(m => m.Body));
        var read = Verify(db => db.ChatMessages.ToDictionary(m => m.Body, m => m.IsRead));
        Assert.False(read["¿Tienen cupo el sábado?"]);
        Assert.True(read["Sí, a las 10."]);
    }

    [Fact]
    public async Task A_client_message_is_saved_and_notifies_the_business()
    {
        var conversation = await StartConversation();
        var before = Verify(db => db.Conversations.Single().LastMessageAt);

        var outcome = await Send(_client.Id, conversation.Id, "  ¿Bañan gatos?  ");

        Assert.Equal(SendMessageOutcome.Sent, outcome);
        var message = Verify(db => db.ChatMessages.Single());
        Assert.Equal("¿Bañan gatos?", message.Body);
        Assert.Equal(_client.Id, message.SenderUserId);
        Assert.True(Verify(db => db.Conversations.Single().LastMessageAt) >= before);
        var notification = Verify(db => db.Notifications.Single());
        Assert.Equal(_business.UserId, notification.UserId);
        Assert.Equal("Nuevo mensaje", notification.Title);
        Assert.Equal("¿Bañan gatos?", notification.Message);
        Assert.Equal("chat", notification.Type);
    }

    [Fact]
    public async Task A_business_reply_notifies_the_client()
    {
        var conversation = await StartConversation();

        await Send(_business.UserId, conversation.Id, "Sí, con cita previa.");

        Assert.Equal(_client.Id, Verify(db => db.Notifications.Single().UserId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_message_is_not_sent(string? body)
    {
        var conversation = await StartConversation();

        var outcome = await Send(_client.Id, conversation.Id, body);

        Assert.Equal(SendMessageOutcome.EmptyMessage, outcome);
        Assert.Equal(0, Verify(db => db.ChatMessages.Count()));
        Assert.Equal(0, Verify(db => db.Notifications.Count()));
    }

    [Fact]
    public async Task A_long_message_is_cut_and_the_notification_shows_a_preview()
    {
        var conversation = await StartConversation();

        await Send(_client.Id, conversation.Id, new string('a', 2500));

        Assert.Equal(SendMessageHandler.MaxLength, Verify(db => db.ChatMessages.Single().Body.Length));
        Assert.Equal(new string('a', 80) + "…", Verify(db => db.Notifications.Single().Message));
    }

    [Fact]
    public async Task Someone_outside_the_conversation_cannot_write_in_it()
    {
        var conversation = await StartConversation();
        var stranger = TestData.AddUser(_db);

        var outcome = await Send(stranger.Id, conversation.Id, "Hola");

        Assert.Equal(SendMessageOutcome.NotFound, outcome);
        Assert.Equal(0, Verify(db => db.ChatMessages.Count()));
    }

    [Fact]
    public async Task The_inbox_lists_the_user_conversations_with_the_latest_message_first()
    {
        var older = await StartConversation();
        await Send(_client.Id, older.Id, "Primer mensaje");
        await Send(_business.UserId, older.Id, "Respuesta más reciente");

        var otherBusiness = TestData.AddBusiness(_db);
        var newer = (await Open(_client.Id, groomerId: otherBusiness.Id))!.Conversation;
        await Send(_client.Id, newer.Id, "Hola otro negocio");

        var strangerChat = (await Open(TestData.AddUser(_db).Id))!.Conversation;

        var clientInbox = await new GetInboxHandler(_db).HandleAsync(new GetInboxQuery(_client.Id));

        Assert.Null(clientInbox.MyGroomerId);
        Assert.Equal(new[] { newer.Id, older.Id }, clientInbox.Items.Select(c => c.Id));
        Assert.Equal("Respuesta más reciente", clientInbox.LastPreview[older.Id]);
        Assert.DoesNotContain(clientInbox.Items, c => c.Id == strangerChat.Id);

        var businessInbox = await new GetInboxHandler(_db).HandleAsync(new GetInboxQuery(_business.UserId));

        Assert.Equal(_business.Id, businessInbox.MyGroomerId);
        Assert.Equal(new[] { older.Id, strangerChat.Id }.OrderBy(id => id), businessInbox.Items.Select(c => c.Id).OrderBy(id => id));
        Assert.Equal(_client.FullName, businessInbox.Items.Single(c => c.Id == older.Id).Client.FullName);
    }
}
