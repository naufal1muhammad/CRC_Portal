namespace CRC.Data.Models
{
    // The staff half of SaveStaffWithDocumentsAsync's argument list — everything spStaff_Insert or
    // spStaff_Update needs, plus the flag that chooses between them.
    //
    // This is the one place in CRC.Data/Models/ that models an INPUT rather than a result set, and it
    // exists because the alternative is a method with sixteen positional parameters where two adjacent
    // strings are the NRIC and the phone number. A caller cannot transpose two named properties by
    // accident; it can very easily transpose two `string` arguments.
    //
    // Property names mirror the procedure's parameters without the `@`, because SqlData hands this
    // straight to Dapper as the anonymous object's field values — read spStaff_Insert and spStaff_Update
    // side by side with this file.
    //
    // Staff_ID is IGNORED when IsNew is true: spStaff_Insert generates the id itself as
    // {Staff_Type}-{5-digit global sequence} and returns it (CoreFlow.md §3.4). It is required, and
    // validated by the caller, when IsNew is false.
    public class StaffSaveInput
    {
        public bool IsNew { get; set; }

        public string Staff_ID { get; set; } = string.Empty;
        public string Staff_Name { get; set; } = string.Empty;
        public string Staff_NRIC { get; set; } = string.Empty;
        public DateTime Staff_BirthDate { get; set; }
        public int Staff_Age { get; set; }
        public string Staff_Phone { get; set; } = string.Empty;
        public string Staff_Email { get; set; } = string.Empty;
        public string Staff_Gender { get; set; } = string.Empty;
        public string Staff_ResState { get; set; } = string.Empty;
        public string Staff_ResCity { get; set; } = string.Empty;
        public string Staff_ResPostcode { get; set; } = string.Empty;
        public string Staff_AddLine1 { get; set; } = string.Empty;
        public string Staff_AddLine2 { get; set; } = string.Empty;
        public string Staff_Base { get; set; } = string.Empty;

        // dbo.Staff.Staff_Type stores a LU_STAFFTYPE.StaffType_ID — "END", "NUR", "ANE" — and
        // spStaff_Insert RAISERRORs on a blank one because it is also the prefix of the generated
        // Staff_ID. It is a VARCHAR code, never an int (CoreFlow.md §3.1).
        public string Staff_Type { get; set; } = string.Empty;
    }
}
