using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JobTracker.Modules.Jobs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedRosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "jobs",
                table: "assignees",
                columns: new[] { "id", "name", "organization_id" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-0000-4000-8000-000000000001"), "J. Ortiz", new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("aaaaaaaa-0000-4000-8000-000000000002"), "M. Ruiz", new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("aaaaaaaa-0000-4000-8000-000000000003"), "K. Lawson", new Guid("22222222-2222-2222-2222-222222222222") }
                });

            migrationBuilder.InsertData(
                schema: "jobs",
                table: "customers",
                columns: new[] { "id", "email", "name", "organization_id" },
                values: new object[,]
                {
                    { new Guid("cccccccc-0000-4000-8000-000000000001"), "ops@acme.test", "Acme Holdings", new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("cccccccc-0000-4000-8000-000000000002"), "facilities@birch.test", "Birch Property", new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("cccccccc-0000-4000-8000-000000000003"), "admin@cedar.test", "Cedar Estates", new Guid("22222222-2222-2222-2222-222222222222") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "jobs",
                table: "assignees",
                keyColumn: "id",
                keyValue: new Guid("aaaaaaaa-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "jobs",
                table: "assignees",
                keyColumn: "id",
                keyValue: new Guid("aaaaaaaa-0000-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "jobs",
                table: "assignees",
                keyColumn: "id",
                keyValue: new Guid("aaaaaaaa-0000-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "jobs",
                table: "customers",
                keyColumn: "id",
                keyValue: new Guid("cccccccc-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "jobs",
                table: "customers",
                keyColumn: "id",
                keyValue: new Guid("cccccccc-0000-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "jobs",
                table: "customers",
                keyColumn: "id",
                keyValue: new Guid("cccccccc-0000-4000-8000-000000000003"));
        }
    }
}
