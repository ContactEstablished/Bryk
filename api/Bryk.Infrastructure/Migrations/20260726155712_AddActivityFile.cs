using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bryk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    ByteSize = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ParsedWorkoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ZoneHistogramJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFiles_AthleteId",
                table: "ActivityFiles",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFiles_ParsedWorkoutId",
                table: "ActivityFiles",
                column: "ParsedWorkoutId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityFiles");
        }
    }
}
