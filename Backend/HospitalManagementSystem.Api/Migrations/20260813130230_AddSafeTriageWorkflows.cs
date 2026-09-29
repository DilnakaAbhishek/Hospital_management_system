using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HospitalManagementSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSafeTriageWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TriageWorkflows",
                columns: table => new
                {
                    TriageWorkflowId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TriageLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UncertaintyState = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    Symptoms = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    VitalsJson = table.Column<string>(type: "text", nullable: true),
                    PlanJson = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    FinalOutcome = table.Column<string>(type: "text", nullable: true),
                    RuleSetVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WorkflowVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageWorkflows", x => x.TriageWorkflowId);
                    table.ForeignKey(
                        name: "FK_TriageWorkflows_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TriageWorkflows_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TriageWorkflowEvents",
                columns: table => new
                {
                    TriageWorkflowEventId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TriageWorkflowId = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageWorkflowEvents", x => x.TriageWorkflowEventId);
                    table.ForeignKey(
                        name: "FK_TriageWorkflowEvents_TriageWorkflows_TriageWorkflowId",
                        column: x => x.TriageWorkflowId,
                        principalTable: "TriageWorkflows",
                        principalColumn: "TriageWorkflowId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TriageWorkflowEvents_TriageWorkflowId_CreatedAt",
                table: "TriageWorkflowEvents",
                columns: new[] { "TriageWorkflowId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TriageWorkflows_PatientId_CreatedAt",
                table: "TriageWorkflows",
                columns: new[] { "PatientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TriageWorkflows_ReviewedByUserId",
                table: "TriageWorkflows",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TriageWorkflows_Status_ApprovalStatus",
                table: "TriageWorkflows",
                columns: new[] { "Status", "ApprovalStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TriageWorkflowEvents");

            migrationBuilder.DropTable(
                name: "TriageWorkflows");
        }
    }
}
