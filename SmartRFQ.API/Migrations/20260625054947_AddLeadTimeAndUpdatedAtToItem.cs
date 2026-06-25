using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRFQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadTimeAndUpdatedAtToItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDays",
                table: "DocRequestItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "DocRequestItems",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeadTimeDays",
                table: "DocRequestItems");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "DocRequestItems");
        }
    }
}
