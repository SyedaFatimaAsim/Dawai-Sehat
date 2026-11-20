# Dawai Sehat

WhatsApp medicine reminder agent for elderly patients in Pakistan. It sends dose reminders in Urdu (Roman Urdu), understands replies like "le li", and alerts a caregiver when a dose is missed.

This is a portfolio project. The owner (Fatima) reviews every change as architect and reviewer, and must be able to explain all code in interviews. Optimise for clarity and learning, not cleverness.

**The full product plan is in `docs/architecture.md`** (in English): features and phases, tech stack and why, the three services, Kafka topics, outbox, Redis, database tables and indexes, RAG, agent tools, safety rules and the 2-week build plan. Test cases are in `docs/testing/test-plan.md`. Read it before planning any feature, and follow its safety rules strictly.

Done so far: solution layout, Reminder service with WhatsApp send and webhook verify/receive. Not done yet: Care and Prescription services, PostgreSQL, Identity/JWT, Kafka, RAG, deployment. Follow the build plan in section 12 of `docs/architecture.md`, one small task per pull request. The main branch is `master`.

## Current state

- Solution layout follows `docs/architecture.md` section 4.4: `src/` (AppHost, ServiceDefaults, Contracts, Reminder) and `tests/` (Reminder.Tests)
- `DawaiSehat.Reminder` (the former `DawaiSehat.Api`): ASP.NET Core minimal API on .NET 10
- `DawaiSehat.AppHost` (.NET Aspire) starts the Reminder service; Care, Prescription, Postgres, Kafka and Redis are added by the tasks that need them
- `IWhatsappSender` with `FakeWhatsappSender` and `MetaWhatsappSender` (typed HttpClient, `IOptions<WhatsappOptions>`)
- `POST /send-message`: sends a reminder through the Meta WhatsApp Cloud API
- `GET /webhook`: Meta verification (hub.mode, hub.verify_token, hub.challenge)
- `POST /webhook`: receives Meta events; currently only logs the raw JSON
- `Domain/ReplyParser`: rule-based reading of replies (Roman Urdu and Urdu script) into `ReplyIntent` (Taken, Later, NotTaken, Unwell, Unknown); not yet connected to the webhook
- Local testing uses a Visual Studio Dev Tunnel and `src/DawaiSehat.Reminder/DawaiSehat.Reminder.http`

## Target architecture (build towards this step by step)

Three microservices in one repository, each its own ASP.NET Core project with its own PostgreSQL database (details in `docs/architecture.md`):

- `DawaiSehat.Care`: caregivers, patients, medications, schedules, JWT login
- `DawaiSehat.Prescription`: prescription photo extraction with Gemini + RAG (pgvector medicine catalog)
- `DawaiSehat.Reminder`: scheduler, WhatsApp send, webhook, reply parsing, missed-dose alerts (formerly `DawaiSehat.Api`)
- Plus: `DawaiSehat.Gateway` (YARP), `DawaiSehat.Contracts` (event records only), `DawaiSehat.AppHost` (.NET Aspire), `DawaiSehat.ServiceDefaults`, React dashboard in `web/`

Keep it simple:
- One project per service with feature folders and a `Domain/` folder; one test project per service. No four-project Clean Architecture inside a service.
- Services talk through Kafka events with an outbox; never read another service's database.
- No new service without an ADR in `docs/decisions/`.

Do not jump ahead. Add a technology only when the current task needs it.

## Rules

- Never put secrets (tokens, verify token, connection strings with passwords) in code, appsettings, `.http` files or commits. Use `dotnet user-secrets` locally.
- Never commit real phone numbers. Use `92XXXXXXXXXX` placeholders.
- Keep `WhatsApp:UseFake` working so the app runs without Meta.
- Every new behaviour gets tests (unit, API or integration as fits). Run `dotnet build` and `dotnet test` before saying a task is done.
- Every pull request adds or updates its test cases in `docs/testing/test-plan.md` (ID, scenario, expected result, status, test name). Use fakes for WhatsApp and Gemini in tests; never call real external services in CI.
- Do not deploy, create cloud resources, or change AWS settings. Deployment is done by Fatima. Instead, keep `docs/deployment-handoff.md` current: Dockerfile, health endpoint, environment variable and secret names (never values), migrations, Kafka topics, Redis use.
- Deadline: a working application by 4 Dec 2026 (the build plan ends 2 Dec, leaving 2 days of buffer). Core features come before Extended ones (see section 2 and 12 of `docs/architecture.md`). Never merge half-finished features.
- Respond to Meta webhooks with 200 quickly; handle duplicate deliveries idempotently.
- Store times as UTC (`timestamptz`); show them in Asia/Karachi.

## Workflow

1. Start every feature in plan mode. Write the plan, list files to change, and wait for approval.
2. One task = one branch = one small pull request. Branch names: `feature/<short-name>`, `fix/<short-name>`.
3. Use the `developer` agent to implement and the `reviewer` agent to check before opening the PR.
4. Pull request description must include:
   - What changed and why
   - Architecture decisions and trade-offs
   - New concepts used, explained simply (Fatima is learning; she reviews in Roman Urdu/English)
   - How to test manually (`.http` requests)
5. Never merge, never push to `master`, never deploy. Fatima merges after review and deploys.

## Commands

- Build: `dotnet build`
- Test: `dotnet test`
- Run everything (Aspire): `dotnet run --project src/DawaiSehat.AppHost`
- Run Reminder only: `dotnet run --project src/DawaiSehat.Reminder`
