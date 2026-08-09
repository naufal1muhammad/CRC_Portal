namespace CRC.Data.Models
{
    // One row of the Appointment tab's table on /Patient/Edit, from spPatientAppointment_ListByPatient:
    // twelve columns, ordered by date DESC, then start time DESC, then id DESC. The ordering is part of
    // the procedure's contract — newest appointment first — and the caller must not re-sort.
    //
    // THE TWO TIME COLUMNS ARE STRINGS, NOT TimeSpan, AND THAT IS THE PROCEDURE'S DOING. The table stores
    // TIME(0), but the procedure projects `CONVERT(VARCHAR(5), …, 108)` on both — so what comes back is
    // "08:00", five characters. /Patient/GetAppointments serializes them verbatim as `from` and `to`, so
    // parsing them into a TimeSpan here would only mean formatting them back again. Exactly the same
    // decision, for exactly the same reason, as StaffSlotItem's two time columns.
    //
    // THE THREE _Name COLUMNS COME FROM LEFT JOINs onto dbo.Staff, dbo.LU_PJ_APP_TYPE and dbo.Branch,
    // none of which is a foreign key (CoreFlow.md §3.4, §3.2), so all three are genuinely nullable: an
    // appointment holding a Staff_ID, a PjAppType_ID or a Branch_ID that no longer resolves is a state
    // the schema permits. The endpoint coerces each to "" — a null renders as the word "null" in the
    // table, which is what the DataTable code this replaces avoided by accident (DBNull.ToString() is "").
    public class PatientAppointmentItem
    {
        public int PatientAppointment_ID { get; set; }
        public string Patient_ID { get; set; } = string.Empty;

        // DATE NOT NULL on the table, so this cannot actually be null — but it is typed nullable because
        // the DataTable code it replaces tested `== DBNull.Value` and rendered "" when it hit one, and
        // Dapper THROWS mapping a NULL onto a non-nullable DateTime. A defensive "" must not become a 500.
        public DateTime? PatientAppointment_Date { get; set; }

        // "08:00" / "10:00" — VARCHAR(5), 24-hour, from CONVERT(…, 108). Not a TimeSpan; see above.
        public string PatientAppointment_StartTime { get; set; } = string.Empty;
        public string PatientAppointment_EndTime { get; set; } = string.Empty;

        public string PatientAppointment_Status { get; set; } = string.Empty;

        public string Staff_ID { get; set; } = string.Empty;
        public string? Staff_Name { get; set; }

        public string PjAppType_ID { get; set; } = string.Empty;
        public string? PjAppType_Name { get; set; }

        public string Branch_ID { get; set; } = string.Empty;
        public string? Branch_Name { get; set; }
    }
}
