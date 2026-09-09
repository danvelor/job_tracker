-- JobTracker — the search query, and its measured plans
--
-- Deliverable for assessment part 4.2. Every plan below is real output captured
-- from PostgreSQL 17.11, not a prediction: 50,012 jobs (45,024 in the tenant
-- being queried), 20,000 photos, ANALYZE run, schema.sql and indexes.sql
-- applied by the EF migrations. Reproduced with:
--
--   docker run -d --name jt-plans -e POSTGRES_PASSWORD=p -e POSTGRES_USER=jt \
--     -e POSTGRES_DB=jobtracker -p 55433:5432 postgres:17-alpine
--   dotnet ef database update --connection "Host=localhost;Port=55433;..."
--   -- then the generate_series load documented at the foot of this file
--
-- Two of these plans do not use the index the query was written for. They are
-- kept as they came out. Which index the planner chooses is a question for
-- EXPLAIN and not for assertion (architecture 6.3), and a captured plan that
-- contradicts the design is worth more than a claim that matches it.
--
-- The repository issues EF's translation of this query rather than this text.
-- The one difference is the photo count: EF emits a correlated subquery where
-- this writes a LATERAL. PostgreSQL plans both the same way — the plan for Q1
-- below shows the aggregate running once per returned row either way.

-- ===========================================================================
-- The query
-- ===========================================================================

SELECT  j.id, j.title, j.status, j.scheduled_date,
        j.assignee_id, a.name AS assignee_name,
        j.street, j.city, j.state,
        coalesce(p.photo_count, 0) AS photo_count
FROM    jobs.jobs j
LEFT JOIN jobs.assignees a ON a.id = j.assignee_id
LEFT JOIN LATERAL (
        SELECT count(*) AS photo_count
        FROM   jobs.job_photos ph
        WHERE  ph.job_id = j.id
) p ON true
WHERE   j.organization_id = $1
  AND   ($2::text[] IS NULL OR j.status = ANY ($2))
  AND   ($3::date   IS NULL OR j.scheduled_date >= $3)
  AND   ($4::date   IS NULL OR j.scheduled_date <= $4)
  AND   ($5::uuid   IS NULL OR j.assignee_id = $5)
  AND   ($6::text   IS NULL OR
         to_tsvector('english', j.title || ' ' || coalesce(j.description, ''))
         @@ websearch_to_tsquery('english', $6))
  AND   ($7::date IS NULL OR
         (coalesce(j.scheduled_date, '-infinity'::date), j.id) < ($7, $8))
ORDER BY coalesce(j.scheduled_date, '-infinity'::date) DESC, j.id DESC
LIMIT   $9;

-- The LATERAL count avoids grouping the whole result set: it runs once per
-- returned row against ix_job_photos_job_id, so its cost is bounded by the page
-- size rather than by the number of matches.
--
-- The keyset predicate compares the ordered pair, which is what stops a row
-- being skipped or served twice when data changes between pages.
--
-- The ordering key is coalesce(scheduled_date, '-infinity'), not the column.
-- Over the bare column the pair comparison yields NULL rather than true for a
-- dateless row, WHERE discards it, and dateless jobs vanish after page one
-- (architecture 6.4). Two integration tests guard it.

-- ===========================================================================
-- Q1 — first page, no filters
-- ===========================================================================
--
-- The plan to read first. No Sort node: the index supplies the order, so the
-- LIMIT stops after 20 rows instead of after 45,024. The photo count is an
-- Index Only Scan run 20 times — loops=20 — which is the LATERAL earning its
-- place.
--
--  Limit (actual time=0.138..0.690 rows=20 loops=1)
--    Buffers: shared hit=64
--    ->  Nested Loop Left Join (actual time=0.137..0.688 rows=20 loops=1)
--          Buffers: shared hit=64
--          ->  Nested Loop Left Join (actual time=0.100..0.417 rows=20 loops=1)
--                Join Filter: (a.id = j.assignee_id)
--                Buffers: shared hit=24
--                ->  Index Scan using ix_jobs_tenant_keyset on jobs j (actual time=0.072..0.377 rows=20 loops=1)
--                      Index Cond: (organization_id = '1111...'::uuid)
--                      Buffers: shared hit=23
--                ->  Materialize (actual time=0.001..0.001 rows=2 loops=20)
--                      ->  Seq Scan on assignees a (actual time=0.009..0.010 rows=2 loops=1)
--          ->  Aggregate (actual time=0.013..0.013 rows=1 loops=20)
--                Buffers: shared hit=40
--                ->  Index Only Scan using ix_job_photos_job_id on job_photos ph (actual time=0.013..0.013 rows=0 loops=20)
--                      Index Cond: (job_id = j.id)
--                      Heap Fetches: 0
--  Planning Time: 2.009 ms
--  Execution Time: 0.833 ms
--
-- The Seq Scan on assignees is correct: the roster has two rows and lives in
-- one page, so an index lookup would cost more than reading it whole.

