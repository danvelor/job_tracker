-- JobTracker — schema
--
-- Deliverable for assessment part 4.1. EF Core migrations under
-- backend/src/Modules/Jobs/JobTracker.Modules.Jobs.Infrastructure/Migrations
-- produce this same shape and are what actually runs; this file is the
-- annotated form, written to be read rather than executed by the application.
-- It is not prose: both this file and indexes.sql were executed against a
-- clean PostgreSQL 17.11, and the resulting catalog was diffed column by
-- column, constraint by constraint and index by index against a database built
-- by `dotnet ef database update`. The two are identical. Constraint names below
-- are EF's, for that reason.
--
-- Tables belonging to plan 4 — jobs.outbox_messages, jobs.notifications and
-- billing.invoices — are documented in context/design.md B7 and are not
-- repeated here, because a schema file that describes tables the migrations do
-- not yet create is a schema file that lies.

-- One schema per module (architecture 6.1). A module's DbContext maps only its
-- own schema, so a query that reaches across a module boundary has no DbSet to
-- reach through — the boundary is enforced by the compiler, and the schema
-- makes it visible in the database too.
CREATE SCHEMA IF NOT EXISTS jobs;

-- ---------------------------------------------------------------------------
-- Rosters
-- ---------------------------------------------------------------------------
-- Read-only (D-26). Seeded by migration; no command writes them and no
-- endpoint mutates them. They exist so the assignee and customer pickers have
-- something to offer, and so a job row can show a name instead of a UUID.
--
-- When a Contacts module exists these rows are maintained by an integration
-- event instead of a seed. The reason they live in this schema today, denormal-
-- ised out of a module that does not exist, is argued in docs/normalization.md.

CREATE TABLE jobs.assignees (
    id              uuid CONSTRAINT pk_assignees PRIMARY KEY,
    organization_id uuid NOT NULL,
    name            character varying(200) NOT NULL
);

CREATE TABLE jobs.customers (
    id              uuid CONSTRAINT pk_customers PRIMARY KEY,
    organization_id uuid NOT NULL,
    name            character varying(200) NOT NULL,
    -- FR-10 needs somewhere to send the completion notice, so a customer
    -- without one is a customer the notification module cannot serve.
    email           character varying(320) NOT NULL
);

-- ---------------------------------------------------------------------------
-- Jobs
-- ---------------------------------------------------------------------------

CREATE TABLE jobs.jobs (
    id                  uuid         CONSTRAINT pk_jobs PRIMARY KEY,
    organization_id     uuid         NOT NULL,
    title               character varying(200) NOT NULL,
    description         text         NULL,

    -- Text, not an ordinal. An integer-backed enum reorders the moment someone
    -- inserts a member and every stored row then means something else; text
    -- also reads correctly in a psql session and diffs legibly in a migration.
    -- The CHECK is what stops text from also meaning "any string at all".
    status              character varying(20) NOT NULL,

    -- Address is a value object with no identity, so it is flattened onto this
    -- row rather than given a table of its own (architecture 6.2). A table
    -- would add a join to every read to model something that cannot exist
    -- apart from the job it belongs to.
    street              text         NOT NULL,
    city                text         NOT NULL,
    state               text         NOT NULL,
    zip_code            text         NOT NULL,
    latitude            numeric(9,6) NOT NULL,
    longitude           numeric(9,6) NOT NULL,

    -- Nullable because a Draft job has no date yet. That single NULL is the
    -- reason the ordering key throughout this schema is an expression rather
    -- than the column — see indexes.sql.
    scheduled_date      date         NULL,
    started_at          timestamptz  NULL,
    completed_at        timestamptz  NULL,
    cancelled_at        timestamptz  NULL,
    cancellation_reason text         NULL,
    signature_url       text         NULL,

    assignee_id         uuid         NULL
        CONSTRAINT fk_jobs_assignees_assignee_id REFERENCES jobs.assignees(id) ON DELETE RESTRICT,
    customer_id         uuid         NOT NULL
        CONSTRAINT fk_jobs_customers_customer_id REFERENCES jobs.customers(id) ON DELETE RESTRICT,

    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),

    -- BR-4 and BR-5 at the level of the data. The aggregate enforces both, and
    -- that is not enough: a row written by a migration, a support session in
    -- psql or a bulk import must not be able to contradict a rule the rest of
    -- the system relies on.
    CONSTRAINT ck_jobs_completed_has_signature
        CHECK (status <> 'Completed' OR signature_url IS NOT NULL),
    CONSTRAINT ck_jobs_cancelled_has_reason
        CHECK (status <> 'Cancelled' OR cancellation_reason IS NOT NULL),

    -- Generated from the JobStatus enum in the EF configuration, so there is
    -- one source of truth. Adding a status without generating a migration
    -- fails a test: the database carries the constraint the migration wrote,
    -- not the one the enum now describes.
    CONSTRAINT ck_jobs_status
        CHECK (status IN ('Draft', 'Scheduled', 'InProgress', 'Completed', 'Cancelled'))
);

-- NFR-6 wants updated_at to be true, and a DEFAULT only fires on INSERT. EF's
-- SaveChanges is not the only writer this schema has to survive, so the column
-- is kept honest by a trigger rather than by the application remembering.
CREATE FUNCTION jobs.touch_updated_at() RETURNS trigger
    LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_jobs_touch_updated_at
    BEFORE UPDATE ON jobs.jobs
    FOR EACH ROW EXECUTE FUNCTION jobs.touch_updated_at();

-- ---------------------------------------------------------------------------
-- Photos
-- ---------------------------------------------------------------------------
-- A separate table because a job has many, and reachable only through the
-- aggregate root: there is no AddPhoto on Job, and photos arrive through
-- Complete, which is the one moment the business produces them.
--
-- BR-2 means the application never deletes a job, so the cascade is for the day
-- a database is trimmed by hand — a photo whose job is gone is a row nothing
-- can ever reach.

CREATE TABLE jobs.job_photos (
    id          uuid        CONSTRAINT pk_job_photos PRIMARY KEY,
    job_id      uuid        NOT NULL
        CONSTRAINT fk_job_photos_jobs_job_id REFERENCES jobs.jobs(id) ON DELETE CASCADE,
    url         character varying(2048) NOT NULL,
    captured_at timestamptz NOT NULL,
    caption     character varying(500)  NULL
);

-- ---------------------------------------------------------------------------
-- On foreign keys, and where they stop
-- ---------------------------------------------------------------------------
-- assignee_id and customer_id do carry foreign keys, because the rosters they
-- point at live in this schema (D-26, reversing D-21). A job assigned to
-- nobody real is a job nobody does.
--
-- billing.invoices.job_id, when plan 4 creates it, will carry no foreign key
-- into jobs.jobs — and that absence is the interesting one. It is the module
-- boundary itself. A constraint there would let the database enforce a
-- relationship the two modules deliberately express through a contract of
-- primitives, and would turn extracting Billing into its own database from a
-- migration into a redesign.
--
-- The rule is not "no foreign keys". It is: a foreign key stays inside the
-- schema that owns both ends.
