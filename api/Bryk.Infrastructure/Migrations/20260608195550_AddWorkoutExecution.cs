using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bryk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActualDistanceMeters",
                table: "Workouts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualDurationSeconds",
                table: "Workouts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AvgHr",
                table: "Workouts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ComputedLoad",
                table: "Workouts",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoadOverride",
                table: "Workouts",
                type: "decimal(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxHr",
                table: "Workouts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Workouts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Rpe",
                table: "Workouts",
                type: "decimal(3,1)",
                precision: 3,
                scale: 1,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkoutStepResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkoutStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    ActualDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    ActualDistanceMeters = table.Column<int>(type: "int", nullable: true),
                    AvgPower = table.Column<int>(type: "int", nullable: true),
                    AvgHr = table.Column<int>(type: "int", nullable: true),
                    AvgPace = table.Column<int>(type: "int", nullable: true),
                    Rpe = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutStepResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutStepResults_WorkoutSteps_WorkoutStepId",
                        column: x => x.WorkoutStepId,
                        principalTable: "WorkoutSteps",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkoutStepResults_Workouts_WorkoutId",
                        column: x => x.WorkoutId,
                        principalTable: "Workouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutStepResults_AthleteId",
                table: "WorkoutStepResults",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutStepResults_WorkoutId_OrderIndex",
                table: "WorkoutStepResults",
                columns: new[] { "WorkoutId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutStepResults_WorkoutStepId",
                table: "WorkoutStepResults",
                column: "WorkoutStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkoutStepResults");

            migrationBuilder.DropColumn(
                name: "ActualDistanceMeters",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "ActualDurationSeconds",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "AvgHr",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "ComputedLoad",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "LoadOverride",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "MaxHr",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Workouts");

            migrationBuilder.DropColumn(
                name: "Rpe",
                table: "Workouts");
        }
    }
}
