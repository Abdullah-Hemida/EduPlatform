using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class modifyCategoryandDeleteLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingModerationLogs");

            migrationBuilder.DropTable(
                name: "CourseModerationLogs");

            migrationBuilder.DropTable(
                name: "ReactiveCourseModerationLogs");

            migrationBuilder.DropTable(
                name: "ReactiveEnrollmentLogs");

            // Add new columns with default empty string so existing rows are valid
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Categories",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameIt",
                table: "Categories",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "Categories",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            // Copy existing Name values into NameAr (so your Arabic names are preserved)
            // Adjust table name/schema if different
            migrationBuilder.Sql("UPDATE Categories SET NameAr = Name WHERE NameAr = '' OR NameAr IS NULL");

            // Optionally: keep the old Name column for backward compatibility.
            // If you want to drop it after you update code, use:
            migrationBuilder.DropColumn(name: "Name", table: "Categories");      

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "NameIt",
                table: "Categories",
                newName: "Name");

            migrationBuilder.CreateTable(
                name: "BookingModerationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReactiveEnrollmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingModerationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingModerationLogs_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CourseModerationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrivateCourseId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AdminId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseModerationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseModerationLogs_PrivateCourses_PrivateCourseId",
                        column: x => x.PrivateCourseId,
                        principalTable: "PrivateCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReactiveCourseModerationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReactiveCourseId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactiveCourseModerationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReactiveCourseModerationLogs_ReactiveCourses_ReactiveCourseId",
                        column: x => x.ReactiveCourseId,
                        principalTable: "ReactiveCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReactiveEnrollmentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReactiveCourseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactiveEnrollmentLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingModerationLogs_BookingId",
                table: "BookingModerationLogs",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingModerationLogs_ReactiveEnrollmentId",
                table: "BookingModerationLogs",
                column: "ReactiveEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseModerationLogs_AdminId",
                table: "CourseModerationLogs",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseModerationLogs_CreatedAtUtc",
                table: "CourseModerationLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CourseModerationLogs_PrivateCourseId",
                table: "CourseModerationLogs",
                column: "PrivateCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReactiveCourseModerationLogs_ReactiveCourseId",
                table: "ReactiveCourseModerationLogs",
                column: "ReactiveCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReactiveEnrollmentLogs_ReactiveCourseId",
                table: "ReactiveEnrollmentLogs",
                column: "ReactiveCourseId");
        }
    }
}
