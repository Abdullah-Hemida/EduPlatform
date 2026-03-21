using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edu.Infrastructure.Migrations
{
    public partial class EditLessonInSchool : Migration
    {
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

            migrationBuilder.AddColumn<int>(
                name: "CurriculumId",
                table: "SchoolLessons",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolLessons_CurriculumId",
                table: "SchoolLessons",
                column: "CurriculumId");

            // Copy CurriculumId from the related module where possible
            migrationBuilder.Sql(@"
UPDATE SL
SET SL.CurriculumId = SM.CurriculumId
FROM SchoolLessons SL
INNER JOIN SchoolModules SM ON SL.ModuleId = SM.Id
WHERE SL.ModuleId IS NOT NULL;
");

            // Ensure a fallback Level + Curriculum exists for lessons that do not belong to a module
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Curricula)
BEGIN
    DECLARE @LevelId INT;

    IF NOT EXISTS (SELECT 1 FROM Levels)
    BEGIN
        INSERT INTO Levels (NameEn, NameIt, NameAr, [Order])
        VALUES (N'Default Level', N'Default Level', N'المستوى الافتراضي', 1);

        SET @LevelId = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        SELECT TOP 1 @LevelId = Id
        FROM Levels
        ORDER BY Id;
    END

    INSERT INTO Curricula (LevelId, Title, [Order])
    VALUES (@LevelId, N'Default Curriculum', 1);
END
");

            // Assign any remaining lessons to the first available curriculum
            migrationBuilder.Sql(@"
DECLARE @CurriculumId INT;

SELECT TOP 1 @CurriculumId = Id
FROM Curricula
ORDER BY Id;

UPDATE SchoolLessons
SET CurriculumId = @CurriculumId
WHERE CurriculumId IS NULL;
");

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

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolLessons_SchoolModules_ModuleId",
                table: "SchoolLessons",
                column: "ModuleId",
                principalTable: "SchoolModules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

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
