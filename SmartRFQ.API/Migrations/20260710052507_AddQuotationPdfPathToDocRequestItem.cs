using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRFQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationPdfPathToDocRequestItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuotationPdfPath",
                table: "DocRequestItems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuotationPdfPath",
                table: "DocRequestItems");
        }
    }
}
