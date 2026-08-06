namespace CRC.Data.Models
{
    // One row of the Admin > Staff table, from spStaff_List: seven columns, ordered by Staff_Name.
    //
    // It is NOT a reduced StaffDetail even though every column here also appears there. spStaff_List omits
    // the birth date, the age, the gender, the whole residential address and the base branch — eleven
    // columns — so sharing StaffDetail would hand every caller a staff member with no address and an age of
    // zero, silently, exactly the failure mode §5.3 describes for spUsers_GetById. A model describes the
    // result set it maps or it stops being documentation.
    //
    // StaffType_Name comes from a LEFT JOIN onto LU_STAFFTYPE, so it is null when Staff_Type holds a code
    // that no longer exists in the lookup — the join is the only thing connecting them, since nucentra has
    // no foreign key here (§3.4). The other six columns are NOT NULL on dbo.Staff.
    public class StaffListItem
    {
        public string Staff_ID { get; set; } = string.Empty;
        public string Staff_Name { get; set; } = string.Empty;
        public string Staff_NRIC { get; set; } = string.Empty;
        public string Staff_Phone { get; set; } = string.Empty;
        public string Staff_Email { get; set; } = string.Empty;
        public string Staff_Type { get; set; } = string.Empty;
        public string? StaffType_Name { get; set; }
    }
}
