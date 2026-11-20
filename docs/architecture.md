# Dawai Sehat: Medication Follow-up Agent

Version 4 · 19 Nov 2026 · Fatima

## 1. Problem and solution

Dawai Sehat is a WhatsApp agent that reminds elderly patients in Urdu to take their medicine, understands their reply, alerts the family when a dose is missed, reads a doctor's prescription photo, and gives the caregiver a weekly summary.

**Who it is for**

- **Patient (elderly):** installs nothing. Gets a WhatsApp reminder and replies "le li" in text or a voice note.
- **Caregiver (son or daughter, often in another city or country):** sets up medicines on a web dashboard, sees daily status, gets alerts, and can ask questions about the patient's medicines.

**Why it matters:** existing apps (Medisafe, MyTherapy, Pillo) need an app download, cannot read handwritten Pakistani prescriptions, and family alerts are often paid. In Pakistan, older people use WhatsApp far more than apps.

**Hard limits (what it will never do)**

- No advice on medicines, doses or changes. It only follows the schedule from the prescription that the caregiver confirmed.
- No diagnosis.
- No emergency handling. If a patient says they feel unwell, the caregiver is alerted at once and the patient gets a fixed message: "Please contact your doctor. In an emergency, call 1122."

## 2. Features (all in the MVP)

All features are part of the MVP. They are built in two phases: **Core** first (the product works end to end), then **Extended**. If time runs out, Extended features move after the first release; Core never does.

| # | Feature | Phase | Service | Details |
|---|---|---|---|---|
| F1 | Caregiver sign-up and login | Core | Care | Email + password, JWT |
| F2 | Add patient | Core | Care | WhatsApp number, language (Urdu / English), time zone |
| F3 | Add medicine manually | Core | Care | Name, strength, dose, times, before / after food |
| F4 | Prescription photo to medicines (RAG) | Core | Prescription | AI reads the photo, matches against a medicine catalog, caregiver confirms every item |
| F5 | WhatsApp reminder | Core | Reminder | On time, approved template, all medicines due at that time in one message |
| F6 | Understand text replies | Core | Reminder | "le li", "haan", "baad mein", "nahi", "tabiyat kharab" |
| F7 | Snooze and second reminder | Core | Reminder | No reply in 30 minutes: remind once more |
| F8 | Missed-dose and unwell alerts | Core | Reminder → Care | Caregiver alerted on WhatsApp and dashboard |
| F9 | Caregiver dashboard | Core | Web | Today's status, 7-day adherence, alerts, prescription confirm screen |
| F10 | Urdu voice notes | Extended | Reminder | Voice note → intent with Gemini audio |
| F11 | Refill prediction | Extended | Care | Pill count goes down on each taken dose; "5 days left" alert |
| F12 | Doctor appointment reminder | Extended | Care → Reminder | Follow-up visit reminder on WhatsApp a day before |
| F13 | Ask about prescriptions (RAG Q&A) | Extended | Prescription | "What is Ammi's evening medicine?" answered from her own confirmed prescriptions |
| F14 | Weekly report agent | Extended | Care | Agent uses read-only tools to summarise adherence and patterns |
| F15 | MCP server | Extended | Care | Caregiver asks Claude Desktop about their patient through read-only tools |

## 3. Tech stack

Every technology has one real job. This table is the answer to "why is this here?" in an interview.

| Area | Technology | Job in this project |
|---|---|---|
| Language | C#, .NET 10, ASP.NET Core minimal APIs | All services |
| Database | **PostgreSQL** + EF Core (Npgsql) | One database per service, on one Postgres server |
| Vector search | **pgvector** (Postgres extension) | RAG: medicine catalog and prescription embeddings, no separate vector database |
| Messaging between services | **Kafka** (Confluent.Kafka) | Events between services; partition key = `patientId` |
| Reliable events | Outbox table in each service | Database change and event are saved together |
| Cache and locks | **Redis** | Scheduler lock and duplicate-webhook check |
| AI | Gemini via **Microsoft.Extensions.AI** | Prescription reading, embeddings, reply and voice understanding, agent tool calling |
| AI protocol | MCP C# SDK | Read-only tools for Claude Desktop |
| WhatsApp | Meta WhatsApp Business Cloud API | Send reminders, receive replies on a webhook |
| Auth | ASP.NET Core Identity + JWT | Caregiver login; every service validates the JWT |
| API gateway | **YARP** | One URL for the dashboard; routes to services |
| Frontend | React, Vite, TypeScript, Tailwind, TanStack Query | Caregiver dashboard |
| Local setup | **.NET Aspire** + Docker | One command starts all services, Postgres, Kafka, Redis |
| Testing | xUnit, FluentAssertions, Testcontainers, WebApplicationFactory, Vitest + Playwright (web) | See `docs/testing/test-plan.md` |
| Cloud | **AWS** (deployment owned by Fatima) | See section 9 |
| CI | GitHub Actions | Build and run all tests on every pull request |