-- ===========================================================================
-- Q2 vs Q3 — why keyset rather than OFFSET (D-12)
-- ===========================================================================
--
-- The same page, 30,000 rows deep, fetched two ways.
--
-- Q2, keyset:
--   WHERE (coalesce(j.scheduled_date,'-infinity'), j.id) < ('2026-10-20', '2f2147a5-…')
--   ORDER BY coalesce(j.scheduled_date,'-infinity') DESC, j.id DESC LIMIT 20
--
--  Limit (actual time=0.038..0.118 rows=20 loops=1)
--    Buffers: shared hit=23
--    ->  Index Scan using ix_jobs_tenant_keyset on jobs j (actual time=0.037..0.116 rows=20 loops=1)
--  Execution Time: 0.150 ms
--
-- Q3, the same page by OFFSET 30000:
--
--  Limit (actual time=6.960..6.964 rows=20 loops=1)
--    Buffers: shared hit=30243
--    ->  Index Scan using ix_jobs_tenant_keyset on jobs j (actual time=0.018..6.370 rows=30020 loops=1)
--  Execution Time: 6.982 ms
--
--        buffers    rows walked    time
--   Q2        23             20    0.150 ms
--   Q3    30,243         30,020    6.982 ms
--
-- 1,315 times the buffers for the same twenty rows, and the ratio grows with
-- depth: OFFSET is linear in how far into the list the reader has scrolled,
-- keyset is flat. This is the measurement behind NFR-5, and it is also why the
-- read model carries no total count — a count pays the same linear cost the
-- pagination was designed to avoid.

-- ===========================================================================
-- Q4 — a single-status filter
-- ===========================================================================
--
-- Uses the composite index, and status being in front of the sort keys is what
-- makes it a scan boundary rather than a post-filter: 23 buffers, no Sort.
--
--  Limit (actual time=0.030..0.048 rows=20 loops=1)
--    Buffers: shared hit=23
--    ->  Index Scan using ix_jobs_tenant_status_keyset on jobs j (actual time=0.029..0.047 rows=20 loops=1)
--  Execution Time: 0.058 ms

-- ===========================================================================
-- Q5 — full text, and the two plans it has
-- ===========================================================================
--
-- This is the pair that does not match the design's expectation, and the
-- planner is right both times.
--
-- 'skylight' matches 1 job in 5. The planner walks the ordered index and
-- applies the tsvector as a filter, because with LIMIT 20 it expects to find
-- twenty matches almost immediately — and it does, in 27 buffers:
--
--  Limit (actual time=0.037..0.072 rows=20 loops=1)
--    Buffers: shared hit=27
--    ->  Index Scan using ix_jobs_tenant_keyset on jobs j (actual time=0.037..0.071 rows=20 loops=1)
--          Index Cond: (organization_id = '1111…'::uuid)
--          Filter: (to_tsvector('english'::regconfig, (((title)::text || ' '::text) || COALESCE(description, ''::text))) @@ '''skylight'''::tsquery)
--  Execution Time: 0.080 ms
--
-- 'cupola' matches 12 of 50,012. Now the GIN index wins, and the planner takes
-- it — accepting a Sort, because sorting twelve rows is nothing:
--
--  Limit (actual time=0.069..0.070 rows=12 loops=1)
--    Buffers: shared hit=11
--    ->  Sort (actual time=0.069..0.069 rows=12 loops=1)
--          Sort Key: (COALESCE(scheduled_date, '-infinity'::date)) DESC, id DESC
--          Sort Method: quicksort  Memory: 25kB
--          ->  Bitmap Heap Scan on jobs j (actual time=0.037..0.039 rows=12 loops=1)
--                Recheck Cond: (to_tsvector(…) @@ '''cupola'''::tsquery)
--                Heap Blocks: exact=1
--                Buffers: shared hit=5
--                ->  Bitmap Index Scan on ix_jobs_search (actual time=0.029..0.030 rows=12 loops=1)
--                      Buffers: shared hit=4
--  Execution Time: 0.105 ms
--
-- So ix_jobs_search is not dead weight; it is the plan for the searches people
-- actually type, which are specific. A common word is served better by the
-- ordering index, and the planner switches on its own. Note also what the
-- Recheck Cond confirms: the index expression and the query expression are
-- character-for-character the same, which is what EF.Constant bought — see
-- indexes.sql.

-- ===========================================================================
-- Q6 — filtering by assignee
-- ===========================================================================
--
-- ix_jobs_tenant_assignee is not used either, and for the same reason: this
-- assignee holds roughly 45% of the tenant's jobs, so the ordered index reaches
-- twenty of them after discarding 54.
--
--  Limit (actual time=0.046..0.051 rows=20 loops=1)
--    Buffers: shared hit=77
--    ->  Index Scan using ix_jobs_tenant_keyset on jobs j (actual time=0.046..0.050 rows=20 loops=1)
--          Filter: (assignee_id = 'aaaa…0001'::uuid)
--          Rows Removed by Filter: 54
--  Execution Time: 0.056 ms
--
-- On this fixture the index earns nothing. It stays because the fixture is not
-- the shape of the requirement: a real contractor has dozens of crews, not two,
-- and at thirty crews the same filter discards roughly 600 rows per page
-- instead of 54. Dropping an index because a synthetic distribution made it
-- redundant would be tuning for the fixture.

-- ===========================================================================
-- The fixture
-- ===========================================================================
--
-- INSERT INTO jobs.jobs (…)
-- SELECT gen_random_uuid(),
--        CASE WHEN g % 10 = 0 THEN '2222…'::uuid ELSE '1111…'::uuid END,
--        (ARRAY['Ridge tile replacement','Gutter reline','Flashing repair',
--               'Full reroof','Skylight seal'])[1 + g % 5] || ' #' || g,
--        (ARRAY['north slope','rear elevation','valley detail',
--               'storm damage',NULL])[1 + g % 5],
--        (ARRAY['Scheduled','InProgress','Completed','Cancelled','Scheduled'])[1 + g % 5],
--        …,
--        CASE WHEN g % 97 = 0 THEN NULL ELSE DATE '2026-01-01' + (g % 900) END,
--        …
-- FROM generate_series(1, 50000) g;
--
-- One job in ten belongs to the second organization, so every plan above is
-- also a demonstration that the tenant predicate is doing work. One in 97 has
-- no scheduled_date, which is what makes the coalesce in the ordering key more
-- than theoretical.
