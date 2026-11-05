using DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20261105100000_VehicleTripCount")]
    public partial class VehicleTripCount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InternalUse",
                table: "VehiclePermitDetails",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TripCount",
                table: "VehiclePermitDetails",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "InternalUse", table: "VehiclePermitDetails");
            migrationBuilder.DropColumn(name: "TripCount", table: "VehiclePermitDetails");
        }
    }
}
