namespace CRC.Data.Models
{
    // One hit on the SUPERUSER Documents search page — a row of EITHER dbo.PatientDocument OR
    // dbo.StaffDocument, whichever branch of spDocuments_Search ran. One model for both is correct here
    // and is the procedure's own doing: its two branches alias their columns to the SAME seven names
    // (DocumentId, Id, Name, DocumentType, FileName, BlobName, UploadedOn) precisely so that one caller can
    // read either. That is unlike PatientDocumentItem / StaffDocumentItem, which keep their tables' native
    // column names and therefore have to stay two types.
    //
    // 🔴 Id IS THE OWNER'S ID, NOT THE DOCUMENT'S. It is the Patient_ID / Staff_ID the row belongs to — the
    // column the page shows in its "ID" column. DocumentId is the PatientDocument_ID / StaffDocument_ID and
    // is the only one /Documents/DocumentUrl accepts. Swapping them silently returns the wrong file, or
    // none.
    //
    // 🔴 BlobName IS SELECTED BY THE PROCEDURE AND MUST NEVER REACH A BROWSER. It is the key inside the
    // private container. /Documents/Search deliberately does not project it; the bytes are reached only
    // through a five-minute read SAS minted per click. It is carried on the model because the procedure
    // really does return it and a reader should see that, not because anything here needs it. See
    // DOCUMENTSTORAGE.md and CoreFlow.md §8.
    //
    // UploadedOn IS A STRING IN BOTH BRANCHES, and for two different reasons — the one place this model
    // papers over a genuine difference between the two tables. dbo.PatientDocument.UploadedOn is already a
    // VARCHAR(100) holding "2026-08-09 08:23:21 +08:00" (§3.15) and is selected as-is;
    // dbo.StaffDocument.UploadedOn is a real DATETIME (§3.5) and the procedure CONVERTs it with style 120,
    // producing "2026-08-09 08:23:21" — no offset. So the two modes' strings are NOT the same format, and
    // the page prints whichever it is given. Do not parse this column.
    //
    // Every property is nullable because the procedure's third branch — an unrecognised @Mode — is a
    // `SELECT CAST(NULL AS …) … WHERE 1 = 0`, and because both real branches LEFT JOIN the owner and the
    // type lookup. DocumentId in particular is int? for that reason; the endpoint coerces null to 0,
    // exactly as the DataTable code did.
    public class DocumentSearchItem
    {
        public int? DocumentId { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? DocumentType { get; set; }
        public string? FileName { get; set; }
        public string? BlobName { get; set; }
        public string? UploadedOn { get; set; }
    }
}
