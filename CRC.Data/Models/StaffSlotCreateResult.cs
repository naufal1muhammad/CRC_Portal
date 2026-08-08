namespace CRC.Data.Models
{
    // What spStaffSlots_CreateRange answers: how many hours it actually opened, and how many were already
    // open. The procedure ends with `SELECT @CreatedCount AS CreatedCount, @SkippedExistingCount AS
    // SkippedExistingCount`, one row, always — every failure path THROWs first.
    //
    // SKIPPED IS NOT A FAILURE. The insert is a MERGE … WHEN NOT MATCHED against the unique index on
    // (Staff_ID, SlotDate, SlotStartTime), so re-running the same range is idempotent: nothing is
    // duplicated, nothing errors, and SkippedExistingCount is simply the hours that already existed.
    // CreatedCount + SkippedExistingCount is the number of hours the requested range covers — which is why
    // a range that is entirely re-opened answers { 0, N } and is still success = true.
    //
    // Both are COUNT(*) over a table variable, so neither can be NULL.
    public class StaffSlotCreateResult
    {
        public int CreatedCount { get; set; }
        public int SkippedExistingCount { get; set; }
    }
}
