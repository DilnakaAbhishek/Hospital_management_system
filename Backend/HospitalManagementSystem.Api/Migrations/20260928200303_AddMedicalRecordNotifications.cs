using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HospitalManagementSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicalRecordNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate any existing 'Archived' records to 'Finalized' before tightening the constraint
            migrationBuilder.Sql("UPDATE \"MedicalRecords\" SET \"Status\" = 'Finalized' WHERE \"Status\" = 'Archived'");

            // Drop and recreate the MedicalRecords status check constraint to remove 'Archived'
            migrationBuilder.DropCheckConstraint(
                name: "CK_MedicalRecords_Status",
                table: "MedicalRecords");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MedicalRecords_Status",
                table: "MedicalRecords",
                sql: "\"Status\" IN ('Draft', 'Finalized')");

            // Create the MedicalRecordNotifications table
            migrationBuilder.CreateTable(
                name: "MedicalRecordNotifications",
                columns: table => new
                {
                    MedicalRecordNotificationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MedicalRecordId = table.Column<int>(type: "integer", nullable: false),
                    RecipientRole = table.Column<string>(type: "text", nullable: false),
                    RecipientUserId = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalRecordNotifications", x => x.MedicalRecordNotificationId);
                    table.ForeignKey(
                        name: "FK_MedicalRecordNotifications_MedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "MedicalRecords",
                        principalColumn: "MedicalRecordId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecordNotifications_MedicalRecordId",
                table: "MedicalRecordNotifications",
                column: "MedicalRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicalRecordNotifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MedicalRecords_Status",
                table: "MedicalRecords");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MedicalRecords_Status",
                table: "MedicalRecords",
                sql: "\"Status\" IN ('Draft', 'Finalized', 'Archived')");
        }
    }
}