## 4. Architecture

### 4.1 Services

Three microservices, split by business capability. Each is its own project, runs as its own process, is deployed on its own, and owns its own database.

| Service | Owns | Main jobs | Database |
|---|---|---|---|
| **Care Service** | Caregivers, patients, medicines, schedules, appointments, alerts, stock | Login (JWT), patients, medicines, confirm prescription drafts, refill prediction, weekly report agent, MCP server | `care_db` |
| **Prescription Service** | Prescription photos, extracted drafts, medicine catalog, prescription embeddings | Extract with Gemini vision, match with RAG, RAG Q&A on a patient's prescriptions | `prescription_db` (+ pgvector) |
| **Reminder Service** | Dose events, inbound messages | Scheduler, WhatsApp send, webhook, text and voice reply understanding, snooze, missed / unwell detection, appointment reminders | `reminder_db` |

Two thin pieces hold no business logic:

- **Gateway (YARP):** the dashboard calls one URL; the gateway routes to the right service.
- **Dashboard (React):** the caregiver's web app.

```text
                     ┌──────────────┐
   Caregiver ──────► │  Dashboard   │
                     └──────┬───────┘
                            │ HTTPS + JWT
                     ┌──────▼───────┐
                     │ Gateway(YARP)│
                     └──┬────┬────┬─┘
            ┌───────────┘    │    └────────────┐
     ┌──────▼──────┐  ┌──────▼───────┐  ┌──────▼──────┐
     │ Care        │  │ Prescription │  │ Reminder    │◄──── Meta webhook
     │ Service     │  │ Service (RAG)│  │ Service     │────► WhatsApp API
     └──────┬──────┘  └──────┬───────┘  └──────┬──────┘
         care_db      prescription_db       reminder_db     (one Postgres server)
            │                │                 │
            └──────── Kafka (events) ──────────┘            Redis: lock + dedup
```

### 4.2 How services talk

1. **Dashboard to services:** HTTP through the gateway.
2. **Service to service:** Kafka events only. No service reads another service's database, and no chains of HTTP calls between services.
3. **Each service keeps the copy it needs.** The Reminder Service keeps its own table of schedules, filled from events, so reminders go out even if the Care Service is down.

| Topic | Events | Published by | Consumed by |
|---|---|---|---|
| `care.patients` | `PatientRegistered`, `PatientDeactivated` | Care | Reminder |
| `care.medications` | `MedicationScheduleChanged`, `AppointmentScheduled` | Care | Reminder |
| `prescriptions` | `PrescriptionDraftReady`, `PrescriptionConfirmed` | Prescription, Care | Care, Prescription (to index for Q&A) |
| `doses` | `DoseTaken`, `DoseMissed`, `PatientUnwell` | Reminder | Care (alerts, stock, stats) |

- **Partition key = `patientId`:** all events of one patient stay in order.
- **Idempotent consumers:** each consumer records processed event IDs in `processed_events` and skips duplicates.
- **Outbox:** the change and the event are saved in one transaction (`outbox_messages`); a background job publishes pending rows.

### 4.3 Inside one service (kept simple)

Each service is **one ASP.NET Core project plus one test project**, organised by feature folders.

```text
DawaiSehat.Reminder/
  Features/
    SendReminders/      scheduler, WhatsApp template sender
    ReceiveReplies/     webhook endpoint, text and voice reply parsing
    MissedDoses/        second reminder, missed / unwell events
    Appointments/       appointment reminders
  Domain/               DoseEvent and its rules (MarkTaken, MarkMissed)
  Data/                 DbContext, migrations, outbox, processed events
  Program.cs
DawaiSehat.Reminder.Tests/
```

**Business rules live in `Domain/` and do not depend on EF Core, WhatsApp or Gemini**, so they are unit tested on their own. External systems sit behind interfaces (`IWhatsappSender`, `IReplyClassifier`, `IVoiceTranscriber`).

### 4.4 Solution layout

