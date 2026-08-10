namespace CRC.Data.Models
{
    // One slice of the SUPERUSER dashboard's race pie, from spDashboard_Patient_ByRace: a race name and
    // how many patients carry it. Every patient counts — the procedure has no WHERE clause, so discharged
    // patients are in here alongside active ones (contrast PatientsByDischargeTypeItem, which by its nature
    // sees only the discharged).
    //
    // 🔴 THE ORDERING IS `PatientCount DESC` WITH NO TIE-BREAKER, and that is a property of the data rather
    // than of the chart: two races on the same count come back in whatever order the engine produced them,
    // and the same query can legitimately answer differently on two runs. The chart does not care. A
    // before/after diff does, which is why this area's smoke-test fixture gives every race a distinct count.
    //
    // Race_Name is a purpose-named type rather than a generic {label, count} pair because §4.12 has to say
    // what each of the three charts shows, and "a label and a number" is not that. It is typed nullable for
    // honesty about the LEFT JOIN it comes from, though the procedure's
    // COALESCE(NULLIF(LTRIM(RTRIM(r.Race_Name)), ''), 'Unknown') means a null cannot actually reach here:
    // a Race_ID matching no LU_RACE row, and a race whose name is blank or whitespace, both arrive as the
    // literal "Unknown" and are grouped together under it.
    public class PatientsByRaceItem
    {
        public string? Race_Name { get; set; }
        public int PatientCount { get; set; }
    }
}
