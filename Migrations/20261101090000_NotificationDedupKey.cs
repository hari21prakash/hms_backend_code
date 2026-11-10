using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class NotificationDedupKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Purely additive: a nullable column and a partial unique index. Existing notification rows are untouched
            // (their DedupKey stays NULL and NULLs are excluded from the index).
            migrationBuilder.AddColumn<string>(
                name: "DedupKey",
                table: "Notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_DedupKey",
                table: "Notifications",
                columns: new[] { "UserId", "DedupKey" },
                unique: true,
                filter: "\"DedupKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_DedupKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DedupKey",
                table: "Notifications");
        }
    }
}
