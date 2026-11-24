using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DawaiSehat.Reminder.Messaging;
using DawaiSehat.Reminder.Messaging.Interfaces;

namespace DawaiSehat.Reminder.Tests.Messaging;

// Test plan: WA-12. The endpoint works with the fake sender; Meta is never called.
public class WhatsappTemplateEndpointTests
{
    private sealed class RecordingSender : IWhatsappSender
    {
        public TemplateMessage? Template;
        public Task SendMessageAsync(string phoneNumber, string message) => Task.CompletedTask;
        public Task SendTemplateAsync(string phoneNumber, TemplateMessage template)
        {
            Template = template;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DoseReminderEndpoint_UseFake_Returns200AndSendsTemplate()
    {
        var sender = new RecordingSender();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("WhatsApp:UseFake", "true");
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IWhatsappSender>(sender)));
        });

        var response = await factory.CreateClient().PostAsync(
            "/send-template/dose-reminder?phoneNumber=920000000001&patientName=Ammi&medicines=Panadol%20500mg,%20Disprin", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sender.Template!.Name.Should().Be(TemplateNames.DoseReminder);
        sender.Template.BodyParameters.Should().Equal("Ammi", "Panadol 500mg, Disprin");
    }

    private static WebApplicationFactory<Program> CreateFake(RecordingSender sender) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("WhatsApp:UseFake", "true");
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IWhatsappSender>(sender)));
        });

    // Test plan: WA-13
    [Theory]
    [InlineData("920000000001", "%20", "Panadol")]      // empty patient name
    [InlineData("920000000001", "Ammi", "%20")]          // empty medicines
    public async Task DoseReminderEndpoint_EmptyValue_Returns400WithShortMessage(string phone, string patient, string medicines)
    {
        var sender = new RecordingSender();
        using var factory = CreateFake(sender);

        var response = await factory.CreateClient().PostAsync(
            $"/send-template/dose-reminder?phoneNumber={phone}&patientName={patient}&medicines={medicines}", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("   at ");
        sender.Template.Should().BeNull();
    }

    // Test plan: WA-13
    [Theory]
    [InlineData("03001234567")]
    [InlineData("92300123456")]
    [InlineData("9230012345678")]
    [InlineData("92abcdefghij")]
    public async Task DoseReminderEndpoint_BadPhoneNumber_Returns400(string phone)
    {
        var sender = new RecordingSender();
        using var factory = CreateFake(sender);

        var response = await factory.CreateClient().PostAsync(
            $"/send-template/dose-reminder?phoneNumber={phone}&patientName=Ammi&medicines=Panadol", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        sender.Template.Should().BeNull();
    }

    // Test plan: WA-20
    [Theory]
    [InlineData("/send-message?phoneNumber=920000000001&medicine=Panadol")]
    [InlineData("/send-template/dose-reminder?phoneNumber=920000000001&patientName=Ammi&medicines=Panadol")]
    public async Task ManualEndpoints_ProductionWithRealMeta_Return404(string url)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("WhatsApp:UseFake", "false");
            b.UseSetting("WhatsApp:PhoneNumberId", "000000000000000");
            b.UseSetting("WhatsApp:Token", "test-token");
            b.UseSetting("WhatsApp:VerifyToken", "test-verify-token");
            b.UseSetting("WhatsApp:AppSecret", "test-app-secret");
            // Production requires a connection string at start-up; nothing connects to it in this test.
            b.UseSetting("ConnectionStrings:reminder-db", "Host=localhost;Database=unused;Username=u;Password=p");
        });

        var response = await factory.CreateClient().PostAsync(url, null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
