namespace CRC.Data.Models
{
    // One active patient as the daily screening sweep sees them, from spAgentPatient_ListScreeningQueue:
    // ten columns per row, every active patient (DischargeType_ID IS NULL — CoreFlow.md §3.8), ordered
    // Patient_ID DESC. Three of the ten are DERIVED IN SQL and exist nowhere in dbo.PatientBasic —
    // NricLast4, ScreeningState and OpenAppointmentCount — which is the whole reason this procedure exists
    // rather than the portal's own spPatientBasic_ListActive (five columns, none of them derived).
    //
    // 🔴 THE FULL NRIC IS NOT ON THIS MODEL AND MUST NEVER BE ADDED. The procedure projects
    // CAST(RIGHT(LTRIM(RTRIM(Patient_NRIC)), 4) AS VARCHAR(4)) and does not select the column at all. The
    // agent confirms identity by asking a patient for four digits and comparing; it must never be ABLE to
    // state more. Widening this model is a privacy decision, not a convenience (AgentApiPlan.md, "What
    // must never leave through this API").
    //
    // ── NULLABILITY, COLUMN BY COLUMN ───────────────────────────────────────────────────────────────────
    //
    // Dapper THROWS mapping a NULL onto a non-nullable value type, so a wrong choice here turns "this
    // patient's iFOBT was never recorded" into a 500 on the sweep that reads every patient (CoreFlow.md
    // §11.2). Each nullable property below carries the reason it is one; everything else is provably
    // NOT NULL in dbo/Tables/PatientBasic.sql or is a SQL expression that cannot produce a NULL.
    public class AgentScreeningQueueItem
    {
        // PatientBasic.Patient_ID — VARCHAR(100) NOT NULL, the primary key.
        public string Patient_ID { get; set; } = string.Empty;

        // VARCHAR(100) NOT NULL, upper-cased by PatientController on save.
        public string Patient_Name { get; set; } = string.Empty;

        // VARCHAR(100) NOT NULL — so the value that produces ScreeningState = 'NO_PHONE' is an EMPTY
        // STRING, not a null: NOT NULL rejects a NULL and accepts '' (CoreFlow.md §3.8). Do not type this
        // nullable to guard a state the DDL forbids.
        public string Patient_Phone { get; set; } = string.Empty;

        // NULLABLE — BIT NULL on the table. NULL = never recorded, 1 = complete, 0 = not complete.
        public bool? Patient_iFOBTStatus { get; set; }

        // NULLABLE — DATE NULL on the table, and forced back to NULL by both write procedures whenever the
        // status is not 1, so "completed on this date" cannot outlive "completed".
        public DateTime? Patient_iFOBTCompletionDate { get; set; }

        // NULLABLE — BIT NULL on the table. 1 = positive, which is what opens a patient journey (§1).
        public bool? Patient_iFOBTResults { get; set; }

        // NOT nullable: RIGHT(LTRIM(RTRIM(Patient_NRIC)), 4) over a VARCHAR(100) NOT NULL column. RIGHT of
        // a non-null is non-null — a blank NRIC yields "", never a NULL. There is no path on which this
        // column is null, so a string? here would only invite a null-forgiving operator downstream.
        public string NricLast4 { get; set; } = string.Empty;

        // NOT nullable: a CASE with an ELSE branch, so every row gets one of six literals —
        // NO_PHONE, UNRECORDED, INCOMPLETE, POSITIVE, NEGATIVE, RESULT_PENDING. NO_PHONE is tested FIRST,
        // ahead of every clinical branch, because a patient with no number is one the agent can do nothing
        // with whatever their result says.
        //
        // RESULT_PENDING is the CASE's ELSE, and it is reachable in exactly one state: status = 1 with a
        // NULL result — the sample is done and the lab result has not been entered. (A NULL result matches
        // neither `= 1` nor `= 0`, because a comparison with NULL is UNKNOWN rather than false.) It carries
        // its OWN label, so that state is no longer indistinguishable from UNRECORDED, which means nothing
        // was recorded at all — one is "chase the lab", the other is "chase the patient", and the caller
        // branches on the difference (CoreFlow.md §13.1, finding 4).
        public string ScreeningState { get; set; } = string.Empty;

        // 🔴 NOT nullable, and this is the one place the "aggregate over a possibly empty set" rule in
        // CoreFlow.md §11.2 does NOT apply. COUNT(*) returns 0 over an empty set, not NULL — unlike SUM,
        // MAX or AVG, which is what that rule is about. A patient with no appointments comes back 0.
        //
        // It is the ONLY duplicate-booking guard that exists: dbo.PatientAppointment has nothing unique
        // except its primary key (CoreFlow.md §3.9), so two bookings for the same patient, clinician and
        // hour are a state no constraint and no procedure notices. The count is over FUTURE 'Scheduled'
        // appointments only, and the agent skips any patient whose count is greater than zero.
        public int OpenAppointmentCount { get; set; }

        // NOT nullable: CAST(CASE WHEN EXISTS (…) THEN 1 ELSE 0 END AS BIT) — an EXISTS test always
        // answers. True when a dbo.PatientJourney row with PjAppType_Name = 'PATIENT ASSESSMENT' exists;
        // the match is on the DENORMALIZED NAME because dbo.PatientJourney does not store the
        // LU_PJ_APP_TYPE code at all and there is no join that resolves the two (CoreFlow.md §3.10).
        public bool HasAssessment { get; set; }
    }
}
