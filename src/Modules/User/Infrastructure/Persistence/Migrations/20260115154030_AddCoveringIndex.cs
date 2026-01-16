using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoveringIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsActive_CreatedAt",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive_CreatedAt",
                table: "Users",
                columns: new[] { "IsActive", "CreatedAt" },
                descending: new[] { false, true },
                filter: "[DeletedAt] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Email", "FirstName", "LastName", "Roles", "LastLoginAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsActive_CreatedAt",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive_CreatedAt",
                table: "Users",
                columns: new[] { "IsActive", "CreatedAt" },
                descending: new[] { false, true },
                filter: "[DeletedAt] IS NULL");
        }
    }
}
