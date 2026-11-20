using System.Net;
using System.Text;
using DawaiSehat.Reminder.Data;
using DawaiSehat.Reminder.Features.ReceiveReplies;
using DawaiSehat.Reminder.Tests.Webhook;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace DawaiSehat.Reminder.Tests.Integration;

// Test plan: DB-01, DB-02, DB-03, DB-04, WH-04 and WH-08 against a real PostgreSQL (Testcontainers).
// Run only these with: dotnet test --filter "Category=Integration"
[Collection("postgres")]
[Trait("Category", "Integration")]
public class InboundMessagesDatabaseTests(PostgresFixture postgres)
{
    private const string AppSecret = "test-app-secret";
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

    private static InboundMessage Text(string id, string body = "le li") =>
        new(id, "920000000001", InboundMessageType.Text, body, null, Now.AddMinutes(-5), Now);

    [Fact] // DB-01
    public async Task Migrate_EmptyDatabase_CreatesTableWithUniqueIndexOnMessageId()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var db = PostgresFixture.CreateContext(connectionString);

        var indexDefinition = await db.Database
            .SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'inbound_messages' AND indexname = 'ux_inbound_messages_whatsapp_message_id'")
            .SingleAsync();

        indexDefinition.Should().Contain("UNIQUE").And.Contain("whatsapp_message_id");
    }

    [Fact] // DB-02
    public async Task TryAdd_NewMessage_SavesAllFields()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var db = PostgresFixture.CreateContext(connectionString);
        var store = new PostgresInboundMessageStore(db);

        var added = await store.TryAddAsync(Text("wamid.1", "لے لی"), CancellationToken.None);

        added.Should().BeTrue();
        var row = await db.InboundMessages.AsNoTracking().SingleAsync();
        row.WhatsappMessageId.Should().Be("wamid.1");
        row.FromNumber.Should().Be("920000000001");
        row.Type.Should().Be("text");
        row.Body.Should().Be("لے لی");
        row.MediaId.Should().BeNull();
        row.SentAt.Should().Be(Now.AddMinutes(-5));
        row.ReceivedAt.Should().Be(Now);
    }

    [Fact] // DB-03
    public async Task TryAdd_AudioWithoutSentAt_SavesNullsAsNull()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var db = PostgresFixture.CreateContext(connectionString);
        var store = new PostgresInboundMessageStore(db);
        var audio = new InboundMessage("wamid.A", "920000000001", InboundMessageType.Audio, null, "media-42", null, Now);

        (await store.TryAddAsync(audio, CancellationToken.None)).Should().BeTrue();

        var row = await db.InboundMessages.AsNoTracking().SingleAsync();
        row.Type.Should().Be("audio");
        row.Body.Should().BeNull();
        row.MediaId.Should().Be("media-42");
        row.SentAt.Should().BeNull();
    }

    [Fact] // DB-04: many deliveries of the same message at the same moment -> exactly one wins
    public async Task TryAdd_SameMessageManyTimesInParallel_InsertsExactlyOnce()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = PostgresFixture.CreateContext(connectionString); // one context per "request"
            return await new PostgresInboundMessageStore(db).TryAddAsync(Text("wamid.RACE"), CancellationToken.None);
        }));

        results.Count(added => added).Should().Be(1);
        await using var check = PostgresFixture.CreateContext(connectionString);
        (await check.InboundMessages.CountAsync()).Should().Be(1);
    }

    // ---- the whole webhook, with the real database behind it ----

    private WebApplicationFactory<Program> CreateApp(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("WhatsApp:UseFake", "true");
            b.UseSetting("WhatsApp:AppSecret", AppSecret);
            b.UseSetting("ConnectionStrings:reminder-db", connectionString);
        });

    private static async Task<HttpStatusCode> Post(WebApplicationFactory<Program> app, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhook") { Content = new ByteArrayContent(bytes) };
        request.Headers.Add("X-Hub-Signature-256", WebhookSignature.Compute(bytes, AppSecret));
        return (await app.CreateClient().SendAsync(request)).StatusCode;
    }

    [Fact] // WH-04 (database version)
    public async Task Webhook_SignedTextMessage_IsSavedInInboundMessagesTable()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync(); // the app migrates it at startup (Development)
        using var app = CreateApp(connectionString);

        var status = await Post(app, MetaPayloads.Text("wamid.W1", "le li"));

        status.Should().Be(HttpStatusCode.OK);
        await using var db = PostgresFixture.CreateContext(connectionString);
        var row = await db.InboundMessages.AsNoTracking().SingleAsync();
        row.WhatsappMessageId.Should().Be("wamid.W1");
        row.Body.Should().Be("le li");
        row.SentAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000));
    }

    [Fact] // WH-08
    public async Task Webhook_SameMessageDeliveredTwice_LeavesOneRowAndBothReturn200()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var app = CreateApp(connectionString);
        var json = MetaPayloads.Text("wamid.W2", "le li");

        var first = await Post(app, json);
        var second = await Post(app, json);

        first.Should().Be(HttpStatusCode.OK);
        second.Should().Be(HttpStatusCode.OK);
        await using var db = PostgresFixture.CreateContext(connectionString);
        (await db.InboundMessages.CountAsync()).Should().Be(1);
    }
}
