using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalManagementSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleConsultationFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConsultationFee",
                table: "DoctorTimeSlots",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DoctorTimeSlots_ConsultationFee_NonNegative",
                table: "DoctorTimeSlots",
                sql: "\"ConsultationFee\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DoctorTimeSlots_ConsultationFee_NonNegative",
                table: "DoctorTimeSlots");

            migrationBuilder.DropColumn(
                name: "ConsultationFee",
                table: "DoctorTimeSlots");
        }
    }
}
