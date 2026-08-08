namespace CRC.Data.Models
{
    // Grid 3 of spStaff_GetPerformance: one row per distinct complication recorded on a colonoscopy this
    // clinician owns, with how many colonoscopies recorded it.
    //
    // dbo.PatientColonoscopy.Complications is FREE TEXT, not a lookup — the procedure groups on the string
    // itself, so "BLEEDING" and "Bleeding " are two rows. NULL and blank values are excluded by the WHERE,
    // which is why every row here has a real value.
    //
    // Total is COUNT(*) over colonoscopies, NOT over patients — one patient with two colonoscopies that
    // both bled counts twice. Grid 4 is the one that de-duplicates by patient.
    //
    // 🔴 THIS GRID AND GRID 4 ARE BOTH {string, int} AND ARE TOLD APART ONLY BY THEIR POSITION. That is why
    // GetStaffPerformanceAsync reads the grids strictly in order and why swapping two lines there would
    // produce plausible, wrong output that nothing would flag.
    public class StaffPerformanceComplication
    {
        public string? Complication { get; set; }
        public int? Total { get; set; }
    }
}
