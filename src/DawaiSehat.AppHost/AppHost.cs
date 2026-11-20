var builder = DistributedApplication.CreateBuilder(args);

// One PostgreSQL server for the whole system, one database per service (architecture.md section 7).
// Only reminder_db exists so far; care_db and prescription_db are added by the tasks that need them.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume(); // keeps the data when the container restarts

// Aspire resource names allow only hyphens, so the resource is "reminder-db"; the real database is "reminder_db".
var reminderDb = postgres.AddDatabase("reminder-db", "reminder_db");

// WithReference gives the service its connection string (ConnectionStrings:reminder-db).
// WaitFor starts the service only after the database is ready.
builder.AddDotnetProject("reminder", "../DawaiSehat.Reminder/DawaiSehat.Reminder.csproj")
    .WithReference(reminderDb)
    .WaitFor(reminderDb);

builder.Build().Run();
