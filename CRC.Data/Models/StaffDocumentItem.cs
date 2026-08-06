namespace CRC.Data.Models
{
    // One dbo.StaffDocument row with its owner's name and its document-type name resolved.
    //
    // ONE MODEL SERVES TWO PROCEDURES, and this time that is correct rather than convenient:
    // spStaffDocument_List and spStaffDocument_GetById select THE SAME NINE COLUMNS in the same order,
    // differing only in their WHERE clause (all documents for a staff member, versus one document by id).
    // Compare BranchDetail, which shares for the same reason, and UserAuthRecord/UserAccountRecord, which
    // deliberately do not.
    //
    // 🔴 BlobName IS ON THIS MODEL AND MUST NEVER REACH A BROWSER. It is the key inside the private blob
    // container — not a URL and not a path — and the only server-side consumers are the three that need it:
    // minting a five-minute read SAS in GetStaffDocumentUrl, and deleting the object after a document row
    // or a whole staff member has gone. /Staff/GetStaffDocuments deliberately does not project it. See
    // DOCUMENTSTORAGE.md and CoreFlow.md §8.
    //
    // Staff_Name and StaffDocumentType_Name are both LEFT JOINs, so both are nullable: a document whose
    // Staff_ID matches no staff row (nothing enforces that it does) has a null Staff_Name, and
    // StaffDocumentType_Name is a COALESCE that falls back to the raw StaffDocumentType_ID — which is
    // itself nullable on the table, so the coalesce can still produce null.
    public class StaffDocumentItem
    {
        public int StaffDocument_ID { get; set; }
        public string Staff_ID { get; set; } = string.Empty;
        public string? Staff_Name { get; set; }
        public string? StaffDocumentType_ID { get; set; }
        public string? StaffDocumentType_Name { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string BlobName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;

        // DATETIME NOT NULL on dbo.StaffDocument, and nullable here for the same defensive reason as
        // StaffDetail.Staff_BirthDate: the endpoint renders it with a plain .ToString() that produced ""
        // for a DBNull, and Dapper would throw rather than reproduce that.
        //
        // It is NOT UTC. spStaffDocument_Insert stamps
        // `GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time'` — Malaysian local time,
        // stored with no offset. Do not SpecifyKind(…, Utc) on it the way §4.3's user timestamps are.
        public DateTime? UploadedOn { get; set; }
    }
}