```text
DawaiSehat/
  src/
    DawaiSehat.AppHost/          .NET Aspire: starts everything locally
    DawaiSehat.ServiceDefaults/  logging, health checks, OpenTelemetry
    DawaiSehat.Contracts/        shared event records only (no logic)
    DawaiSehat.Gateway/          YARP
    DawaiSehat.Care/
    DawaiSehat.Prescription/
    DawaiSehat.Reminder/         the current DawaiSehat.Api moves here
  tests/
    DawaiSehat.Care.Tests/
    DawaiSehat.Prescription.Tests/
    DawaiSehat.Reminder.Tests/
    DawaiSehat.IntegrationTests/ cross-service flows with Testcontainers
  web/                           React dashboard (+ Vitest, Playwright)
  docs/
    architecture.md
    decisions/                   ADRs
    testing/                     test plan and test reports
    deployment-handoff.md        what Fatima needs to deploy each service
```

### 4.5 Life of one reminder

1. Every minute the Reminder Service scheduler takes a Redis lock and picks due doses (`dose_events`, status Pending).
2. It sends an approved WhatsApp template and increments `reminders_sent`.
3. The patient replies "le li" (text or voice). Meta posts it to the webhook.
4. The webhook verifies the signature, checks Redis for a duplicate message ID, saves the message, and returns 200 at once.
5. The reply is classified as TAKEN, LATER, NOT_TAKEN or UNWELL. Text: rules first, Gemini only when the rules do not understand. Voice: Gemini audio.
6. In one transaction: update the dose event and add the event to the outbox. The outbox job publishes it.
7. No reply in 30 minutes: second reminder. Still none: `DoseMissed` and a caregiver alert. UNWELL: `PatientUnwell`, immediate alert and the fixed safety message.

## 5. RAG

### 5.1 Prescription photo to medicines (F4)

Handwritten Pakistani prescriptions use brand names, short forms and messy handwriting. A vision model alone often misreads them. RAG checks every extracted name against a trusted medicine catalog before the caregiver sees it.

| Step | What happens |
|---|---|
| 1. Catalog (once) | A curated CSV of 100–300 common medicines in Pakistan: brand name, generic name, strengths, form. Each row embedded with a Gemini embedding model into `medicine_catalog` (pgvector) |
| 2. Extract | Gemini vision returns JSON per medicine: name as written, strength, quantity, times per day, duration, before / after food. Unreadable fields stay empty, never guessed |
| 3. Retrieve | For each name: top 5 catalog matches by vector search, combined with spelling-tolerant search (`pg_trgm`) |
| 4. Generate | A second Gemini call picks the best match **only from the retrieved candidates** and gives a confidence score |
| 5. Confirm | The dashboard shows each item with the match and confidence; low confidence is highlighted. Nothing is active until the caregiver confirms |

### 5.2 Ask about prescriptions (F13)

When a prescription is confirmed, its medicines and instructions are chunked and embedded per patient. The caregiver asks a question; retrieval is **always filtered by `patient_id` and the caregiver's own patients**; the model answers only from the retrieved chunks and cites which prescription it used. If nothing relevant is found, it says so.

### 5.3 RAG rules

- The model chooses only from retrieved rows; no invented medicine, dose or frequency.
- Photos are private in object storage, shown only through short-lived links.
- Every run is logged (`extraction_runs`, `qa_runs`: model, tokens, confidence).
- Accuracy is measured on an evaluation set (see the test plan) and reported in the README.

## 6. Agent and MCP (F14, F15)

**Principle:** sending reminders and recording doses is plain code, never AI. AI is used only for language, images, audio and multi-step summaries.

**Weekly report agent tools (all read-only):**

| Tool | Returns |
|---|---|
| `get_patient_schedule(patientId)` | Medicines and times |
| `get_dose_history(patientId, from, to)` | Taken, late, missed doses |
| `get_adherence_summary(patientId, days)` | Percentage per medicine and time of day |
| `get_refill_status(patientId)` | Days of medicine left |

The agent states patterns ("evening doses are often missed on weekends"), never causes or treatment. It has no tool to add, change or delete medicines and never messages the patient. The MCP server exposes the same read-only tools to Claude Desktop, scoped to the logged-in caregiver.

## 7. Data design (PostgreSQL)

One Postgres server, three databases. `snake_case` names, `timestamptz` (UTC) for every time, shown in the patient's time zone.

**care_db**

| Table | Key columns |
|---|---|
| `caregivers` | id, name, email, whatsapp_number (+ Identity tables) |
| `patients` | id, caregiver_id, name, whatsapp_number (unique), language, time_zone, is_active |
| `medications` | id, patient_id, name, generic_name, strength, dose_quantity, instructions, start_date, end_date, pills_in_stock, status (Draft / Confirmed) |
| `medication_schedules` | id, medication_id, time_of_day, days_of_week |
| `appointments` | id, patient_id, doctor_name, scheduled_at, notes |
| `caregiver_alerts` | id, patient_id, type (Missed / Unwell / RefillLow), created_at, seen_at |
| `dose_stats` | patient_id, date, scheduled, taken (filled from dose events) |
| `agent_runs` | id, patient_id, tool_calls_json, output_text, tokens_used, created_at |
| `outbox_messages`, `processed_events` | standard |

