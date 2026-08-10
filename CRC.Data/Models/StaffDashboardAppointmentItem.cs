namespace CRC.Data.Models
{
    // One row of a STAFF user's own dashboard, from any of the three spStaffDashboard_* procedures.
    //
    // ONE MODEL FOR THREE PROCEDURES IS CORRECT HERE, and it is not the usual "close enough": the three
    // select the SAME nine columns from the SAME joins in the SAME order, and differ only in their date
    // predicate — `= @ForDate`, `>= @FromDate AND < @FromDate + 7 days`, and the calendar month built from
    // `@Year`/`@Month`. That is the StaffDocumentItem case (reuse the shape), not the PatientListItem case
    // (a strict subset, which must stay two types).
    //
    // 🔴 EVERY ONE OF THE THREE FILTERS ON `pa.Staff_ID = @Staff_ID`, AND THAT PREDICATE IS THE PORTAL'S
    // ONLY SCOPING OF THIS PAGE. The staff id comes from the caller's own `StaffId` claim, resolved in
    // StaffDashboardController before the call; nothing in this layer knows who is logged in. A method here
    // that defaulted, guessed or widened that parameter would hand one clinician another's diary. See §4.13.
    //
    // The three _Name columns come from LEFT JOINs onto dbo.PatientBasic, dbo.LU_PJ_APP_TYPE and dbo.Branch,
    // none of which is a foreign key, so all three are genuinely nullable — the same reasoning as
    // PatientAppointmentItem. The endpoint coerces each to "".
    public class StaffDashboardAppointmentItem
    {
        public int PatientAppointment_ID { get; set; }
        public string Patient_ID { get; set; } = string.Empty;
        public string? Patient_Name { get; set; }
        public string? PjAppType_Name { get; set; }
        public string PatientAppointment_Status { get; set; } = string.Empty;
        public string? Branch_Name { get; set; }

        // DATE NOT NULL on the table, so this cannot be null in practice — nullable because the DataTable
        // code this replaces tested for DBNull and rendered "" rather than throwing, and Dapper throws
        // mapping a NULL onto a non-nullable DateTime. A defensive "" must not become a 500.
        public DateTime? PatientAppointment_Date { get; set; }

        // 🔴 TimeSpan, NOT string — the opposite call to PatientAppointmentItem, and the procedures are the
        // reason. These three select `PatientAppointment_StartTime` / `_EndTime` RAW, so a TIME(0) column
        // arrives as a TimeSpan; spPatientAppointment_ListByPatient CONVERTs its two to VARCHAR(5) first.
        // Same table, same columns, two different result types, and the models say which is which.
        // The endpoint formats these with "hh\:mm" and also sorts on them, so parsing is not avoidable.
        public TimeSpan? PatientAppointment_StartTime { get; set; }
        public TimeSpan? PatientAppointment_EndTime { get; set; }
    }
}
