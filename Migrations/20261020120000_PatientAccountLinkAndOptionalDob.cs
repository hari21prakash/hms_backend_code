using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class PatientAccountLinkAndOptionalDob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Self-registered patients do not provide a date of birth at sign-up.
            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Patients",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            // Repair existing data: every Patient-role login must have a linked Patient record.
            // Accounts created before registration created the patient record are the orphans fixed here.
            // Staff-created records are NOT auto-linked by e-mail (that would grant record access silently).
            migrationBuilder.Sql(@"
INSERT INTO ""Patients""
    (""Id"", ""CreatedAt"", ""PatientNumber"", ""UserId"", ""FirstName"", ""LastName"", ""DateOfBirth"", ""Gender"", ""BloodGroup"",
     ""Phone"", ""Email"", ""Address"", ""City"", ""State"", ""EmergencyContactName"", ""EmergencyContactPhone"", ""RegisteredAt"", ""Status"")
SELECT gen_random_uuid(), now(),
       'PAT-' || to_char(now(), 'YYYY') || '-' || lpad(nextval('patient_number_seq')::text, 6, '0'),
       u.""Id"", u.""FirstName"", u.""LastName"", NULL, 'Unknown', 'Unknown',
       COALESCE(u.""PhoneNumber"", ''), u.""Email"", '', '', '', '', '', u.""CreatedAt"", 'Active'
FROM ""Users"" u
JOIN ""Roles"" r ON r.""Id"" = u.""RoleId""
WHERE r.""Name"" = 'Patient'
  AND NOT EXISTS (SELECT 1 FROM ""Patients"" p WHERE p.""UserId"" = u.""Id"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The column was NOT NULL before; give unknown dates a sentinel so the constraint can be restored.
            migrationBuilder.Sql(@"UPDATE ""Patients"" SET ""DateOfBirth"" = DATE '0001-01-01' WHERE ""DateOfBirth"" IS NULL;");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Patients",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