**prescription_db**

| Table | Key columns |
|---|---|
| `prescriptions` | id, patient_id, photo_key, doctor_name, issued_on, status |
| `extracted_items` | id, prescription_id, raw_text, matched_catalog_id, confidence, fields_json |
| `medicine_catalog` | id, brand_name, generic_name, strengths, form, embedding |
| `prescription_chunks` | id, patient_id, prescription_id, content, embedding |
| `extraction_runs`, `qa_runs` | id, model, tokens_used, created_at |
| `outbox_messages`, `processed_events` | standard |

**reminder_db**

| Table | Key columns |
|---|---|
| `patient_schedules` | patient_id, whatsapp_number, medication_name, time_of_day, days_of_week (copy from events) |
| `dose_events` | id, patient_id, scheduled_at, status (Pending / Taken / Late / Missed / Unwell), reminders_sent, responded_at, response_text, response_intent |
| `inbound_messages` | id, whatsapp_message_id (unique), from_number, patient_id (added with the patient lookup on Day 4), type (text / audio), body, media_id, sent_at (Meta time), received_at (our time) |
| `appointment_reminders` | id, patient_id, appointment_at, sent_at |
| `outbox_messages`, `processed_events` | standard |

**Indexes worth explaining in an interview**

| Where | What | Why |
|---|---|---|
| `dose_events` | unique (patient_id, scheduled_at) | The scheduler can run twice without creating two reminders |
| `dose_events` | partial index on scheduled_at where status = 'Pending' | The every-minute query stays fast |
| `inbound_messages` | unique whatsapp_message_id | Second wall against duplicate webhooks if Redis is down |
| `medicine_catalog`, `prescription_chunks` | HNSW index on embedding; GIN trigram on brand_name | Fast vector and spelling-tolerant search |

**Adherence query** (window-function practice; write the last column yourself):

```sql
WITH daily AS (
    SELECT patient_id, date AS dose_date, scheduled, taken
    FROM dose_stats
)
SELECT patient_id, dose_date, scheduled, taken,
       ________ AS rolling_7_day_pct
FROM daily
ORDER BY patient_id, dose_date;
```

## 8. Safety and privacy

- A medicine is active only after the caregiver confirms it.
- Reply intent is one of four values. If unsure, ask the patient a plain yes / no question.
- UNWELL: immediate caregiver alert and a fixed, non-AI safety message.
- A caregiver sees only their own patients: checked in every query, every RAG search, every agent and MCP tool.
- Send the LLM only what the task needs.
- Mask phone numbers in logs (`+92300*****67`).
- Consent: the first message explains the service; "STOP" ends reminders.
- Text in messages or on a prescription is data, never an instruction to the AI.
- README: "This is not a medical device and gives no medical advice."

## 9. Deployment (owned by Fatima)

**Claude Code does not deploy, create cloud resources, or change AWS settings.** Claude's job ends at code that builds, passes all tests, runs locally with Aspire, and is documented in `docs/deployment-handoff.md`.

What Claude provides for each service:

- A `Dockerfile` and a `/health` endpoint
- The list of environment variables and secrets it needs (names only, never values)
- Database migrations that can be run as a separate step
- Kafka topics and Redis usage it depends on

Suggested AWS target (Fatima decides):

| Need | AWS option | Budget note |
|---|---|---|
| Run containers | Amazon ECS on Fargate | Or one EC2 instance with Docker Compose to save credits |
| PostgreSQL + pgvector | Amazon RDS for PostgreSQL | Supports the pgvector extension |
| Prescription photos | Amazon S3 with pre-signed URLs | |
| Secrets | AWS Secrets Manager or SSM Parameter Store | Parameter Store is cheaper |
| Kafka | Container on ECS / EC2 | Amazon MSK is costly for a portfolio project |
| Redis | Container, or Amazon ElastiCache | |
| Logs and metrics | Amazon CloudWatch | |
| Public HTTPS for the webhook | Application Load Balancer | |

Code must not depend on a specific cloud: storage, secrets and messaging sit behind interfaces or configuration.

## 10. Testing

Full details and every test case: `docs/testing/test-plan.md`.

