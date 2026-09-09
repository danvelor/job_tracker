using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobTracker.Modules.Jobs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialJobsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "jobs");

            migrationBuilder.CreateTable(
                name: "assignees",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignees", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    street = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    zip_code = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    signature_url = table.Column<string>(type: "text", nullable: true),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.id);
                    table.CheckConstraint("ck_jobs_cancelled_has_reason", "status <> 'Cancelled' OR cancellation_reason IS NOT NULL");
                    table.CheckConstraint("ck_jobs_completed_has_signature", "status <> 'Completed' OR signature_url IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_jobs_assignees_assignee_id",
                        column: x => x.assignee_id,
                        principalSchema: "jobs",
                        principalTable: "assignees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_jobs_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "jobs",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_photos",
                schema: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_photos_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "jobs",
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_photos_job_id",
                schema: "jobs",
                table: "job_photos",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_assignee_id",
                schema: "jobs",
                table: "jobs",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_customer_id",
                schema: "jobs",
                table: "jobs",
                column: "customer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_photos",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "assignees",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "jobs");
        }
    }
}
