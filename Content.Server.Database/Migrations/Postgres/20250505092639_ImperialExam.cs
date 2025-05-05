using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ImperialExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "profile_exams",
                columns: table => new
                {
                    profile_exams_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    preference_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_exams", x => x.profile_exams_id);
                });

            migrationBuilder.CreateTable(
                name: "profile_exam_data",
                columns: table => new
                {
                    profile_exam_data_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    preference_exams_id = table.Column<int>(type: "integer", nullable: false),
                    prototype = table.Column<string>(type: "text", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_exam_data", x => x.profile_exam_data_id);
                    table.ForeignKey(
                        name: "FK_profile_exam_data_profile_exams_preference_exams_id",
                        column: x => x.preference_exams_id,
                        principalTable: "profile_exams",
                        principalColumn: "profile_exams_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_profile_exam_data_preference_exams_id",
                table: "profile_exam_data",
                column: "preference_exams_id");

            migrationBuilder.CreateIndex(
                name: "IX_profile_exams_preference_id",
                table: "profile_exams",
                column: "preference_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_exam_data");

            migrationBuilder.DropTable(
                name: "profile_exams");
        }
    }
}
