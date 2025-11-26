using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EditLessonInSchool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchoolLessons_SchoolModules_ModuleId",
                table: "SchoolLessons");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "SchoolLessons",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "ModuleId",
                table: "SchoolLessons",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // 1) add CurriculumId nullable
            migrationBuilder.AddColumn<int>(
                name: "CurriculumId",
                table: "SchoolLessons",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolLessons_CurriculumId",
                table: "SchoolLessons",
                column: "CurriculumId");

            // 2) Populate CurriculumId for lessons that reference a module
            migrationBuilder.Sql(@"
        UPDATE SL
        SET CurriculumId = SM.CurriculumId
        FROM SchoolLessons SL
        INNER JOIN SchoolModules SM ON SL.ModuleId = SM.Id
        WHERE SL.ModuleId IS NOT NULL;
    ");

            // 3) Ensure there is at least one curriculum to use as fallback for module-less lessons
            //    If none exist we raise an error so migration stops and you can create a curriculum first.
            migrationBuilder.Sql(@"
        IF (SELECT COUNT(*) FROM Curricula) = 0
        BEGIN
            RAISERROR('No curricula found in database. Create at least one curriculum before applying this migration.', 16, 1);
        END
    ");

            // 4) Set any remaining NULL CurriculumId (lessons without modules) to the first curriculum id (fallback)
            migrationBuilder.Sql(@"
        DECLARE @cid INT;
        SELECT TOP 1 @cid = Id FROM Curricula ORDER BY Id;
        UPDATE SchoolLessons SET CurriculumId = @cid WHERE CurriculumId IS NULL;
    ");

            // 5) Now make CurriculumId NOT NULL and add FK
            migrationBuilder.AlterColumn<int>(
                name: "CurriculumId",
                table: "SchoolLessons",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolLessons_Curricula_CurriculumId",
                table: "SchoolLessons",
                column: "CurriculumId",
                principalTable: "Curricula",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 6) Re-create ModuleId FK with SetNull
            migrationBuilder.AddForeignKey(
                name: "FK_SchoolLessons_SchoolModules_ModuleId",
                table: "SchoolLessons",
                column: "ModuleId",
                principalTable: "SchoolModules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchoolLessons_Curricula_CurriculumId",
                table: "SchoolLessons");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolLessons_SchoolModules_ModuleId",
                table: "SchoolLessons");

            migrationBuilder.DropIndex(
                name: "IX_SchoolLessons_CurriculumId",
                table: "SchoolLessons");

            migrationBuilder.DropColumn(
                name: "CurriculumId",
                table: "SchoolLessons");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "SchoolLessons",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<int>(
                name: "ModuleId",
                table: "SchoolLessons",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolLessons_SchoolModules_ModuleId",
                table: "SchoolLessons",
                column: "ModuleId",
                principalTable: "SchoolModules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
