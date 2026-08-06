namespace CRC.Data.Models
{
    // What SaveStaffWithDocumentsAsync hands back after its transaction COMMITS. Nothing in here is
    // meaningful otherwise: the method throws on failure, and a throw means the transaction rolled back and
    // no result was produced.
    //
    // Its whole job is to carry the two facts the caller cannot know until the commit returned:
    //   • the Staff_ID, which spStaff_Insert generated inside the transaction; and
    //   • what to do to blob storage and to the audit channel NOW THAT the rows are certainly durable.
    public class StaffSaveResult
    {
        // The saved staff member's id — the caller's own Staff_ID on an update, or the newly generated
        // {Staff_Type}-{5-digit sequence} on an insert.
        public string StaffId { get; set; } = string.Empty;

        // The documents whose rows were deleted inside the transaction, with the container keys that are
        // now safe to remove. They are removed AFTER the commit, never before: a rolled-back transaction
        // puts the rows back, and a deleted blob cannot be un-deleted.
        public List<StaffDocumentDeletion> RemovedDocuments { get; set; } = new();
    }
}
