using System;
using DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20261105090000_PermitClassificationGates")]
    public partial class PermitClassificationGates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermitClassificationGates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermitClassificationGates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermitClassificationGates_Gates_GateId",
                        column: x => x.GateId,
                        principalTable: "Gates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermitClassificationGates_Classification_GateId",
                table: "PermitClassificationGates",
                columns: new[] { "Classification", "GateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermitClassificationGates_GateId",
                table: "PermitClassificationGates",
                column: "GateId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PermitClassificationGates");
        }
    }
}
