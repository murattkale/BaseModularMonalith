using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace User.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateKeysetIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsActive_CreatedAt",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive_CreatedAt_Id",
                table: "Users",
                columns: new[] { "IsActive", "CreatedAt", "Id" },
                descending: new[] { false, true, true },
                filter: "[DeletedAt] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Email", "FirstName", "LastName", "Roles", "LastLoginAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsActive_CreatedAt_Id",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive_CreatedAt",
                table: "Users",
                columns: new[] { "IsActive", "CreatedAt" },
                descending: new[] { false, true },
                filter: "[DeletedAt] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Email", "FirstName", "LastName", "Roles", "LastLoginAt" });
        }
    }
}
