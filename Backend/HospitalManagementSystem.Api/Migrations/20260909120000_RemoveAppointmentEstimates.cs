using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalManagementSystem.Api.Migrations
{
    public partial class RemoveAppointmentEstimates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_EstimatedStartAt",
                table: "Appointments");
            migrationBuilder.DropColumn(
                name: "EstimatedStartAt",
                table: "Appointments");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedStartAt",
                table: "Appointments",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: DateTime.MinValue);
            // Restore a usable time from the session when rolling back.
            migrationBuilder.Sql("""
                UPDATE "Appointments" AS a
                SET "EstimatedStartAt" = s."StartAt"
                FROM "DoctorTimeSlots" AS s
                WHERE a."DoctorTimeSlotId" = s."DoctorTimeSlotId";
                """);
            migrationBuilder.CreateIndex(
                name: "IX_Appointments_EstimatedStartAt",
                table: "Appointments",
                column: "EstimatedStartAt");
        }
    }
}
