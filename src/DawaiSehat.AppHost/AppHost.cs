var builder = DistributedApplication.CreateBuilder(args);

// Day 1: only the Reminder service. Care, Prescription, Postgres, Kafka and Redis
// are added by the tasks that need them (see docs/architecture.md section 12).
builder.AddDotnetProject("reminder", "../DawaiSehat.Reminder/DawaiSehat.Reminder.csproj");

builder.Build().Run();
