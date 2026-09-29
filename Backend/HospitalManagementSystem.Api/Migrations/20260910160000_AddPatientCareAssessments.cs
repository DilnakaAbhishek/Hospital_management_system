using HospitalManagementSystem.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HospitalManagementSystem.Api.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260910160000_AddPatientCareAssessments")]
public partial class AddPatientCareAssessments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PatientCareAssessments",
            columns: table => new
            {
                PatientCareAssessmentId = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                PatientId = table.Column<int>(type: "integer", nullable: false),
                Symptoms = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                RequestedSpecialty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                RequestedAppointmentProposal = table.Column<bool>(type: "boolean", nullable: false),
                TriageLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ClinicalJson = table.Column<string>(type: "text", nullable: false),
                ProposalJson = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PatientCareAssessments", x => x.PatientCareAssessmentId);
                table.ForeignKey("FK_PatientCareAssessments_Patients_PatientId", x => x.PatientId,
                    principalTable: "Patients", principalColumn: "PatientId", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_PatientCareAssessments_PatientId_CreatedAt", table: "PatientCareAssessments", columns: new[] { "PatientId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "PatientCareAssessments");
}
