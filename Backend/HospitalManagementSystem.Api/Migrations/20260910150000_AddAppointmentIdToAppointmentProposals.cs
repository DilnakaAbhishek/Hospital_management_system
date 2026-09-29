using HospitalManagementSystem.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalManagementSystem.Api.Migrations;

/// <summary>
/// Stores the appointment created after a patient confirms a proposal.
/// The column is nullable because proposals exist before a booking is made.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260910150000_AddAppointmentIdToAppointmentProposals")]
public partial class AddAppointmentIdToAppointmentProposals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AppointmentId",
            table: "AppointmentProposals",
            type: "integer",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AppointmentId",
            table: "AppointmentProposals");
    }
}
