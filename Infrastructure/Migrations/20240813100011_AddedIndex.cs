using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ElectricityPriceData_EndDate",
                table: "ElectricityPriceDatas",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_ElectricityPriceData_StartDate",
                table: "ElectricityPriceDatas",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_ElectricityPriceData_StartEndDate",
                table: "ElectricityPriceDatas",
                columns: new[] { "StartDate", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ElectricityPriceData_EndDate",
                table: "ElectricityPriceDatas");

            migrationBuilder.DropIndex(
                name: "IX_ElectricityPriceData_StartDate",
                table: "ElectricityPriceDatas");

            migrationBuilder.DropIndex(
                name: "IX_ElectricityPriceData_StartEndDate",
                table: "ElectricityPriceDatas");
        }
    }
}
