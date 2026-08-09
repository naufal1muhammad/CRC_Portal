namespace CRC.Data.Models
{
    // One dbo.PatientDocument row with its patient's name and its document-type name resolved.
    //
    // ONE MODEL SERVES TWO PROCEDURES, and here that is correct: spPatientDocument_List and
    // spPatientDocument_GetById select THE SAME NINE COLUMNS in the same order, differing only in their
    // WHERE clause — every document for one patient, versus one document by id. Exactly the call
    // StaffDocumentItem makes for its own pair, and the opposite of UserAuthRecord/UserAccountRecord, where
    // one result set is a strict subset of the other and sharing would hide a lockout.
    //
    // 🔴 BlobName IS ON THIS MODEL AND MUST NEVER REACH A BROWSER. It is the key inside the private blob
    // container — not a URL, not a path — and its only consumers are server-side: minting a five-minute
    // read SAS in GetPatientDocumentUrl, and removing the object after a row has gone.
    // /StaffPatient/GetPatientDocuments deliberately does not project it, and neither must Prompt 8's
    // Documents search. See DOCUMENTSTORAGE.md and CoreFlow.md §8.
    //
    // 🔴 UploadedOn IS A STRING, AND THAT IS THE COLUMN'S TYPE, NOT A CONVENIENCE. dbo.PatientDocument
    // declares `UploadedOn VARCHAR(100) NOT NULL` — a formatted string, not a date — and
    // spPatientDocument_Insert writes
    // `CONVERT(VARCHAR(100), GETUTCDATE() AT TIME ZONE 'UTC' AT TIME ZONE 'Singapore Standard Time', 120)`,
    // which produces "2026-08-09 08:23:21 +08:00". Two consequences:
    //
    //   • It is Malaysian local time with an explicit offset, NOT UTC. Do not SpecifyKind it.
    //   • The ORDER BY on spPatientDocument_List is a STRING sort over this column. It happens to be
    //     chronological because the format is fixed-width and big-endian; it would stop being so the day
    //     anything wrote a differently-shaped string. dbo.StaffDocument.UploadedOn, by contrast, is a real
    //     DATETIME (CoreFlow.md §3.5) — the two document tables disagree about the type of the same idea.
    //
    // Both joined columns are LEFT JOINs onto tables nothing constrains the ids to, so both are nullable:
    // Patient_Name is null when the patient row has gone, and PatientDocumentType_Name is a COALESCE that
    // falls back to the raw PatientDocumentType_ID — which is itself NULL-able on the table, so the
    // coalesce can still yield null.
    public class PatientDocumentItem
    {
        public int PatientDocument_ID { get; set; }
        public string Patient_ID { get; set; } = string.Empty;
        public string? Patient_Name { get; set; }
        public string? PatientDocumentType_ID { get; set; }
        public string? PatientDocumentType_Name { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string BlobName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string? UploadedOn { get; set; }
    }
}
