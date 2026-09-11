using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartRFQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorQuoteAndActualPurReplyDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualPurReplyDate",
                table: "DocRequests",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VendorQuotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocRequestItemId = table.Column<int>(type: "integer", nullable: false),
                    VendorName = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric", nullable: true),
                    QuotationFilePath = table.Column<string>(type: "text", nullable: true),
                    IsRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorQuotes_DocRequestItems_DocRequestItemId",
                        column: x => x.DocRequestItemId,
                        principalTable: "DocRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorQuotes_DocRequestItemId",
                table: "VendorQuotes",
                column: "DocRequestItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorQuotes");

            migrationBuilder.DropColumn(
                name: "ActualPurReplyDate",
                table: "DocRequests");
        }
    }
}
