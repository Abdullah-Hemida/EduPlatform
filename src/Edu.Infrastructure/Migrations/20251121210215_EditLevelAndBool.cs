using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EditLevelAndBool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CVStorageKey",
                table: "Teachers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForChildren",
                table: "PrivateCourses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                    name: "NameEn",
                    table: "Levels",
                    type: "nvarchar(200)",
                    maxLength: 200,
                    nullable: false,
                    defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameIt",
                table: "Levels",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "Levels",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Migrate existing old Name → Arabic (because your original levels were Arabic)
            migrationBuilder.Sql(@"
            UPDATE Levels SET 
            NameAr = Name, 
            NameEn = Name, 
            NameIt = Name
           ");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Levels");


            migrationBuilder.AddColumn<string>(
                name: "CoverImageKey",
                table: "Curricula",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CVStorageKey",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "IsForChildren",
                table: "PrivateCourses");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Levels",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Levels SET Name = NameAr");

            migrationBuilder.DropColumn(name: "NameEn", table: "Levels");
            migrationBuilder.DropColumn(name: "NameIt", table: "Levels");
            migrationBuilder.DropColumn(name: "NameAr", table: "Levels");

            migrationBuilder.DropColumn(
            name: "CoverImageKey",
             table: "Curricula");
        }
    }
}
