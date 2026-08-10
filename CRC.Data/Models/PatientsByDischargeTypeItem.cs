namespace CRC.Data.Models
{
    // One bar of the SUPERUSER dashboard's discharge chart, from spDashboard_Patient_ByDischargeType: a
    // discharge reason and how many patients left the programme under it.
    //
    // 🔴 THIS IS THE ONE DASHBOARD AGGREGATE WITH A `WHERE`, AND IT IS THE WHOLE MEANING OF THE CHART.
    // `WHERE pb.DischargeType_ID IS NOT NULL` — so it counts DISCHARGED PATIENTS ONLY, and a NULL discharge
    // type is the definition of an active patient (§3.8). The race and age charts count everybody. The
    // three charts therefore do NOT add up to the same total, and that is correct rather than a bug to
    // reconcile: this one answers "of the patients who have left, why", not "of all patients, what".
    //
    // The COALESCE still exists on the name — a DischargeType_ID that matches no LU_DISCHARGETYPE row, or
    // one whose name is blank, is labelled "Unknown" — but the WHERE means "Unknown" here can only ever
    // mean "discharged under a reason the lookup no longer knows about", never "not discharged".
    //
    // Ordered by PatientCount DESC with no tie-breaker; see PatientsByRaceItem for why that matters to a
    // diff and not to the chart.
    public class PatientsByDischargeTypeItem
    {
        public string? DischargeType_Name { get; set; }
        public int PatientCount { get; set; }
    }
}
