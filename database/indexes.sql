CREATE INDEX ix_jobs_tenant_keyset
    ON jobs.jobs (organization_id,
                  coalesce(scheduled_date, '-infinity'::date) DESC,
                  id DESC);

CREATE INDEX ix_jobs_tenant_status_keyset
    ON jobs.jobs (organization_id, status,
                  coalesce(scheduled_date, '-infinity'::date) DESC,
                  id DESC);

CREATE INDEX ix_jobs_tenant_title_keyset
    ON jobs.jobs (organization_id, title, id);

CREATE INDEX ix_jobs_tenant_assignee
    ON jobs.jobs (organization_id, assignee_id);

CREATE INDEX ix_jobs_search
    ON jobs.jobs
    USING GIN (to_tsvector('english', title || ' ' || coalesce(description, '')));

CREATE INDEX ix_job_photos_job_id ON jobs.job_photos (job_id);
CREATE INDEX ix_jobs_assignee_id  ON jobs.jobs (assignee_id);
CREATE INDEX ix_jobs_customer_id  ON jobs.jobs (customer_id);

CREATE INDEX ix_assignees_tenant ON jobs.assignees (organization_id);
CREATE INDEX ix_customers_tenant ON jobs.customers (organization_id);
