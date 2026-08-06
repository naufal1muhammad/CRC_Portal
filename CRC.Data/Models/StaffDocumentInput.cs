namespace CRC.Data.Models
{
    // One dbo.StaffDocument row to insert inside SaveStaffWithDocumentsAsync's transaction — the arguments
    // of spStaffDocument_Insert, minus the two the data layer supplies itself (@Staff_ID, which is only
    // known after the staff row is written, and @User_ID, which is the audit actor).
    //
    // 🔴 BlobName ARRIVES ALREADY UPLOADED. The bytes are in the container by the time one of these is
    // constructed: the caller streams the file to storage and hands the resulting key over. CRC.Data does
    // not upload anything and must never learn how to — IDocumentStorage is a CRC.Web service and this
    // project has no reference to CRC.Web. See CoreFlow.md §6.6 for why the split falls exactly here, and
    // for the compensation the caller owes when the transaction rolls back and the blob does not.
    public class StaffDocumentInput
    {
        // Denormalized onto the audit summary only — spStaffDocument_Insert does not store it on the row,
        // because dbo.StaffDocument has no Staff_Name column.
        public string Staff_Name { get; set; } = string.Empty;

        public string StaffDocumentType_ID { get; set; } = string.Empty;

        // Also audit-only, for the same reason: the row keeps the id and the name is re-joined on read.
        public string StaffDocumentType_Name { get; set; } = string.Empty;

        // The user's file name after DocumentValidation.SafeFileName — path stripped, bounded to 255
        // characters because the column is VARCHAR(255). Never part of the blob key.
        public string FileName { get; set; } = string.Empty;

        // The key inside the private container: staff/{Staff_ID}/{guid}{ext}. Not a URL, not a path.
        public string BlobName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;
    }
}
