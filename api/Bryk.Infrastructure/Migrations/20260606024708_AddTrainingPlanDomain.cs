using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bryk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingPlanDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Methodology = table.Column<int>(type: "int", nullable: false),
                    BuildWeeks = table.Column<int>(type: "int", nullable: true),
                    RecoveryWeeks = table.Column<int>(type: "int", nullable: true),
                    RecoveryWeekPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingPlans_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingPlans_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PlannedWorkouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sport = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PlannedDurationMinutes = table.Column<int>(type: "int", nullable: true),
                    PlannedLoad = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedWorkouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannedWorkouts_TrainingPlans_TrainingPlanId",
                        column: x => x.TrainingPlanId,
                        principalTable: "TrainingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedWorkoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Sport = table.Column<int>(type: "int", nullable: false),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workouts_PlannedWorkouts_PlannedWorkoutId",
                        column: x => x.PlannedWorkoutId,
                        principalTable: "PlannedWorkouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedWorkouts_AthleteId_ScheduledDate",
                table: "PlannedWorkouts",
                columns: new[] { "AthleteId", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedWorkouts_TrainingPlanId",
                table: "PlannedWorkouts",
                column: "TrainingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPlans_AthleteId",
                table: "TrainingPlans",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPlans_EventId",
                table: "TrainingPlans",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Workouts_AthleteId",
                table: "Workouts",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_Workouts_PlannedWorkoutId",
                table: "Workouts",
                column: "PlannedWorkoutId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Workouts");

            migrationBuilder.DropTable(
                name: "PlannedWorkouts");

            migrationBuilder.DropTable(
                name: "TrainingPlans");
        }
    }
}
