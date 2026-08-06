namespace CRC.Data.Models
{
    // A dbo.StaffDocument row that SaveStaffWithDocumentsAsync deleted inside its transaction, paired with
    // the blob key it pointed at. Both halves are needed after the commit and neither is available before
    // it: the key to reclaim the storage, the id to name the row on the deferred
    // AuditLog.StaffDocumentDeleted line.
    //
    // The pair is read from spStaffDocument_GetById BEFORE spStaffDocument_Delete runs, because the row is
    // gone afterwards. spStaffDocument_Delete *does* also hand the key back through an OUTPUT parameter,
    // and the standalone DeleteStaffDocumentAsync uses that — but the transactional path keeps the
    // read-then-delete pair the controller has always performed, so a document removed during a save and a
    // document removed on its own still take the same two steps in the same order.
    public class StaffDocumentDeletion
    {
        public int StaffDocument_ID { get; set; }
        public string BlobName { get; set; } = string.Empty;
    }
}
