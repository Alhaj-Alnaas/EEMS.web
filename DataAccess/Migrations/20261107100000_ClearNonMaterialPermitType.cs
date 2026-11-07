using DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <summary>
    /// استخراج / إدخال / استخراج وإدخال belong to materials permits only.
    /// </summary>
    [DbContext(typeof(DataContext))]
    [Migration("20261107100000_ClearNonMaterialPermitType")]
    public partial class ClearNonMaterialPermitType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Permits
                SET [type] = NULL
                WHERE classification IN (
                    N'Cars', N'سيارات', N'مركبات', N'تصريح سيارات',
                    N'Visitors', N'زوار', N'تصريح زوار',
                    N'EmployeeAccess', N'دخول موظفين')
                   OR [type] = N'زيارة'
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
