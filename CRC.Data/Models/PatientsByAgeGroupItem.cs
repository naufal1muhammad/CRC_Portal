namespace CRC.Data.Models
{
    // One slice of the SUPERUSER dashboard's age pie, from spDashboard_Patient_ByAgeGroup: a bucket label
    // and how many patients fall in it. Every patient counts, discharged or not.
    //
    // THE SIX BUCKETS ARE DEFINED IN SQL AND NOWHERE ELSE — "20 and below", "21-40", "41-60", "61-80",
    // "81 and above", "Unknown" — cut from dbo.PatientBasic.Patient_Age by a CASE expression. The
    // boundaries are inclusive on both sides and leave no gap: 20 and 21 are in different buckets, 40 and
    // 41 are in different buckets. Changing a boundary is a .sql change; there is no C# to adjust.
    //
    // 🔴 THE "Unknown" BUCKET IS UNREACHABLE. It fires on `Patient_Age IS NULL`, and `Patient_Age` is
    // `INT NOT NULL` (CoreFlow.md §3.8), so nothing can land in it. It is not dead weight to delete on
    // sight either — it is what keeps the CASE total, and it is the branch that would start returning rows
    // the day the column is made nullable.
    //
    // Unlike the other two dashboard aggregates this one is NOT ordered by count. Its ORDER BY is a second
    // CASE that puts the buckets in AGE ORDER, youngest first, with "Unknown" last — so the ordering is
    // stable, has no ties, and is the chart's x-axis. The caller must not re-sort.
    public class PatientsByAgeGroupItem
    {
        // Never null: the CASE always produces a literal. Typed nullable only because the endpoint that
        // consumes it coerces a null to "Unknown" and nothing here should force that coercion to be dead.
        public string? AgeGroup { get; set; }
        public int PatientCount { get; set; }
    }
}
