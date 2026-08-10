namespace CRC.Data.Models
{
    // One cell of the Patient Tracker's grid, from spPatientTracker_Appointments_List: the state of one
    // patient's booking for one of the four journey types.
    //
    // 🔴 IT IS ONE ROW PER (Patient_ID, PjAppType_ID), NOT ONE PER APPOINTMENT. The procedure ranks each
    // patient's appointments of each type by date, start time and id descending and keeps only the newest,
    // so a patient rebooked three times for a COLONOSCOPY contributes exactly one row — the latest. The
    // tracker draws a grid of patients against types and needs a single current state per cell; the
    // history behind it is not on this page.
    //
    // The result set is UNORDERED — the procedure's ranking is inside the CTE and there is no outer
    // ORDER BY — because the caller indexes it by the two ids rather than reading it in sequence. Do not
    // add a dependency on the order it happens to arrive in.
    public class PatientTrackerAppointmentItem
    {
        public string Patient_ID { get; set; } = string.Empty;
        public string PjAppType_ID { get; set; } = string.Empty;
        public string PatientAppointment_Status { get; set; } = string.Empty;

        // DATE NOT NULL on the table; nullable because the endpoint renders "" for a missing date rather
        // than failing, and Dapper throws mapping a NULL onto a non-nullable DateTime.
        public DateTime? PatientAppointment_Date { get; set; }
    }
}
