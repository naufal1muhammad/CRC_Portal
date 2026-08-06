namespace CRC.Data.Models
{
    // What spStaff_Delete answers. THE PROCEDURE RETURNS TWO RESULT SETS, ALWAYS, AND THIS TYPE IS BOTH OF
    // THEM — which is why DeleteStaffAsync is a QueryMultipleAsync and not an ExecuteAsync.
    //
    // Grid 1, one row:   Status VARCHAR(20), Message VARCHAR(500)
    // Grid 2, N rows:    BlobName VARCHAR(500)
    //
    // 🔴 THE GRID COUNT IS STABLE; THE SECOND GRID MAY BE EMPTY. Both early-return branches — "no staff
    // with that id" and "blocked by a reference" — emit a `SELECT TOP 0 CAST(NULL AS VARCHAR(500)) AS
    // [BlobName]` placeholder before returning, precisely so a caller can always read two grids in order
    // without inspecting the status first. The success path can also return zero rows there, simply because
    // the staff member had no documents. An empty grid 2 therefore means "nothing to delete from storage",
    // never "something went wrong" — the Status is the only thing that says whether the delete happened.
    //
    // Status is one of exactly three strings, compared case-insensitively by the caller:
    //   "Success"  — the rows are gone; BlobNames are the container keys to delete afterwards
    //   "Blocked"  — nothing was deleted; Message names the tables still referencing this staff member
    //   "NotFound" — no staff row had that id
    public class StaffDeleteResult
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        // The blob keys of every document the deleted staff member owned, captured by the procedure BEFORE
        // it deleted the dbo.StaffDocument rows. Storage takes no part in the database transaction, so the
        // keys have to travel back out and be removed by the caller after the fact.
        public List<string> BlobNames { get; set; } = new();
    }
}
