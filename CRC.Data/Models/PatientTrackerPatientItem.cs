namespace CRC.Data.Models
{
    // One row of the Patient Tracker's left-hand patient column, from spPatientTracker_Patients_List:
    // seven columns of dbo.PatientBasic plus a computed flag. EVERY patient is here, active and discharged
    // alike, ordered by name then id — the tracker is a whole-programme view, not a work queue.
    //
    // 🔴 IsStalled IS BUSINESS LOGIC THAT LIVES ONLY IN SQL. A patient is stalled when they have at least
    // one dbo.PatientAppointment row AND the status of their LATEST one is anything other than 'Scheduled'
    // — latest by date, then start time, then identity id, descending. A patient with NO appointment at all
    // is NOT stalled (the LEFT JOIN misses and the CASE returns 0), which is worth saying out loud: someone
    // registered and never booked looks exactly as calm as someone booked for tomorrow. The comparison is
    // an ordinary `=` against the literal 'Scheduled', so it inherits the column's collation
    // (case-insensitive in practice) and any other status value — 'Completed', 'Cancelled', a typo — counts
    // as stalled. See CoreFlow.md §5.10.
    //
    // spPatientTracker_StalledCount_Get computes THE SAME definition a second time, in its own procedure,
    // over an INNER JOIN. Two copies of one rule; changing one and not the other makes the badge disagree
    // with the rows underneath it, and nothing would fail.
    public class PatientTrackerPatientItem
    {
        public string Patient_ID { get; set; } = string.Empty;
        public string Patient_Name { get; set; } = string.Empty;
        public string Patient_NRIC { get; set; } = string.Empty;
        public string Patient_Phone { get; set; } = string.Empty;

        // INT NOT NULL on the table; nullable here because the endpoint coerced a DBNull to 0 and Dapper
        // throws mapping a NULL onto a non-nullable int. The defensive 0 stays reachable.
        public int? Patient_Age { get; set; }

        public string Patient_Gender { get; set; } = string.Empty;

        // NULL for every active patient — the discharge date is written with DischargeType_ID (§3.8).
        public DateTime? Patient_DischargeDate { get; set; }

        // CAST(… AS BIT), so never null. Typed bool? for the same reason BranchDetail.Branch_Status is:
        // Dapper throws mapping a NULL onto a non-nullable bool, and a defensive false must not be a 500.
        public bool? IsStalled { get; set; }
    }
}
