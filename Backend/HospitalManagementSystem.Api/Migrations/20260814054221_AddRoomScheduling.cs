using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HospitalManagementSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DoctorId",
                table: "DoctorTimeSlots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "DoctorTimeSlots",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    RoomId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RoomName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Floor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.RoomId);
                    table.ForeignKey(
                        name: "FK_Rooms_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorTimeSlots_DoctorId_StartAt_EndAt",
                table: "DoctorTimeSlots",
                columns: new[] { "DoctorId", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorTimeSlots_RoomId_StartAt_EndAt",
                table: "DoctorTimeSlots",
                columns: new[] { "RoomId", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_CreatedByUserId",
                table: "Rooms",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_IsConfirmed",
                table: "Rooms",
                column: "IsConfirmed");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_RoomNumber",
                table: "Rooms",
                column: "RoomNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorTimeSlots_Doctors_DoctorId",
                table: "DoctorTimeSlots",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "DoctorId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DoctorTimeSlots_Rooms_RoomId",
                table: "DoctorTimeSlots",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "RoomId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql("""
                ALTER TABLE "DoctorTimeSlots"
                ADD CONSTRAINT "EX_DoctorTimeSlots_Room_NoOverlap"
                EXCLUDE USING gist (
                    "RoomId" WITH =,
                    tsrange("StartAt", "EndAt", '[)') WITH &&
                ) WHERE ("IsActive" = TRUE AND "RoomId" IS NOT NULL);
                """);
            migrationBuilder.Sql("""
                ALTER TABLE "DoctorTimeSlots"
                ADD CONSTRAINT "EX_DoctorTimeSlots_Doctor_NoOverlap"
                EXCLUDE USING gist (
                    "DoctorId" WITH =,
                    tsrange("StartAt", "EndAt", '[)') WITH &&
                ) WHERE ("IsActive" = TRUE AND "DoctorId" IS NOT NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"DoctorTimeSlots\" DROP CONSTRAINT IF EXISTS \"EX_DoctorTimeSlots_Room_NoOverlap\";");
            migrationBuilder.Sql("ALTER TABLE \"DoctorTimeSlots\" DROP CONSTRAINT IF EXISTS \"EX_DoctorTimeSlots_Doctor_NoOverlap\";");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorTimeSlots_Doctors_DoctorId",
                table: "DoctorTimeSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_DoctorTimeSlots_Rooms_RoomId",
                table: "DoctorTimeSlots");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_DoctorTimeSlots_DoctorId_StartAt_EndAt",
                table: "DoctorTimeSlots");

            migrationBuilder.DropIndex(
                name: "IX_DoctorTimeSlots_RoomId_StartAt_EndAt",
                table: "DoctorTimeSlots");

            migrationBuilder.DropColumn(
                name: "DoctorId",
                table: "DoctorTimeSlots");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "DoctorTimeSlots");
        }
    }
}
