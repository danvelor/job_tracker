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
