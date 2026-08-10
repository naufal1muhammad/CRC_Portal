namespace CRC.Data.Models
{
    // One completed step of a patient's journey as the Patient Tracker shows it, from
    // spPatientTracker_Procedures_List — a bare read of dbo.PatientJourney, every row, ordered by patient
    // then journey date then id.
    //
    // THE DISTINCTION THIS PAIR OF LISTS DRAWS IS THE POINT OF THE PAGE: PatientTrackerAppointmentItem is
    // what was BOOKED (dbo.PatientAppointment, one row per patient per type, latest only);
    // PatientTrackerProcedureItem is what was DONE (dbo.PatientJourney, every row, all of them). A patient
    // can have one without the other in either direction — nothing in the schema ties a journey row to the
    // appointment that produced it (§3.10) — and the tracker renders both columns precisely so that the
    // gap is visible.
    //
    // 🔴 THE TYPE ARRIVES AS A NAME, NOT AN ID. dbo.PatientJourney stores `PjAppType_Name` denormalized and
    // holds no `PjAppType_ID` at all, so this list joins to the appointment list by the type's NAME while
    // that one carries the type's ID. Renaming a row in LU_PJ_APP_TYPE silently disconnects the two.
    public class PatientTrackerProcedureItem
    {
        public string Patient_ID { get; set; } = string.Empty;
        public string PjAppType_Name { get; set; } = string.Empty;

        // DATETIME NOT NULL on the table; nullable because the endpoint renders "" rather than failing.
        public DateTime? PatientJourney_Date { get; set; }
    }
}
