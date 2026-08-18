namespace CRC.Data.Models
{
    // One clinician based at a branch, from spAgentStaff_ListByBranch: six columns for
    // Staff_Base = @Branch_ID, ordered by Staff_Name.
    //
    // NOT the portal's own staff list. spStaff_List returns a much wider shape for the Admin > Staff table;
    // this one returns the six things the agent needs to name a doctor and reach them, which is why it is
    // its own procedure and its own model rather than StaffListItem with most properties left at their
    // defaults (CoreFlow.md §11.2 — a model should describe the result set it maps).
    //
    // 🔴 Staff_Phone IS FOR THE CLINICIAN-CONFIRMATION STEP, NOT FOR A PATIENT. The agent needs it to ask a
    // clinician to confirm an hour before anything is booked. Nothing in this model, this procedure or the
    // API stops it reaching a patient — the agent's own system prompt is the enforcement point. Anyone
    // reusing this shape for a patient-facing surface must strip it themselves.
    //
    // ── NULLABILITY ─────────────────────────────────────────────────────────────────────────────────────
    // Five of the six columns are VARCHAR(100) NOT NULL on dbo.Staff (verified in dbo/Tables/Staff.sql, not
    // assumed). Exactly one is nullable, and it is nullable because of the JOIN rather than the column.
    public class AgentStaffItem
    {
        // Staff.Staff_ID — VARCHAR(100) NOT NULL, the primary key. Composed, and the prefix is the staff
        // type: "END-00003" says "endoscopist" at a glance (CoreFlow.md §3.4).
        public string Staff_ID { get; set; } = string.Empty;

        // VARCHAR(100) NOT NULL.
        public string Staff_Name { get; set; } = string.Empty;

        // VARCHAR(100) NOT NULL. See the note above about who this is for.
        public string Staff_Phone { get; set; } = string.Empty;

        // VARCHAR(100) NOT NULL — holds an LU_STAFFTYPE.StaffType_ID ('END', 'NUR', 'ANE', 'ENT', 'GAS')
        // BY CONVENTION ONLY. It is not a foreign key; see the next property.
        public string Staff_Type { get; set; } = string.Empty;

        // 🔴 NULLABLE BECAUSE THE JOIN IS A LEFT JOIN, not because LU_STAFFTYPE.StaffType_Name is nullable
        // — it is VARCHAR(100) NOT NULL. Staff.Staff_Type IS NOT A FOREIGN KEY (PK_Staff is the table's
        // only constraint — CoreFlow.md §3.4), so a staff member holding a code that has since been
        // removed from dbo.LU_STAFFTYPE is a state the schema permits and the join simply yields NULL.
        // An INNER JOIN would have DROPPED that person from the branch's list silently, and the agent
        // would be told a clinician standing in the branch does not work there. Same reason
        // StaffListItem and StaffDetail already type theirs nullable.
        public string? StaffType_Name { get; set; }

        // VARCHAR(100) NOT NULL — holds a Branch.Branch_ID, also by convention only. Echoed back so the
        // caller can key its own structures without re-sending the argument it asked with.
        public string Staff_Base { get; set; } = string.Empty;
    }
}
