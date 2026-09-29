using HospitalManagementSystem.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HospitalManagementSystem.Api.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260911120000_AddHospitalAssistantConversations")]
public sealed class AddHospitalAssistantConversations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("AssistantConversations", table => new
        {
            AssistantConversationId = table.Column<Guid>(type: "uuid", nullable: false),
            PatientId = table.Column<int>(type: "integer", nullable: false),
            InitialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
            Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
            StateJson = table.Column<string>(type: "text", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
        }, constraints: table => {
            table.PrimaryKey("PK_AssistantConversations", x => x.AssistantConversationId);
            table.ForeignKey("FK_AssistantConversations_Patients_PatientId", x => x.PatientId, "Patients", "PatientId", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_AssistantConversations_PatientId_InitialRequestId", "AssistantConversations", new[] { "PatientId", "InitialRequestId" }, unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("AssistantConversations");
}
