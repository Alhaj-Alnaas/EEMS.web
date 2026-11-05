using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Must carry [Migration] so EF discovers it. This file was previously missing
    /// both the attribute and a Designer.cs, so database update never applied it.
    /// </remarks>
    [DbContext(typeof(DataContext))]
    [Migration("20260913120000_AccessLevelsAndSystemTasks")]
    public partial class AccessLevelsAndSystemTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserType",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "AccessLevelId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccessLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsGateOfficer = table.Column<bool>(type: "bit", nullable: false),
                    ReceivesDeviceAlerts = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AccessLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_SystemTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccessLevelTasks",
                columns: table => new
                {
                    AccessLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLevelTasks", x => new { x.AccessLevelId, x.SystemTaskId });
                    table.ForeignKey(
                        name: "FK_AccessLevelTasks_AccessLevels_AccessLevelId",
                        column: x => x.AccessLevelId,
                        principalTable: "AccessLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccessLevelTasks_SystemTasks_SystemTaskId",
                        column: x => x.SystemTaskId,
                        principalTable: "SystemTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AccessLevelId",
                table: "AspNetUsers",
                column: "AccessLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLevels_Name",
                table: "AccessLevels",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SystemTasks_Code",
                table: "SystemTasks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessLevelTasks_SystemTaskId",
                table: "AccessLevelTasks",
                column: "SystemTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AccessLevels_AccessLevelId",
                table: "AspNetUsers",
                column: "AccessLevelId",
                principalTable: "AccessLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AccessLevels_AccessLevelId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "AccessLevelTasks");

            migrationBuilder.DropTable(
                name: "AccessLevels");

            migrationBuilder.DropTable(
                name: "SystemTasks");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AccessLevelId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AccessLevelId",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "UserType",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
