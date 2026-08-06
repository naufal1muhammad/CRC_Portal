namespace CRC.Data.Models
{
    // A whole dbo.Staff row plus the joined staff-type name, from spStaff_GetById (SELECT TOP 1 — at most
    // one row, possibly none). This is what the Edit Staff form and the read-only My Profile page load.
    //
    // Every column except StaffType_Name is NOT NULL on dbo.Staff, yet two are nullable here on purpose,
    // and both mirror a defensive coercion the endpoint has always performed:
    //
    //   • Staff_BirthDate is DateTime? because the DataTable code this replaced guarded it with
    //     `value == DBNull.Value ? null : …` and returned a JSON null rather than throwing. Dapper THROWS
    //     mapping a NULL onto a non-nullable DateTime, so a non-nullable property would turn a harmless
    //     null birth date into a 500.
    //   • Staff_Age is int? for the same reason — the endpoint coerces a null to 0 and must keep doing so.
    //
    // StaffType_Name is genuinely nullable: it comes from a LEFT JOIN onto LU_STAFFTYPE, and nothing
    // constrains Staff.Staff_Type to that table (§3.4).
    public class StaffDetail
    {
        public string Staff_ID { get; set; } = string.Empty;
        public string Staff_Name { get; set; } = string.Empty;
        public string Staff_NRIC { get; set; } = string.Empty;
        public DateTime? Staff_BirthDate { get; set; }
        public int? Staff_Age { get; set; }
        public string Staff_Phone { get; set; } = string.Empty;
        public string Staff_Email { get; set; } = string.Empty;
        public string? Staff_Gender { get; set; }
        public string? Staff_ResState { get; set; }
        public string? Staff_ResCity { get; set; }
        public string? Staff_ResPostcode { get; set; }
        public string? Staff_AddLine1 { get; set; }
        public string? Staff_AddLine2 { get; set; }
        public string? Staff_Base { get; set; }
        public string? Staff_Type { get; set; }
        public string? StaffType_Name { get; set; }
    }
}
