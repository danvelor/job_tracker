using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobTracker.Modules.Jobs.Infrastructure.Migrations
{
    /// <summary>
    /// The parts of the schema EF's fluent API cannot express: an index over an
    /// expression, a GIN index, and a trigger. Written as raw SQL rather than
    /// worked around, because each one is doing something the alternatives
    /// cannot — see the comments inside.
    /// </summary>
    /// <inheritdoc />
    public partial class SearchIndexesAndTouchTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NFR-6 wants updated_at to be true, and a DEFAULT only fires on
            // INSERT. EF's SaveChanges is not the only writer this schema has
            // to survive — a migration, a support session in psql and a bulk
            // import all write rows — so the trigger is what keeps the column
            // honest rather than decorative.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION jobs.touch_updated_at() RETURNS trigger
                    LANGUAGE plpgsql AS $$
                BEGIN
                    NEW.updated_at := now();
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER tr_jobs_touch_updated_at
                    BEFORE UPDATE ON jobs.jobs
                    FOR EACH ROW EXECUTE FUNCTION jobs.touch_updated_at();
                """);

            // The default list order. Nothing filtered stands in front of the
            // sort keys, so this one index supplies the order for the
            // unfiltered list and for any multi-status filter.
            //
            // The key is the coalesce expression, not the column: over the bare
            // column the keyset comparison yields NULL for a dateless row and
            // WHERE drops it (architecture 6.4). The repository emits exactly
            // this expression — EF.Constant is what makes it a literal instead
            // of a bind parameter the planner cannot match.
            migrationBuilder.Sql(
                """
                CREATE INDEX ix_jobs_tenant_keyset
                    ON jobs.jobs (organization_id,
                                  coalesce(scheduled_date, '-infinity'::date) DESC,
                                  id DESC);
                """);

            // A single-status filter. status sits in front of the sort keys so
            // the equality is a scan boundary rather than a post-filter; behind
            // them it would buy nothing the index above does not already give.
            migrationBuilder.Sql(
                """
                CREATE INDEX ix_jobs_tenant_status_keyset
                    ON jobs.jobs (organization_id, status,
                                  coalesce(scheduled_date, '-infinity'::date) DESC,
                                  id DESC);
                """);

            // Sorting by title, the other JobSortField.
            migrationBuilder.Sql(
                "CREATE INDEX ix_jobs_tenant_title_keyset ON jobs.jobs (organization_id, title, id);");

            // FR-6's assignee filter. EF already created an index on
            // assignee_id alone for the foreign key; this one leads with the
            // tenant, which every query has and that one ignores.
            migrationBuilder.Sql(
                "CREATE INDEX ix_jobs_tenant_assignee ON jobs.jobs (organization_id, assignee_id);");

            // Full text over title and description together. GIN rather than
            // GiST because this table is read far more than written, and the
            // expression is the one the repository matches against — a
            // different concatenation or a different regconfig would leave the
            // index unused and nothing would say so.
            migrationBuilder.Sql(
                """
                CREATE INDEX ix_jobs_search ON jobs.jobs
                    USING GIN (to_tsvector('english', title || ' ' || coalesce(description, '')));
                """);

            // Every roster read filters by organization and by nothing else,
            // and the pickers issue one on every page load.
            migrationBuilder.Sql(
                "CREATE INDEX ix_assignees_tenant ON jobs.assignees (organization_id);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_customers_tenant ON jobs.customers (organization_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS jobs.ix_customers_tenant;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS jobs.ix_assignees_tenant;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS jobs.ix_jobs_search;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS jobs.ix_jobs_tenant_assignee;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS jobs.ix_jobs_tenant_title_keyset;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS jobs.ix_jobs_tenant_status_keyset;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS jobs.ix_jobs_tenant_keyset;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_jobs_touch_updated_at ON jobs.jobs;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS jobs.touch_updated_at();");
        }
    }
}
