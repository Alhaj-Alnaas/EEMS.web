using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ReadersPushProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "Readers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "Readers",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceSerial",
                table: "Readers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "Readers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncAt",
                table: "Readers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Readers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Readers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Offline");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Readers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Preserve previous IsOnline flag into Status before dropping the column.
            migrationBuilder.Sql("""
                UPDATE Readers SET Status = CASE WHEN IsOnline = 1 THEN 'Online' ELSE 'Offline' END;
                UPDATE Readers SET Name = DeviceSerial WHERE Name = '' OR Name IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "Readers");

            migrationBuilder.AddColumn<int>(
                name: "ProtocolCommandId",
                table: "DeviceCommands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentOn",
                table: "DeviceCommands",
                type: "datetime2",
                nullable: true);

            // Align legacy Succeeded status with DDD Acknowledged.
            migrationBuilder.Sql("""
                UPDATE DeviceCommands SET Status = 'Acknowledged' WHERE Status = 'Succeeded';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Readers_DeviceSerial",
                table: "Readers",
                column: "DeviceSerial",
                unique: true,
                filter: "[isDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Readers_DeviceSerial",
                table: "Readers");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "Readers");

            migrationBuilder.DropColumn(
                name: "LastSyncAt",
                table: "Readers");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Readers");

            migrationBuilder.DropColumn(
                name: "ProtocolCommandId",
                table: "DeviceCommands");

            migrationBuilder.DropColumn(
                name: "SentOn",
                table: "DeviceCommands");

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "Readers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE Readers SET IsOnline = CASE WHEN Status = 'Online' THEN 1 ELSE 0 END;
                UPDATE DeviceCommands SET Status = 'Succeeded' WHERE Status = 'Acknowledged';
                """);

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Readers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Readers");

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "Readers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "Readers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(45)",
                oldMaxLength: 45,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceSerial",
                table: "Readers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
