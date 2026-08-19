using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MovementLogDevicePin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MovementLog_EmployeeOrPermit",
                table: "MovementLogs");

            migrationBuilder.AddColumn<string>(
                name: "DevicePin",
                table: "MovementLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovementLog_EmployeeOrPermit",
                table: "MovementLogs",
                sql: "[EmployeeId] IS NOT NULL OR [PermitId] IS NOT NULL OR ([DevicePin] IS NOT NULL AND [DevicePin] <> '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MovementLog_EmployeeOrPermit",
                table: "MovementLogs");

            migrationBuilder.DropColumn(
                name: "DevicePin",
                table: "MovementLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovementLog_EmployeeOrPermit",
                table: "MovementLogs",
                sql: "[EmployeeId] IS NOT NULL OR [PermitId] IS NOT NULL");
        }
    }
}
