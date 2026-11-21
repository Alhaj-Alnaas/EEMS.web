using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class LaborRoster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LaborCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Specialty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContractStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PermitCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
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
                    table.PrimaryKey("PK_LaborCompanies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LaborImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    AppliedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_LaborImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborImportBatches_LaborCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "LaborCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LaborWorkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitNo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PassportNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_LaborWorkers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborWorkers_LaborCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "LaborCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LaborImportLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentPermitNo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ProposedPermitNo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PassportNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProposedStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_LaborImportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborImportLines_LaborImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "LaborImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LaborCompanies_Number",
                table: "LaborCompanies",
                column: "Number",
                unique: true,
                filter: "[isDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LaborCompanies_PermitCode",
                table: "LaborCompanies",
                column: "PermitCode",
                unique: true,
                filter: "[isDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LaborImportBatches_CompanyId",
                table: "LaborImportBatches",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborImportLines_BatchId",
                table: "LaborImportLines",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborWorkers_CompanyId",
                table: "LaborWorkers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborWorkers_PermitNo",
                table: "LaborWorkers",
                column: "PermitNo",
                unique: true,
                filter: "[isDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LaborImportLines");

            migrationBuilder.DropTable(
                name: "LaborWorkers");

            migrationBuilder.DropTable(
                name: "LaborImportBatches");

            migrationBuilder.DropTable(
                name: "LaborCompanies");
        }
    }
}
