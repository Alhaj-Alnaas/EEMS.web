using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Must carry [Migration] so EF discovers it. Without the attribute,
    /// startup never applies the Direction column and reader queries fail.
    /// </remarks>
    [DbContext(typeof(DataContext))]
    [Migration("20260928100000_ReaderDirection")]
    public partial class ReaderDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "Readers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "In");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Direction",
                table: "Readers");
        }
    }
}
