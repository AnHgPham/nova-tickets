using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaTickets.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeatAreaId",
                table: "Seats",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TicketTypeId",
                table: "Seats",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeatAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    VenueId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeatAreas_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TicketTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTypes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PerformanceTicketTypes",
                columns: table => new
                {
                    PerformanceId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceTicketTypes", x => new { x.PerformanceId, x.TicketTypeId });
                    table.ForeignKey(
                        name: "FK_PerformanceTicketTypes_Performances_PerformanceId",
                        column: x => x.PerformanceId,
                        principalTable: "Performances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PerformanceTicketTypes_TicketTypes_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "TicketTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_SeatAreaId",
                table: "Seats",
                column: "SeatAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_TicketTypeId",
                table: "Seats",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceTicketTypes_TicketTypeId",
                table: "PerformanceTicketTypes",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatAreas_VenueId_Code",
                table: "SeatAreas",
                columns: new[] { "VenueId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketTypes_Code",
                table: "TicketTypes",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Seats_SeatAreas_SeatAreaId",
                table: "Seats",
                column: "SeatAreaId",
                principalTable: "SeatAreas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Seats_TicketTypes_TicketTypeId",
                table: "Seats",
                column: "TicketTypeId",
                principalTable: "TicketTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Seats_SeatAreas_SeatAreaId",
                table: "Seats");

            migrationBuilder.DropForeignKey(
                name: "FK_Seats_TicketTypes_TicketTypeId",
                table: "Seats");

            migrationBuilder.DropTable(
                name: "PerformanceTicketTypes");

            migrationBuilder.DropTable(
                name: "SeatAreas");

            migrationBuilder.DropTable(
                name: "TicketTypes");

            migrationBuilder.DropIndex(
                name: "IX_Seats_SeatAreaId",
                table: "Seats");

            migrationBuilder.DropIndex(
                name: "IX_Seats_TicketTypeId",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "SeatAreaId",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "TicketTypeId",
                table: "Seats");
        }
    }
}
