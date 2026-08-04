using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wasta.SupportChat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentIdToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "ChatMessages",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_StudentId_CreatedAt",
                table: "ChatMessages",
                columns: new[] { "StudentId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_StudentId_CreatedAt",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "ChatMessages");
        }
    }
}
