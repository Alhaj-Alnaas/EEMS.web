using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MovementLogDeviceTxnIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeviceTxnIndex",
                table: "MovementLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovementLogs_ReaderId_DeviceTxnIndex",
                table: "MovementLogs",
                columns: new[] { "ReaderId", "DeviceTxnIndex" },
                unique: true,
                filter: "[isDeleted] = 0 AND [DeviceTxnIndex] IS NOT NULL AND [ReaderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovementLogs_ReaderId_DeviceTxnIndex",
                table: "MovementLogs");

            migrationBuilder.DropColumn(
                name: "DeviceTxnIndex",
                table: "MovementLogs");
        }
    }
}
