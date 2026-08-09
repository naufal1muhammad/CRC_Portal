namespace CRC.Data.Models
{
    // One dbo.PatientDocument row to insert — the arguments of spPatientDocument_Insert, minus @User_ID,
    // which is the audit ACTOR and is supplied by SqlData from DatabaseHelper.CurrentUserId (CoreFlow.md
    // §0.1). The staff-side twin is StaffDocumentInput; the difference is that this one carries its
    // @Patient_ID, because a patient's id exists long before the upload, whereas a NEW staff member's id
    // does not exist until spStaff_Insert has run inside a transaction.
    //
    // 🔴 BlobName ARRIVES ALREADY UPLOADED. The bytes are in the private container by the time one of these
    // is constructed: StaffPatientController streams the file to storage and hands the resulting key over.
    // CRC.Data does not upload anything and must never learn how to — IDocumentStorage is a CRC.Web service
    // and this project has no reference to CRC.Web. See DOCUMENTSTORAGE.md and CoreFlow.md §6.6.
    //
    // 🔴 THERE IS NO TRANSACTION AROUND A PATIENT-DOCUMENT UPLOAD, and unlike the staff side there is no
    // compensation either. /StaffPatient/UploadPatientDocuments loops the batch and, per file, uploads the
    // blob and then inserts the row; a failure on file three leaves files one and two committed. That is
    // pre-existing behaviour and it is left exactly as found — the staff equivalent needs a transaction
    // because the mandatory-document rule makes a staff row without its documents invalid, and no
    // equivalent rule exists for patients at upload time (the discharge check runs much later, §5.6).
    public class PatientDocumentInput
    {
        public string Patient_ID { get; set; } = string.Empty;

        // Denormalized onto the audit summary only — spPatientDocument_Insert does not store it, because
        // dbo.PatientDocument has no Patient_Name column; reads re-join dbo.PatientBasic for it.
        public string Patient_Name { get; set; } = string.Empty;

        public string PatientDocumentType_ID { get; set; } = string.Empty;

        // Also audit-only, for the same reason.
        public string PatientDocumentType_Name { get; set; } = string.Empty;

        // The user's file name after DocumentValidation.SafeFileName — path stripped, bounded to 255
        // characters because the column is VARCHAR(255). Never part of the blob key.
        public string FileName { get; set; } = string.Empty;

        // The key inside the private container: patients/{Patient_ID}/{guid}{ext}. Not a URL, not a path.
        public string BlobName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;
    }
}
