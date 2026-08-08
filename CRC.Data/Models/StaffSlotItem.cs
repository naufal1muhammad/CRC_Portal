namespace CRC.Data.Models
{
    // One published hour of a staff member's availability — one dbo.StaffSlots row as
    // spStaffSlots_List returns it.
    //
    // 🔴 ONE MODEL, TWO CONSUMERS, AND THE SECOND ONE IS A CONCURRENCY CHECK.
    //
    //   1. StaffScheduleController.List renders the schedule grid on the Staff Edit page.
    //   2. PatientController.SaveAppointment (Prompt 6) reads the SAME procedure INSIDE the appointment
    //      transaction to decide whether the hours being booked are still free.
    //
    // They need exactly the same five columns, so this is deliberately one model and not two — the
    // availability check is not a different question from the listing, it is the same rows read under a
    // lock. If a later change ever gives one of them a column the other does not need, split the model
    // rather than widening this one silently.
    //
    // 🔴 PatientAppointment_ID BEING NULL IS WHAT "AVAILABLE" MEANS. There is no IsBooked column and no
    // status: a slot with a null appointment id is open, and a slot with one is taken by that appointment.
    // It is the only genuinely nullable column here, and it is nullable for that reason alone.
    //
    // THE TIME COLUMNS ARE STRINGS, NOT TimeSpan, AND THAT IS THE PROCEDURE'S DOING. dbo.StaffSlots stores
    // TIME(0), but spStaffSlots_List projects `CONVERT(VARCHAR(5), SlotStartTime, 108)` — so what comes back
    // is "09:00", five characters. They stay strings all the way to the caller: the schedule endpoint
    // serializes them verbatim, and SaveAppointment parses them with TimeSpan.Parse at its own call site.
    // Parsing them here instead would move where a malformed value throws, which is a behaviour change
    // wearing a tidy-up's clothes.
    //
    // Everything except the appointment id is typed NON-NULLABLE, unlike BranchDetail's defensive bool?.
    // The difference is that these three are provably NOT NULL — StaffSlot_ID is the IDENTITY primary key,
    // SlotDate is DATE NOT NULL, and the two time strings are CONVERTs of TIME(0) NOT NULL columns — and
    // consumer 2 above compares the date and keys a set on the id inside a transaction. A `DateTime?` there
    // would put a null-forgiving `!` in the middle of the check that stops two administrators booking the
    // same hour, to guard a state the table's DDL forbids.
    public class StaffSlotItem
    {
        public int StaffSlot_ID { get; set; }
        public DateTime SlotDate { get; set; }

        // "09:00" / "10:00" — VARCHAR(5), 24-hour, from CONVERT(…, 108). Not a TimeSpan; see above.
        public string SlotStartTime { get; set; } = string.Empty;
        public string SlotEndTime { get; set; } = string.Empty;

        // Null = this hour is open. Non-null = it is consumed by that dbo.PatientAppointment.
        public int? PatientAppointment_ID { get; set; }
    }
}
