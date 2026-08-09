namespace CRC.Data.Models
{
    // Everything SaveAppointmentAsync needs, in one object rather than eight positional parameters where
    // four adjacent ones are strings. Same reasoning as StaffSaveInput: a caller cannot transpose two
    // named properties by accident, and it can very easily transpose Patient_ID with Staff_ID.
    //
    // 🔴 NOTE WHAT IS ABSENT: THE START AND END TIMES. dbo.PatientAppointment stores them, but the caller
    // does not supply them — they are DERIVED inside the transaction from the slots being consumed, as
    // the first of the requested hours' start and the last of their ends. That is not a convenience: the
    // slots are read under the transaction's lock, so their times are not known until the method that
    // owns that transaction has read them. A caller that computed the times itself would be computing
    // them from a read taken outside the lock, which is precisely the race the transaction exists to
    // prevent (CoreFlow.md §6.6).
    //
    // Property names mirror the procedures' parameters without the `@` wherever they map straight through,
    // because SqlData hands them to Dapper as an anonymous object's field values — read
    // spPatientAppointment_Insert and spPatientAppointment_Update side by side with this file.
    public class AppointmentSaveInput
    {
        // 0 (or less) means INSERT; anything else is the PatientAppointment_ID being UPDATED. The client
        // decides which by whether it sends an id, and nothing verifies that an id it does send exists —
        // spPatientAppointment_Update is silent when it matches nothing (CoreFlow.md §5.7).
        //
        // It is also the id compared against each slot's PatientAppointment_ID for the SlotTaken check, so
        // on an edit the appointment's own hours are correctly not treated as somebody else's booking.
        public int PatientAppointment_ID { get; set; }

        public string Patient_ID { get; set; } = string.Empty;

        // The DATE half of the appointment, at midnight. It is both the value written to the row and the
        // @FromDate/@ToDate of the in-transaction slot read, so every slot considered is on this day.
        public DateTime PatientAppointment_Date { get; set; }

        public string Staff_ID { get; set; } = string.Empty;

        // The StaffSlot_IDs to consume, already de-duplicated and stripped of non-positive values by the
        // caller. Their COUNT is load-bearing: the slot-existence check is a comparison against how many
        // of them the in-transaction read gave back.
        public IReadOnlyList<int> SlotIds { get; set; } = Array.Empty<int>();

        public string PjAppType_ID { get; set; } = string.Empty;
        public string Branch_ID { get; set; } = string.Empty;

        // "Scheduled" / "Attended" / "Not Attended". A VARCHAR(100) with no check constraint and no lookup
        // table behind it — the three values are a convention held in the controllers and in the .js, and
        // the controller validates them before calling. See CoreFlow.md §3.9.
        public string PatientAppointment_Status { get; set; } = string.Empty;
    }
}
