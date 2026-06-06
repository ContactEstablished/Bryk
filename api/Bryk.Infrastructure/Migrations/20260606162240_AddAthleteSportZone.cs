using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bryk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAthleteSportZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AthleteSportZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sport = table.Column<int>(type: "int", nullable: false),
                    ZoneNumber = table.Column<int>(type: "int", nullable: false),
                    Metric = table.Column<int>(type: "int", nullable: false),
                    LowerBound = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    UpperBound = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteSportZones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AthleteSportZones_AthleteId_Sport_ZoneNumber_Metric",
                table: "AthleteSportZones",
                columns: new[] { "AthleteId", "Sport", "ZoneNumber", "Metric" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AthleteSportZones");
        }
    }
}
