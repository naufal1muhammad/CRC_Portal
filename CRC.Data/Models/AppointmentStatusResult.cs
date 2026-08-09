namespace CRC.Data.Models
{
    // The appointment as it stands AFTER spPatientAppointment_UpdateStatus has changed its status — the
    // eight columns the procedure re-reads into OUTPUT parameters once the UPDATE has landed.
    //
    // WHY A STATUS CHANGE RETURNS THE WHOLE ROW. The procedure's own comment says it: "Re-read persisted
    // values so callers can audit DB state, not request payload". AppointmentController turns these into
    // an AuditLog.AppointmentUpdated line, and a security line describing the request rather than the row
    // is wrong precisely when somebody is reading it to find out what actually happened.
    //
    // 🔴 EVERY PROPERTY IS NON-NULLABLE AND THAT IS SAFE HERE, unlike the two nullable dates on
    // PatientAppointmentItem and AppointmentSearchItem. The procedure RAISERRORs 'Appointment not found.'
    // and RETURNs when @@ROWCOUNT is 0, so the only way to reach the SELECT that fills these is for the
    // row to exist — there is no "updated nothing" path that leaves them NULL. Contrast
    // spPatientAppointment_Update, which is silent on a bad id and therefore does leave its OUTPUTs NULL;
    // that is why AppointmentSaveResult seeds its persisted values from the request and this does not.
    //
    // ONE METHOD, TWO CALLERS, AND ONLY ONE OF THEM READS THIS. /Appointment/UpdateAppointmentStatus
    // audits from it; /AdminDashboard/UpdateAppointmentStatus writes no AuditLog line at all and discards
    // the result. Requesting the OUTPUT parameters costs the second caller nothing — they all declare
    // `= NULL` defaults and the procedure fills them either way — so the two share one method rather than
    // growing a second one that differs only in what it throws away.
    public class AppointmentStatusResult
    {
        public string Patient_ID { get; set; } = string.Empty;
        public string Staff_ID { get; set; } = string.Empty;
        public DateTime PatientAppointment_Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string PjAppType_ID { get; set; } = string.Empty;
        public string Branch_ID { get; set; } = string.Empty;
        public string PatientAppointment_Status { get; set; } = string.Empty;
    }
}
