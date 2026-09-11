using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRFQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorQuoteExtraFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "VendorName",
                table: "VendorQuotes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "BuyerEmail",
                table: "VendorQuotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUserSelected",
                table: "VendorQuotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ItemDescription",
                table: "VendorQuotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "VendorQuotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "VendorQuotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecPartNo",
                table: "VendorQuotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserDiffReason",
                table: "VendorQuotes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyerEmail",
                table: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "IsUserSelected",
                table: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "ItemDescription",
                table: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "Remark",
                table: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "SpecPartNo",
                table: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "UserDiffReason",
                table: "VendorQuotes");

            migrationBuilder.AlterColumn<string>(
                name: "VendorName",
                table: "VendorQuotes",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
