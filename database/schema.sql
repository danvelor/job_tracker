CREATE SCHEMA IF NOT EXISTS jobs;

CREATE TABLE jobs.assignees (
    id              uuid CONSTRAINT pk_assignees PRIMARY KEY,
    organization_id uuid NOT NULL,
    name            character varying(200) NOT NULL
);

CREATE TABLE jobs.customers (
    id              uuid CONSTRAINT pk_customers PRIMARY KEY,
    organization_id uuid NOT NULL,
    name            character varying(200) NOT NULL,
    email           character varying(320) NOT NULL
);

CREATE TABLE jobs.jobs (
    id                  uuid         CONSTRAINT pk_jobs PRIMARY KEY,
    organization_id     uuid         NOT NULL,
    title               character varying(200) NOT NULL,
    description         text         NULL,
    status              character varying(20) NOT NULL,

    street              text         NOT NULL,
    city                text         NOT NULL,
    state               text         NOT NULL,
    zip_code            text         NOT NULL,
    latitude            numeric(9,6) NOT NULL,
    longitude           numeric(9,6) NOT NULL,

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

    CONSTRAINT ck_jobs_completed_has_signature
        CHECK (status <> 'Completed' OR signature_url IS NOT NULL),
    CONSTRAINT ck_jobs_cancelled_has_reason
        CHECK (status <> 'Cancelled' OR cancellation_reason IS NOT NULL),
    CONSTRAINT ck_jobs_status
        CHECK (status IN ('Draft', 'Scheduled', 'InProgress', 'Completed', 'Cancelled'))
);

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

CREATE TABLE jobs.job_photos (
    id          uuid        CONSTRAINT pk_job_photos PRIMARY KEY,
    job_id      uuid        NOT NULL
        CONSTRAINT fk_job_photos_jobs_job_id REFERENCES jobs.jobs(id) ON DELETE CASCADE,
    url         character varying(2048) NOT NULL,
    captured_at timestamptz NOT NULL,
    caption     character varying(500)  NULL
);