| Level | Tool | What it proves |
|---|---|---|
| Unit | xUnit, FluentAssertions | Domain rules, reply parser, schedule maths, validators |
| API | WebApplicationFactory | Endpoints, auth, validation, status codes |
| Integration | Testcontainers (Postgres, Kafka, Redis) | Migrations, outbox, idempotent consumers, real queries |
| End to end | Aspire + test harness, fake WhatsApp and fake Gemini | Reminder → reply → dose recorded → caregiver alert across services |
| Web | Vitest, Playwright | Components and the main caregiver flows |
| AI evaluation | Evaluation sets in `tests/eval/` | RAG extraction accuracy, reply classification accuracy, Q&A grounding |
| Safety | Dedicated test cases | Unwell flow, cross-caregiver data access, prompt injection |

Rules: every pull request adds or updates tests **and** their entries in the test plan; external services are faked in unit and integration tests; CI runs all tests on every pull request.

## 11. Why microservices, and how we keep them simple

**Decision:** three services split by business capability, each with its own database, talking through events.

**Why:** each part changes and fails for different reasons. Prescription reading is slow and AI-heavy; reminders must be reliable every minute; care data is ordinary CRUD. A failure in AI extraction must never stop reminders. It also shows real distributed-systems skills: events, outbox, idempotency, database per service.

| Risk | How we keep it small |
|---|---|
| Too many services | Only three. No new service without an ADR |
| Too many projects | One project per service, feature folders inside |
| Distributed data | Events plus a local copy; no cross-service joins |
| Hard local setup | .NET Aspire starts everything with one command |
| Hard debugging | OpenTelemetry tracing through ServiceDefaults |
| Shared code coupling | `Contracts` holds event records only |

Record this as ADR 001.

## 12. Build plan: 2 weeks (19 Nov to 2 Dec 2026)

Claude Code writes the code and tests; Fatima plans, reviews, merges and deploys. One task = one small pull request with its tests and test-plan rows. Several tasks run per day.

**Limits on speed (plan around them):**

- Fatima's review time: aim for small pull requests that take 15–30 minutes each to review.
- Claude Pro usage limits (5-hour and weekly). Plan mode first, small tasks, and no re-runs of large jobs.
- Things only Fatima can provide: Meta template approval (submit on day 1, can take a day or more), 20 prescription photos, 20 voice notes, medicine catalog review.

### Week 1: Core backend

| Day | Tasks |
|---|---|
| 1 (19 Nov) | ADR 001; solution layout (AppHost, ServiceDefaults, Contracts, test projects); move Api into Reminder Service; CI build + test. **Fatima:** submit reminder template to Meta |
| 2 (20 Nov) | Webhook parsing, signature check, `inbound_messages`; rule-based reply parser with tests |
| 3 (21 Nov) | Care Service: Identity + JWT, patients, medicines, schedules, validation |
| 4 (22 Nov) | Kafka + outbox in both services; schedule events consumed by Reminder (idempotent) |
| 5 (23 Nov) | Scheduler, dose events, template send, Redis lock, snooze, missed / unwell events, alerts |
| 6 (24 Nov) | Gateway (YARP); cross-service integration tests; E2E happy and missed paths |
| 7 (25 Nov) | Buffer, fixes, test report. **Fatima:** first AWS deploy of Care + Reminder |

### Week 2: Prescription RAG, dashboard, extended features

| Day | Tasks |
|---|---|
| 8 (26 Nov) | Prescription Service: catalog + embeddings in pgvector, photo upload, Gemini extraction |
| 9 (27 Nov) | Retrieval + constrained matching, draft → confirm, RAG evaluation run |
| 10 (28 Nov) | Dashboard: login, patients, medicines, today's status, alerts, prescription confirm |
| 11 (29 Nov) | Adherence chart; Dockerfiles, health checks, `deployment-handoff.md`. **Core complete** |
| 12 (30 Nov) | Extended: voice notes, Gemini fallback for text, refill prediction, appointment reminders |
| 13 (1 Dec) | Extended: RAG Q&A, weekly report agent, MCP server (read-only) |
| 14 (2 Dec) | Security tests, README, test report, demo video. **Fatima:** final deploy |

**Rule if a day slips:** Core first, always. Extended features move after the release in this order: MCP server, weekly report agent, RAG Q&A, appointment reminders, voice notes. A feature that is not finished is left out of `master`, not half merged.

## 13. Working with Claude Code

- Plan every task in plan mode with the `architect` agent. Fatima approves before any code.
- `developer` implements on a feature branch with tests; `reviewer` checks before the pull request.
- Each pull request: code, tests, test-plan update, and a description explaining what, why and new concepts.
- Fatima reviews line by line and merges. Claude never merges, pushes to `master`, or deploys.
