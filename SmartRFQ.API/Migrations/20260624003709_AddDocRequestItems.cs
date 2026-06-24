using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartRFQ.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDocRequestItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RfqNo",
                table: "AuditLogs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<int>(
                name: "DocRequestId",
                table: "AuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RfqNo = table.Column<string>(type: "text", nullable: false),
                    RevNo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LeadTime = table.Column<int>(type: "integer", nullable: true),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocRequests_Users_PurchaserId",
                        column: x => x.PurchaserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocRequests_Users_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

       
            migrationBuilder.CreateTable(
                name: "DocRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocRequestId = table.Column<int>(type: "integer", nullable: false),
                    TargetPURreply = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProjectName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    GlCode = table.Column<string>(type: "text", nullable: false),
                    SapItem = table.Column<string>(type: "text", nullable: true),
                    ItemDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SpecPartNo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "text", nullable: true),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ForGas = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    SpecPurity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CylinderType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CylinderSize = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MakerSource = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RequiredValve = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PurposeApplication = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Customer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AddressLocation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecommendVendor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AttachDwgPath = table.Column<string>(type: "text", nullable: true),
                    AttachSpecPath = table.Column<string>(type: "text", nullable: true),
                    AttachQuotationPath = table.Column<string>(type: "text", nullable: true),
                    AttachEtcPath = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocRequestItems_DocRequests_DocRequestId",
                        column: x => x.DocRequestId,
                        principalTable: "DocRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocRequestItems_DocRequestId",
                table: "DocRequestItems",
                column: "DocRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DocRequests_CreatedAt",
                table: "DocRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocRequests_PurchaserId",
                table: "DocRequests",
                column: "PurchaserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocRequests_RequesterId",
                table: "DocRequests",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_DocRequests_RfqNo",
                table: "DocRequests",
                column: "RfqNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocRequests_Status",
                table: "DocRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocRequestItems");

        
            migrationBuilder.DropTable(
                name: "DocRequests");

            migrationBuilder.DropColumn(
                name: "DocRequestId",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "RfqNo",
                table: "AuditLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
