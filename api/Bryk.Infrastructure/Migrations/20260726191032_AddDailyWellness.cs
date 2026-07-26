using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bryk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyWellness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyWellness",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SleepHours = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    SleepQuality = table.Column<int>(type: "int", nullable: true),
                    RestingHr = table.Column<int>(type: "int", nullable: true),
                    WeightKg = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Soreness = table.Column<int>(type: "int", nullable: true),
                    HrvMs = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyWellness", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyWellness_AthleteId_Date",
                table: "DailyWellness",
                columns: new[] { "AthleteId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyWellness");
        }
    }
}
