using System;
using DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20261105140000_PermitVisitors")]
    public partial class PermitVisitors : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ExpectedDepartureTime", table: "VisitorPermitDetails", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ExpectedVisitTime", table: "VisitorPermitDetails", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "HostDepartment", table: "VisitorPermitDetails", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "HostFileNumber", table: "VisitorPermitDetails", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "HostName", table: "VisitorPermitDetails", type: "nvarchar(max)", nullable: true);

            migrationBuilder.CreateTable(
                name: "PermitVisitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    createdBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    createdOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    updatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    isDeleted = table.Column<bool>(type: "bit", nullable: false),
                    deletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    deletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    remarks = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitVisitors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermitVisitors_Permits_PermitId",
                        column: x => x.PermitId,
                        principalTable: "Permits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermitVisitors_PermitId",
                table: "PermitVisitors",
                column: "PermitId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PermitVisitors");
            migrationBuilder.DropColumn(name: "ExpectedDepartureTime", table: "VisitorPermitDetails");
            migrationBuilder.DropColumn(name: "ExpectedVisitTime", table: "VisitorPermitDetails");
            migrationBuilder.DropColumn(name: "HostDepartment", table: "VisitorPermitDetails");
            migrationBuilder.DropColumn(name: "HostFileNumber", table: "VisitorPermitDetails");
            migrationBuilder.DropColumn(name: "HostName", table: "VisitorPermitDetails");
        }
    }
}
