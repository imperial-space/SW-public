using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
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
                    profile_exams_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    preference_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_exams", x => x.profile_exams_id);
                });

            migrationBuilder.CreateTable(
                name: "profile_exam_data",
                columns: table => new
                {
                    profile_exam_data_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    preference_exams_id = table.Column<int>(type: "INTEGER", nullable: false),
                    prototype = table.Column<string>(type: "TEXT", nullable: false),
                    passed = table.Column<bool>(type: "INTEGER", nullable: false),
                    attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    last_attempt_time = table.Column<DateTime>(type: "TEXT", nullable: false)
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
