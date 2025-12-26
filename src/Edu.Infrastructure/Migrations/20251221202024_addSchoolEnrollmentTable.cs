using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addSchoolEnrollmentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecordedVideoUrl",
                table: "ReactiveCourseLessons",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReactiveCourseLessonId",
                table: "FileResources",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_ReactiveCourseLessonId",
                table: "FileResources",
                column: "ReactiveCourseLessonId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileResource_ReactiveCourseLesson",
                table: "FileResources",
                column: "ReactiveCourseLessonId",
                principalTable: "ReactiveCourseLessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OnlineCourses_Levels_LevelId",
                table: "OnlineCourses",
                column: "LevelId",
                principalTable: "Levels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileResource_ReactiveCourseLesson",
                table: "FileResources");

            migrationBuilder.DropForeignKey(
                name: "FK_OnlineCourses_Levels_LevelId",
                table: "OnlineCourses");

            migrationBuilder.DropIndex(
                name: "IX_FileResources_ReactiveCourseLessonId",
                table: "FileResources");

            migrationBuilder.DropColumn(
                name: "RecordedVideoUrl",
                table: "ReactiveCourseLessons");

            migrationBuilder.DropColumn(
                name: "ReactiveCourseLessonId",
                table: "FileResources");
        }
    }
}
