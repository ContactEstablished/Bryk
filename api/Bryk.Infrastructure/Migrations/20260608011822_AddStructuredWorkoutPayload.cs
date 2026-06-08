using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bryk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredWorkoutPayload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkoutBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedWorkoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Repeats = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutBlocks_PlannedWorkouts_PlannedWorkoutId",
                        column: x => x.PlannedWorkoutId,
                        principalTable: "PlannedWorkouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutBlockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Intent = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    DistanceMeters = table.Column<int>(type: "int", nullable: true),
                    TargetZone = table.Column<int>(type: "int", nullable: true),
                    TargetPowerLow = table.Column<int>(type: "int", nullable: true),
                    TargetPowerHigh = table.Column<int>(type: "int", nullable: true),
                    TargetHrLow = table.Column<int>(type: "int", nullable: true),
                    TargetHrHigh = table.Column<int>(type: "int", nullable: true),
                    TargetPaceLow = table.Column<int>(type: "int", nullable: true),
                    TargetPaceHigh = table.Column<int>(type: "int", nullable: true),
                    Sets = table.Column<int>(type: "int", nullable: true),
                    Reps = table.Column<int>(type: "int", nullable: true),
                    LoadKg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Rpe = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutSteps_WorkoutBlocks_WorkoutBlockId",
                        column: x => x.WorkoutBlockId,
                        principalTable: "WorkoutBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutBlocks_AthleteId",
                table: "WorkoutBlocks",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutBlocks_PlannedWorkoutId_OrderIndex",
                table: "WorkoutBlocks",
                columns: new[] { "PlannedWorkoutId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSteps_AthleteId",
                table: "WorkoutSteps",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSteps_WorkoutBlockId_OrderIndex",
                table: "WorkoutSteps",
                columns: new[] { "WorkoutBlockId", "OrderIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkoutSteps");

            migrationBuilder.DropTable(
                name: "WorkoutBlocks");
        }
    }
}
