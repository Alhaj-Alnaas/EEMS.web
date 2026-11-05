using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeBloodType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees",
                column: "FileNumber",
                unique: true,
                filter: "[isDeleted] = 0 AND [FileNumber] IS NOT NULL AND [FileNumber] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees",
                column: "FileNumber",
                unique: true);
        }
    }
}
