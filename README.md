# JobTracker

Multi-tenant job management for a roofing contractor. Office staff schedule,
assign and complete roofing jobs; completing one asynchronously raises an
invoice and notifies the customer through a transactional outbox.

Built as a technical assessment for a Senior Fullstack Engineer position.

---

## Run it

You need Docker. Nothing else — no .NET SDK, no Node, no credentials, no
database to create and no migration to apply.

```bash
docker compose up --build
```

Then open **<http://localhost:3000/jobs>**.

The first build takes a few minutes. When it finishes, three containers are
running: PostgreSQL, the .NET API on `:8080`, and the Next.js app on `:3000`.
The API applies both modules' migrations and seeds the crew and customer
rosters before it starts serving, so the interface works on the first click.

| | |
|---|---|
| Interface | <http://localhost:3000/jobs> |
| API | <http://localhost:8080> |
| OpenAPI | <http://localhost:8080/openapi/v1.json> |
| Background jobs | <http://localhost:8080/hangfire> — the outbox drain and every send. Development only, and refused from a public address |
| Database | `localhost:5432` — credentials below |

Watch the asynchronous half work: create a job, complete it, and within a few
seconds an invoice and two notifications appear.

```bash
docker compose exec postgres psql -U jobtracker -d jobtracker \
  -c "select recipient, status from jobs.notifications" \
  -c "select amount, job_id from billing.invoices"
```

Each notification leaves two traces. The row above, which moves from `Pending`
to `Sent`, and a line in the backend log:

```bash
docker compose logs backend | grep "Notification sent"
```

```
Notification sent to crew@example.com with subject Job completed
```

Delivery is simulated — there is no mail transport — so the table is the
evidence and the log line is how you watch it happen.


To stop, and to discard the data:

```bash
docker compose down -v
```

### Connecting to the database

| Setting | Value |
|---|---|
| Host | `localhost` — from another container it is `postgres` |
| Port | `5432` |
| Database | `jobtracker` |
| User | `jobtracker` |
| Password | `jobtracker` |

The same thing as a connection string, which is what the API is handed:

```
Host=localhost;Port=5432;Database=jobtracker;Username=jobtracker;Password=jobtracker
```

Or with no client at all:

```bash
docker compose exec postgres psql -U jobtracker -d jobtracker
```

The tables live in three schemas — `jobs`, `billing` and `hangfire` — so a
client that opens on `public` shows an empty database.

### Running the pieces separately

```bash
# Backend — needs PostgreSQL on 5432
dotnet run --project backend/src/Api/JobTracker.Api

# Frontend — no backend needed; uses the in-memory adapter
npm --prefix frontend run dev
```

The frontend serves the in-memory adapter unless `JOBTRACKER_API_URL` is set.
That is what lets the browser suite run with neither backend nor database.

---

## Verify it

Running the suites needs what `docker compose up` did not: the .NET 9 SDK and
Node 22. Once, after cloning:

```bash
npm --prefix frontend ci
npm --prefix frontend exec -- playwright install chromium
```

Then:

```bash
# Backend: 313 tests — domain, application, architecture, integration
dotnet test backend/JobTracker.sln

# Frontend: 251 tests, with an 80% coverage gate that fails the run
npm --prefix frontend run test:coverage

# Types. Separate on purpose: expect-type assertions fail at compile time,
# so a broken type test leaves Jest green
npm --prefix frontend run typecheck

# Browser: 21 tests. Seventeen against the in-memory adapter, and four against a
# second server whose API url points nowhere — the only faithful way to reach
# app/jobs/error.tsx, since the failure happens in the Server Component render
npm --prefix frontend run test:e2e

# The acceptance walkthrough against the running stack — all nine steps
docker compose up --build --wait
npm --prefix frontend run test:smoke
```

The integration suite starts its own PostgreSQL through Testcontainers, so it
needs Docker but no setup.


---

## What it does


1. Open the job list
2. Create a job through the form
3. It appears as **Scheduled**
4. **The assigned crew is notified** — asynchronous
5. Narrow the list by status and find it
6. Record that the crew started
7. Complete it with a signature and photos
8. It shows **Completed**
9. **An invoice exists and the customer was notified** — asynchronous


---

## Diagrams

Each one is a draw.io source with an exported SVG beside it, in
[`docs/diagrams/`](docs/diagrams).

| | Diagram | |
|---|---|---|
| 00 | System overview | [svg](docs/diagrams/00-system-overview.svg) · [drawio](docs/diagrams/00-system-overview.drawio) |
| 01 | Frontend layers | [svg](docs/diagrams/01-frontend-layers.svg) · [drawio](docs/diagrams/01-frontend-layers.drawio) |
| 02 | Backend modules | [svg](docs/diagrams/02-backend-modules.svg) · [drawio](docs/diagrams/02-backend-modules.drawio) |
| 03 | Async pipeline | [svg](docs/diagrams/03-async-pipeline.svg) · [drawio](docs/diagrams/03-async-pipeline.drawio) |
| 04 | Data architecture | [svg](docs/diagrams/04-data-architecture.svg) · [drawio](docs/diagrams/04-data-architecture.drawio) |
| 05 | Cross-cutting | [svg](docs/diagrams/05-cross-cutting.svg) · [drawio](docs/diagrams/05-cross-cutting.drawio) |


---

## Continuous integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every push to
`main` and on every pull request. Five jobs, and the two that need Docker are
separated from the three that do not, so a typo fails in a minute instead of
after the slow ones have finished.

| Job | What it runs |
|---|---|
| Backend | `dotnet build` in Release with warnings as errors, then the domain, application, Billing, presentation and architecture suites |
| Frontend | `npm run lint`, `npm run typecheck` and Jest behind an 80% coverage gate |
| Integration | The EF, tenancy, outbox and keyset suites against a real PostgreSQL that Testcontainers starts itself |
| End to end | Playwright against the in-memory adapter, with no backend and no database |
| Smoke | `docker compose up --build --wait`, then the nine-step acceptance walkthrough against the whole stack |

Type checking is its own step and never folded into the test run: `expect-type`
assertions fail at compile time, so a broken type test leaves Jest green. A
failing browser or smoke run uploads its Playwright report and the Compose logs
as artefacts.

Local development uses the same `docker-compose.yml` the smoke job does.

---

## Answers to the open questions

Six parts of the assessment ask for prose rather than code — 3.4 item 2, 4.1
item 5, 4.2, 4.3, 6.2 and 6.3. They are answered together, briefly, in
[`docs/answers.md`](docs/answers.md), along with the assumptions the frontend,
backend and database rest on.

Two of them have a longer treatment of their own:
[`docs/normalization.md`](docs/normalization.md) for 4.3, and
[`docs/design-principles.md`](docs/design-principles.md) for part 6.

---

