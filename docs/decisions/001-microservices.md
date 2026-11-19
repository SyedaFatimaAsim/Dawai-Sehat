# ADR 001: Three services, database per service, events between them

Status: Accepted · Date: 19 Nov 2026 · Owner: Fatima

## Context

Dawai Sehat has three kinds of work that change and fail for different reasons:

- **Care data** (caregivers, patients, medicines, alerts) is ordinary CRUD.
- **Reminders** must go out reliably every minute, even when other parts are down.
- **Prescription reading** is slow, AI-heavy and depends on an external model (Gemini).

A failure or slowdown in AI extraction must never stop a patient's reminder. This is also a portfolio project: it should show real distributed-systems skills (events, outbox, idempotency, database per service).

## Decision

Split by business capability into **three services**:

| Service | Owns | Database |
|---|---|---|
| Care | Caregivers, patients, medicines, schedules, appointments, alerts, stock | `care_db` |
| Prescription | Prescription photos, extracted drafts, medicine catalog, embeddings (RAG) | `prescription_db` |
| Reminder | Dose events, inbound messages, scheduler, WhatsApp send and webhook | `reminder_db` |

Rules that follow from this:

1. Each service is its own project, its own process, deployed on its own, with its own database on one Postgres server.
2. Services talk only through **Kafka events** (partition key = `patientId`). No service reads another service's database. No chains of HTTP calls between services.
3. Each service keeps the copy of data it needs. Reminder keeps `patient_schedules`, filled from events, so reminders work even if Care is down.
4. Every service publishes events through an **outbox table** (change and event saved in one transaction) and consumes them **idempotently** (`processed_events`).
5. `DawaiSehat.Contracts` holds event records only, no logic.
6. The gateway (YARP) and the dashboard hold no business logic.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| One monolith | Simplest, but an AI extraction problem could take reminders down, and it shows fewer distributed-systems skills |
| Many small services (one per feature) | Too much setup and review work for a 2-week build |
| Clean Architecture with four projects inside each service | Too many projects for the size; feature folders plus a `Domain/` folder are enough |

## Consequences

Costs we accept, and how we keep them small:

| Risk | Mitigation |
|---|---|
| Too many services | Only three. No new service without a new ADR |
| Too many projects | One project per service plus one test project, feature folders inside |
| Distributed data | Events plus local copies; no cross-service joins |
| Hard local setup | .NET Aspire starts everything with one command |
| Hard debugging | OpenTelemetry tracing through `ServiceDefaults` |
| Shared code coupling | `Contracts` contains event records only |

Business rules live in each service's `Domain/` folder and do not depend on EF Core, WhatsApp or Gemini, so they are unit tested on their own.
