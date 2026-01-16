using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimizedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_RowVersion",
                table: "Users",
                column: "RowVersion");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Processing",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtUtc", "CreatedAtUtc" },
                filter: "[ProcessedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotentRequests_CreatedAt",
                table: "IdempotentRequests",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_RowVersion",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Processing",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_IdempotentRequests_CreatedAt",
                table: "IdempotentRequests");
        }
    }
}
