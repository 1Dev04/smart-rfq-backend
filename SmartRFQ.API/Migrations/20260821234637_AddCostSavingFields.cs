using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRFQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCostSavingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostSavingReason",
                table: "VendorQuotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalDiscount",
                table: "VendorQuotes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalPrice",
                table: "VendorQuotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalQuotationFilePath",
                table: "VendorQuotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalRemark",
                table: "VendorQuotes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostSavingReason",
                table: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "FinalDiscount",
                table: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "FinalPrice",
                table: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "FinalQuotationFilePath",
                table: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "FinalRemark",
                table: "VendorQuotes");
        }
    }
}
