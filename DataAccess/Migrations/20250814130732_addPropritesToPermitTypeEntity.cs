using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class addPropritesToPermitTypeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "createdBy",
                table: "PermitTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "createdOn",
                table: "PermitTypes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "deletedBy",
                table: "PermitTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "deletedOn",
                table: "PermitTypes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "isDeleted",
                table: "PermitTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "remarks",
                table: "PermitTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "updatedBy",
                table: "PermitTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedOn",
                table: "PermitTypes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "createdBy",
                table: "PermitTypes");

            migrationBuilder.DropColumn(
                name: "createdOn",
                table: "PermitTypes");

            migrationBuilder.DropColumn(
                name: "deletedBy",
                table: "PermitTypes");

            migrationBuilder.DropColumn(
                name: "deletedOn",
                table: "PermitTypes");

            migrationBuilder.DropColumn(
                name: "isDeleted",
                table: "PermitTypes");

            migrationBuilder.DropColumn(
                name: "remarks",
                table: "PermitTypes");

            migrationBuilder.DropColumn(
                name: "updatedBy",
                table: "PermitTypes");

            migrationBuilder.DropColumn(
                name: "updatedOn",
                table: "PermitTypes");
        }
    }
}
