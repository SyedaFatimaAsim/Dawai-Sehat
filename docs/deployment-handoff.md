# Deployment hand-off

What Fatima needs to deploy each service. Claude keeps this file current and never deploys.
Secret and variable **names only, never values**. In .NET, `WhatsApp:Token` is set as the environment
variable `WhatsApp__Token` (double underscore). Locally use `dotnet user-secrets`.

Status: Reminder service only. Dockerfile, health endpoint for production and migrations are planned for Day 11.

## Reminder Service (`src/DawaiSehat.Reminder`)

| Item | Value |
|---|---|
| Dockerfile | Not yet (Day 11) |
| Health endpoint | `/health` and `/alive` exist but are **Development only** (ServiceDefaults). Must be enabled for production on Day 11 |
| Database | Not yet. Messages are in memory for now; PostgreSQL `reminder_db` comes next |
| Kafka / Redis | Not used yet |

### Settings and secrets

| Name | Secret? | Needed when | What it is |
|---|---|---|---|
| `WhatsApp__UseFake` | no | always | `true` = log messages only, no Meta. Must be `false` in production |
| `WhatsApp__Token` | **yes** | `UseFake=false` | Meta permanent access token |
| `WhatsApp__PhoneNumberId` | no | `UseFake=false` | Meta phone number ID |
| `WhatsApp__VerifyToken` | **yes** | `UseFake=false` | Token you choose; Meta sends it when registering the webhook (GET `/webhook`) |
| `WhatsApp__AppSecret` | **yes** | `UseFake=false` | Meta App Secret. Used to check `X-Hub-Signature-256` on every POST `/webhook`. Without it every Meta call gets 401 |
| `WhatsApp__ApiVersion` | no | optional | Default `v25.0` |

With `UseFake=false` the service **refuses to start** if Token, PhoneNumberId, VerifyToken or AppSecret is empty.

### Webhook behaviour that matters for the load balancer / firewall

- Public HTTPS URL for `GET` and `POST /webhook` (Meta calls it).
- `POST /webhook`: 401 = missing or wrong signature (or no `AppSecret`), 413 = body over 256 KB, 200 otherwise.
- Do not strip or re-encode the request body; the signature is checked over the raw bytes.
