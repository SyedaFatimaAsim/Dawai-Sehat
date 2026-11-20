using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DawaiSehat.Reminder.Data;

/// <summary>
/// Used only by the `dotnet ef migrations add` tool, which needs a DbContext but never connects to a database.
/// The app itself reads the real connection string from configuration (see Program.cs).
/// </summary>
public class ReminderDbContextFactory : IDesignTimeDbContextFactory<ReminderDbContext>
{
    public ReminderDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ReminderDbContext>()
            .UseNpgsql("Host=localhost;Database=reminder_db_design_time")
            .Options);
}
