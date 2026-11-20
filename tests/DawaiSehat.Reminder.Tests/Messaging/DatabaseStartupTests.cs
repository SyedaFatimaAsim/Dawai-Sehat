using Microsoft.AspNetCore.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DawaiSehat.Reminder.Tests.Messaging;

// Test plan: DB-07, DB-08. No Docker needed: these check start-up rules only.
public class DatabaseStartupTests
{
    [Fact] // DB-07
    public void Start_ProductionWithoutConnectionString_Fails()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("WhatsApp:UseFake", "true");
        });

        var act = () => factory.CreateClient();

        act.Should().Throw<Exception>().Where(e => e.ToString().Contains("ConnectionStrings:reminder-db"));
    }

    [Fact] // DB-08: local development without a database still starts (the webhook then fails with a clear log)
    public void Start_DevelopmentWithoutConnectionString_Starts()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("WhatsApp:UseFake", "true");
        });

        var act = () => factory.CreateClient();

        act.Should().NotThrow();
    }
}
