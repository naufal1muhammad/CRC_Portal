namespace CRC.Data.Models
{
    // ONE OPEN HOUR of one clinician's published availability at one branch, from
    // spAgentSlots_FindOpenByBranch: eight columns, ordered SlotDate, SlotStartTime, Staff_Name.
    //
    // ONE ROW IS ONE HOUR. dbo.StaffSlots holds exactly one on-the-hour hour per row, by check constraint
    // (CoreFlow.md §3.7), so a three-hour gap comes back as THREE rows and it is the caller that groups
    // them into something a patient would recognise as "9am to noon".
    //
    // 🔴 THIS IS AN ADVISORY READ AND THE CALLER MUST TREAT IT THAT WAY. It runs outside any transaction
    // and holds no lock, so a slot on this list can be consumed by an administrator working in the portal
    // a second later — and nothing in the schema would catch the double-claim, because
    // dbo.PatientAppointment has no unique constraint beyond its identity and dbo.StaffSlots has nothing
    // unique on PatientAppointment_ID (CoreFlow.md §3.9). The only defence is the re-read INSIDE
    // SaveAppointmentAsync's transaction, which answers SlotTaken (CoreFlow.md §6.7). A SlotTaken answer
    // to a booking is a NORMAL OUTCOME of a correct system, not a bug in this read — the caller re-runs
    // slot discovery and tries again.
    //
    // 🔴 A ROW APPEARING HERE ALREADY MEANS "OPEN". The procedure filters PatientAppointment_ID IS NULL,
    // which IS availability — dbo.StaffSlots has no IsBooked column, no status and no "released" state
    // (CoreFlow.md §3.7). That is why, unlike StaffSlotItem, there is no PatientAppointment_ID property on
    // this model at all: it would be null on every row by construction and would read as information.
    //
    // 🔴 Staff_Phone IS FOR THE CLINICIAN-CONFIRMATION STEP, NOT FOR A PATIENT — the same note as on
    // AgentStaffItem, and the same enforcement point: the agent's system prompt, not this API.
    //
    // ── NULLABILITY ─────────────────────────────────────────────────────────────────────────────────────
    // Nothing here is nullable, and that is checked rather than assumed. StaffSlot_ID is the IDENTITY
    // primary key; Staff_ID and SlotDate are NOT NULL on dbo.StaffSlots; the three Staff columns come
    // through an INNER JOIN (correct here — a slot whose staff row no longer exists has no name, no phone
    // and nowhere to send a patient) onto columns that are NOT NULL on dbo.Staff; and the two time strings
    // are CONVERTs of TIME(0) NOT NULL columns.
    public class AgentOpenSlotItem
    {
        // StaffSlots.StaffSlot_ID — INT IDENTITY, one of the few numeric keys in nucentra. THIS IS THE
        // VALUE THE BOOKING WRITE TAKES BACK as its slotIds argument, so it is the one column on this
        // model the caller must round-trip unchanged. It is sequential and therefore guessable, which is a
        // security fact rather than a detail (CoreFlow.md §3.7, §4.5).
        public int StaffSlot_ID { get; set; }

        // StaffSlots.Staff_ID — VARCHAR(100) NOT NULL, a Staff.Staff_ID by convention only.
        public string Staff_ID { get; set; } = string.Empty;

        // Staff.Staff_Name — VARCHAR(100) NOT NULL, through an INNER JOIN.
        public string Staff_Name { get; set; } = string.Empty;

        // Staff.Staff_Phone — VARCHAR(100) NOT NULL. See the note above about who this is for.
        public string Staff_Phone { get; set; } = string.Empty;

        // Staff.Staff_Type — VARCHAR(100) NOT NULL, the LU_STAFFTYPE code. This is what @Staff_Type
        // filters on when the caller supplies it; NULL there means "do not filter", not "no type".
        public string Staff_Type { get; set; } = string.Empty;

        // DATE NOT NULL — date only, no time component. The hour is in the two strings below.
        public DateTime SlotDate { get; set; }

        // 🔴 THE TWO TIMES ARE STRINGS — "09:00", five characters — AND THAT IS DELIBERATE, NOT AN
        // OVERSIGHT. The columns are TIME(0); the procedure projects CONVERT(VARCHAR(5), …, 108), which
        // truncates the seconds, exactly as spStaffSlots_List already does, so both slot reads in nucentra
        // hand back the identical wire shape.
        //
        // THE REASON IS THE CONSUMER, and it is the opposite of the usual argument. This endpoint's caller
        // is a JSON API client (an n8n workflow), NOT a .NET consumer: a TIME maps onto a TimeSpan and
        // then onto JSON as "17:00:00" or as a duration object depending on the serializer — a shape the
        // caller then has to parse and re-format before it can send it back in a booking payload. A
        // five-character string is unambiguous, sorts correctly, and is already the format the write
        // expects. Parsing these into TimeSpan here would only mean formatting them back again.
        public string SlotStartTime { get; set; } = string.Empty;
        public string SlotEndTime { get; set; } = string.Empty;
    }
}
