namespace CRC.Data.Models
{
    // Everything spStaff_GetPerformance answers about one clinician, in one object.
    //
    // 🔴 THE PROCEDURE RETURNS FOUR RESULT SETS, NOT FIVE. DapperLayerPlan.md's Prompt 4 calls it a
    // "five-result-set procedure"; the .sql has four SELECTs at statement level (the fifth match a reader
    // finds is the SELECT inside the `Findings` CTE of grid 4), the deployed procedure in CRC_DB agrees,
    // and StaffPerformanceController has only ever read ds.Tables[0..3]. Four is the contract.
    //
    //   grid 1  ONE row     TotalColonoscopy INT, TotalColonoscopyThisMonth INT   → the two properties here
    //   grid 2  N rows      PjAppType_ID, PjAppType_Name, TotalHours              → HoursByType
    //   grid 3  N rows      Complication, Total                                   → Complications
    //   grid 4  N rows      TypeOfAnomaly, PatientCount                           → Anomalies
    //
    // THE ORDER IS THE WHOLE CONTRACT. Nothing in a result set says which grid it is, and grids 3 and 4 are
    // both {string, int} — read them the wrong way round and the page renders complications under the
    // anomalies heading, with no exception and nothing in a log. GetStaffPerformanceAsync reads them in the
    // order above and only in that order.
    //
    // 🔴 GRID 1's SUMS ARE GENUINELY NULLABLE, and that is not defensive typing. It is
    // `SELECT SUM(CASE …) FROM dbo.PatientJourney WHERE Staff_ID = @Staff_ID` with NO GROUP BY, so for a
    // staff member with no journey rows it returns one row containing two NULLs — not zero rows. The old
    // DataTable code turned those into 0 with a DBNull check; the controller keeps doing that with `?? 0`.
    // Typing them `int` would make "a clinician who has done nothing yet" a 500.
    //
    // The three lists are populated by GetStaffPerformanceAsync after grid 1 is read; Dapper ignores them
    // when it maps grid 1, exactly as it ignores StaffDeleteResult.BlobNames.
    public class StaffPerformanceResult
    {
        public int? TotalColonoscopy { get; set; }
        public int? TotalColonoscopyThisMonth { get; set; }

        public List<StaffPerformanceHours> HoursByType { get; set; } = new();
        public List<StaffPerformanceComplication> Complications { get; set; } = new();
        public List<StaffPerformanceAnomaly> Anomalies { get; set; } = new();
    }
}
