namespace CRC.Data.Models
{
    // What SaveAppointmentAsync hands back. Unlike StaffSaveResult, this one is meaningful on BOTH
    // outcomes: a failure is an ordinary, expected answer carried in Reason, not an exception.
    //
    //   Reason == Ok       the transaction COMMITTED. The appointment row and the slot assignments are
    //                      durable, and the audit values below are worth reading.
    //   Reason != Ok       the transaction was ROLLED BACK and NOTHING was written. Every other property
    //                      is at its default and means nothing.
    //
    // A genuine failure — a dropped connection, a constraint violation — still throws, as everywhere else
    // in this layer. Reason is for the validation outcomes the flow is designed to produce, and it is the
    // controller that turns each into the sentence the user reads. See AppointmentSaveFailure.
    public class AppointmentSaveResult
    {
        public AppointmentSaveFailure Reason { get; set; } = AppointmentSaveFailure.Ok;

        // True when this was an INSERT. It decides which audit line the caller writes, and it cannot be
        // re-derived from the input once the id has been filled in below.
        public bool IsInsert { get; set; }

        // The saved appointment's id — the caller's own on an update, or the identity
        // spPatientAppointment_Insert generated inside the transaction on an insert.
        public int PatientAppointment_ID { get; set; }

        // The hours the appointment actually occupies, derived inside the transaction from the slots it
        // consumed: the earliest requested slot's start, and the latest one's end. The caller does not
        // supply these (see AppointmentSaveInput) and needs them back to audit an insert.
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // ── The persisted values, for the UPDATE path's audit line ───────────────────────────────────
        //
        // 🔴 THESE ARE RE-READ FROM THE ROW, NOT ECHOED FROM THE REQUEST, AND THAT IS DELIBERATE.
        // spPatientAppointment_Update ends by selecting the eight columns back into OUTPUT parameters,
        // with a comment saying why: "Re-read persisted values so callers can audit DB state, not request
        // payload". An audit line that reports what was asked for rather than what is now in the table is
        // a line that can be wrong in exactly the case somebody is reading it to investigate.
        //
        // They are SEEDED from the request (and from the derived times above) and overwritten only when
        // the procedure actually returns one. That fallback is live: the procedure fills its OUTPUT
        // parameters inside `IF @RowsAffected > 0`, so an update against an id that matches nothing leaves
        // all eight NULL — and the pre-migration code kept its request-derived locals in exactly that
        // case. Same behaviour, same reason.
        public string PersistedPatientId { get; set; } = string.Empty;
        public string PersistedStaffId { get; set; } = string.Empty;
        public DateTime PersistedDate { get; set; }
        public TimeSpan PersistedStartTime { get; set; }
        public TimeSpan PersistedEndTime { get; set; }
        public string PersistedTypeId { get; set; } = string.Empty;
        public string PersistedBranchId { get; set; } = string.Empty;
        public string PersistedStatus { get; set; } = string.Empty;
    }
}
