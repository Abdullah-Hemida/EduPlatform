using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OnlineSchoolTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OnlineCourseLessonId",
                table: "FileResources",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OnlineCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverImageKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IntroductionVideoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PricePerMonth = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DurationMonths = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    LevelId = table.Column<int>(type: "int", nullable: false),
                    TeacherName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineCourses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnlineCourseMonths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OnlineCourseId = table.Column<int>(type: "int", nullable: false),
                    MonthIndex = table.Column<int>(type: "int", nullable: false),
                    MonthStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MonthEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsReadyForPayment = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineCourseMonths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnlineCourseMonths_OnlineCourses_OnlineCourseId",
                        column: x => x.OnlineCourseId,
                        principalTable: "OnlineCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnlineEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OnlineCourseId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnlineEnrollments_OnlineCourses_OnlineCourseId",
                        column: x => x.OnlineCourseId,
                        principalTable: "OnlineCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnlineEnrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OnlineCourseLessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OnlineCourseId = table.Column<int>(type: "int", nullable: false),
                    OnlineCourseMonthId = table.Column<int>(type: "int", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MeetUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecordedVideoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduledUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineCourseLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnlineCourseLessons_OnlineCourseMonths_OnlineCourseMonthId",
                        column: x => x.OnlineCourseMonthId,
                        principalTable: "OnlineCourseMonths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OnlineCourseLessons_OnlineCourses_OnlineCourseId",
                        column: x => x.OnlineCourseId,
                        principalTable: "OnlineCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnlineEnrollmentMonthPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OnlineEnrollmentId = table.Column<int>(type: "int", nullable: false),
                    OnlineCourseMonthId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineEnrollmentMonthPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnlineEnrollmentMonthPayments_OnlineCourseMonths_OnlineCourseMonthId",
                        column: x => x.OnlineCourseMonthId,
                        principalTable: "OnlineCourseMonths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OnlineEnrollmentMonthPayments_OnlineEnrollments_OnlineEnrollmentId",
                        column: x => x.OnlineEnrollmentId,
                        principalTable: "OnlineEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_OnlineCourseLessonId",
                table: "FileResources",
                column: "OnlineCourseLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineCourseLessons_OnlineCourseId",
                table: "OnlineCourseLessons",
                column: "OnlineCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineCourseLessons_OnlineCourseMonthId",
                table: "OnlineCourseLessons",
                column: "OnlineCourseMonthId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineCourseMonths_OnlineCourseId_MonthIndex",
                table: "OnlineCourseMonths",
                columns: new[] { "OnlineCourseId", "MonthIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineCourses_LevelId",
                table: "OnlineCourses",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineEnrollmentMonthPayments_OnlineCourseMonthId",
                table: "OnlineEnrollmentMonthPayments",
                column: "OnlineCourseMonthId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineEnrollmentMonthPayments_OnlineEnrollmentId_OnlineCourseMonthId",
                table: "OnlineEnrollmentMonthPayments",
                columns: new[] { "OnlineEnrollmentId", "OnlineCourseMonthId" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineEnrollments_OnlineCourseId_StudentId",
                table: "OnlineEnrollments",
                columns: new[] { "OnlineCourseId", "StudentId" },
                unique: true,
                filter: "[StudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineEnrollments_StudentId",
                table: "OnlineEnrollments",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileResource_OnlineCourseLesson",
                table: "FileResources",
                column: "OnlineCourseLessonId",
                principalTable: "OnlineCourseLessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileResource_OnlineCourseLesson",
                table: "FileResources");

            migrationBuilder.DropTable(
                name: "OnlineCourseLessons");

            migrationBuilder.DropTable(
                name: "OnlineEnrollmentMonthPayments");

            migrationBuilder.DropTable(
                name: "OnlineCourseMonths");

            migrationBuilder.DropTable(
                name: "OnlineEnrollments");

            migrationBuilder.DropTable(
                name: "OnlineCourses");

            migrationBuilder.DropIndex(
                name: "IX_FileResources_OnlineCourseLessonId",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "OnlineCourseLessonId",
                table: "FileResources");
        }
    }
}
