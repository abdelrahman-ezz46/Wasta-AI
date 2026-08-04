using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wasta.CareerCoach.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentCoachPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    AttemptId = table.Column<int>(type: "integer", nullable: false),
                    ScoreId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: true),
                    Assessment = table.Column<string>(type: "text", nullable: true),
                    WeeklyPlan = table.Column<string>(type: "jsonb", nullable: true),
                    ProjectTitle = table.Column<string>(type: "text", nullable: true),
                    ProjectDesc = table.Column<string>(type: "text", nullable: true),
                    ProjectSkills = table.Column<string>(type: "jsonb", nullable: true),
                    InterviewLine = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProviderUsed = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCoachPlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentCoachPlans_AttemptId",
                table: "StudentCoachPlans",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentCoachPlans_StudentId_Status",
                table: "StudentCoachPlans",
                columns: new[] { "StudentId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentCoachPlans");
        }
    }
}
