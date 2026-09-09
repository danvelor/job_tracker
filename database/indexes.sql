-- JobTracker — indexes
--
-- Deliverable for assessment part 4.1. Created by the migration
-- SearchIndexesAndTouchTrigger, which writes them as raw SQL: an index over an
-- expression and a GIN index are not expressible in EF's fluent API.
--
-- Captured query plans for each of these live in queries.sql. Which index the
-- planner actually chooses is a question for EXPLAIN and not for assertion,
-- so the plans there are real output, not predictions.

-- ---------------------------------------------------------------------------
-- The default list, and why the key is an expression
-- ---------------------------------------------------------------------------
-- Nothing filtered stands in front of the sort keys, so this one index supplies
-- the order for the unfiltered list and for any multi-status filter — a
-- multi-status IN cannot be a scan boundary, so the planner is better served by
-- an index that gives it the order directly.
--
-- The key is coalesce(scheduled_date, '-infinity'), not the column. A Draft job
-- has no date, and over the bare column the keyset comparison
--
--     (scheduled_date, id) < (cursor_date, cursor_id)
--
-- yields NULL rather than true for a dateless row, so WHERE discards it and the
-- job vanishes after page one (architecture 6.4). '-infinity' is a real date:
-- the expression is total, the comparison always defined, and an undated job
-- sorts last under DESC, which is where it belongs in a schedule.
--
-- The repository emits this expression verbatim. Getting there took EF.Constant:
-- without it EF emitted coalesce(scheduled_date, $1), and the planner cannot
-- match a bind parameter against an indexed expression — the index would have
-- existed and never been used, with nothing to say so. A test now compares the
-- stored index definition against the expression the repository generates.
CREATE INDEX ix_jobs_tenant_keyset
    ON jobs.jobs (organization_id,
                  coalesce(scheduled_date, '-infinity'::date) DESC,
                  id DESC);

-- ---------------------------------------------------------------------------
-- A single-status filter
-- ---------------------------------------------------------------------------
-- status sits in front of the sort keys, which is the whole value of this
-- index: the equality becomes a scan boundary rather than a post-filter. Behind
-- them it would buy nothing that ix_jobs_tenant_keyset does not already give.
CREATE INDEX ix_jobs_tenant_status_keyset
    ON jobs.jobs (organization_id, status,
                  coalesce(scheduled_date, '-infinity'::date) DESC,
                  id DESC);

-- The other JobSortField. No coalesce: title is NOT NULL, so the column is
-- already a total ordering key.
CREATE INDEX ix_jobs_tenant_title_keyset
    ON jobs.jobs (organization_id, title, id);

-- FR-6's assignee filter. EF creates an index on assignee_id alone for the
-- foreign key; this one leads with the tenant, which every query has and that
-- one ignores.
CREATE INDEX ix_jobs_tenant_assignee
    ON jobs.jobs (organization_id, assignee_id);

-- ---------------------------------------------------------------------------
-- Full text
-- ---------------------------------------------------------------------------
-- Over title and description together, because a search that misses the
-- description misses most of what a crew actually wrote down.
--
-- GIN rather than GiST: this table is read far more often than written, which
-- is the trade GIN makes. The indexed expression must be character-for-
-- character the one the repository matches against — a different concatenation
-- or a different regconfig leaves the index unused and silent — so a test
-- compares the two.
--
-- The search is a filter and never a sort. Ordering by ts_rank would make
-- PostgreSQL compute and sort the whole matched set for every page, which is
-- precisely the cost profile keyset pagination exists to remove (D-12).
CREATE INDEX ix_jobs_search
    ON jobs.jobs
    USING GIN (to_tsvector('english', title || ' ' || coalesce(description, '')));

-- ---------------------------------------------------------------------------
-- Photos and rosters
-- ---------------------------------------------------------------------------
-- Created by EF for the foreign key. It is what makes the per-row photo count
-- in queries.sql cost page-size lookups rather than a scan of every photo.
CREATE INDEX ix_job_photos_job_id ON jobs.job_photos (job_id);

-- EF creates these two for the foreign keys, and they are listed so this file
-- matches the database exactly. ix_jobs_tenant_assignee above serves the
-- filter; these serve the constraint check on the roster side, which is the
-- other direction.
CREATE INDEX ix_jobs_assignee_id ON jobs.jobs (assignee_id);
CREATE INDEX ix_jobs_customer_id ON jobs.jobs (customer_id);

-- Every roster read filters by organization and by nothing else, and the
-- pickers issue one on every page load.
CREATE INDEX ix_assignees_tenant ON jobs.assignees (organization_id);
CREATE INDEX ix_customers_tenant ON jobs.customers (organization_id);

-- ---------------------------------------------------------------------------
-- What is deliberately not indexed
-- ---------------------------------------------------------------------------
-- created_at and updated_at. Nothing queries by either; they are an audit
-- record (NFR-6), and an index nobody reads still costs every write.
--
-- organization_id on its own. Every query that filters by tenant also orders or
-- filters by something else, and each composite index above already leads with
-- it — a standalone index would be a prefix of four others.
--
-- status on its own. Selectivity is poor at five values, and the composite
-- above covers the case that matters.
