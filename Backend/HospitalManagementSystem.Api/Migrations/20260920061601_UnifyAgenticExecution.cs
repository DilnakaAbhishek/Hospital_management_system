using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HospitalManagementSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class UnifyAgenticExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionWorkflowId",
                table: "TriageWorkflows",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionWorkflowId",
                table: "AppointmentProposals",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgenticExecutions",
                columns: table => new
                {
                    WorkflowId = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    RecordJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgenticExecutions", x => x.WorkflowId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgenticExecutions_PatientId",
                table: "AgenticExecutions",
                column: "PatientId");


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgenticExecutions");


            migrationBuilder.DropColumn(
                name: "ExecutionWorkflowId",
                table: "TriageWorkflows");

            migrationBuilder.DropColumn(
                name: "ExecutionWorkflowId",
                table: "AppointmentProposals");

        }
    }
}
