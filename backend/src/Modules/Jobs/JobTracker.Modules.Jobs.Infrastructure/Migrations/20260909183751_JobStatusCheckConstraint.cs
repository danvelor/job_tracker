using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobTracker.Modules.Jobs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class JobStatusCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_jobs_status",
                schema: "jobs",
                table: "jobs",
                sql: "status in ('Draft', 'Scheduled', 'InProgress', 'Completed', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_jobs_status",
                schema: "jobs",
                table: "jobs");
        }
    }
}
