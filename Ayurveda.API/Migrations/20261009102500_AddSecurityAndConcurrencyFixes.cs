using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ayurveda.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityAndConcurrencyFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "OtpCode",
                table: "PasswordResetOtps");

            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "PasswordResetOtps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CreatedFromIp",
                table: "PasswordResetOtps",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtpHashed",
                table: "PasswordResetOtps",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "Doctors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndUtc",
                table: "Doctors",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartUtc",
                table: "Appointments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "Admins",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndUtc",
                table: "Admins",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Admins",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Doctors_ContactNumber",
                table: "Doctors",
                column: "ContactNumber",
                unique: true);

            // Backfill StartUtc from AppointmentDate + TimeSlot
            migrationBuilder.Sql(@"
                UPDATE Appointments
                SET StartUtc = CASE 
                    WHEN TimeSlot LIKE '%Morning%' THEN DATEADD(minute, 210, CAST(CAST(AppointmentDate AS DATE) AS DATETIME2))
                    WHEN TimeSlot LIKE '%Afternoon%' THEN DATEADD(minute, 510, CAST(CAST(AppointmentDate AS DATE) AS DATETIME2))
                    WHEN TimeSlot LIKE '%Evening%' THEN DATEADD(minute, 690, CAST(CAST(AppointmentDate AS DATE) AS DATETIME2))
                    ELSE DATEADD(minute, 210, CAST(CAST(AppointmentDate AS DATE) AS DATETIME2))
                END
                WHERE StartUtc = '0001-01-01' OR StartUtc IS NULL;

                -- Resolve any remaining collisions for active appointments with the same DoctorId and StartUtc
                ;WITH Collisions AS (
                    SELECT Id, DoctorId, StartUtc, Status,
                           ROW_NUMBER() OVER (PARTITION BY DoctorId, StartUtc ORDER BY Id) AS rn
                    FROM Appointments
                    WHERE [Status] <> 'Cancelled' AND [Status] <> 'Declined'
                )
                UPDATE a
                SET a.StartUtc = DATEADD(minute, (c.rn - 1) * 30, a.StartUtc)
                FROM Appointments a
                JOIN Collisions c ON a.Id = c.Id
                WHERE c.rn > 1;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_StartUtc",
                table: "Appointments",
                columns: new[] { "DoctorId", "StartUtc" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [Status] <> 'Declined'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Doctors_ContactNumber",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_StartUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEndUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "PasswordResetOtps");

            migrationBuilder.DropColumn(
                name: "CreatedFromIp",
                table: "PasswordResetOtps");

            migrationBuilder.DropColumn(
                name: "OtpHashed",
                table: "PasswordResetOtps");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "LockoutEndUtc",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "StartUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "LockoutEndUtc",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Admins");

            migrationBuilder.AddColumn<string>(
                name: "OtpCode",
                table: "PasswordResetOtps",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments",
                column: "DoctorId");
        }
    }
}
