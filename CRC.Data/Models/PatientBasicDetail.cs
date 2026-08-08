namespace CRC.Data.Models
{
    // A whole dbo.PatientBasic row plus the six joined lookup names, from spPatientBasic_GetById
    // (a plain SELECT on the primary key — at most one row, possibly none). This is what the Patient Edit
    // form loads, and — from Prompt 7 — what StaffPatientController.GetBasic loads for the clinical pages.
    //
    // Property names mirror the column names because Dapper maps by name and no .sql may be aliased; a
    // property whose name does not match its column stays at its default on every row, silently.
    //
    // ── THE SIX _Name COLUMNS ARE LEFT JOINs AND EVERY ONE OF THEM IS GENUINELY NULLABLE ───────────────
    //
    // None of Race_ID, Source_ID, Religion_ID, MaritalStatus_ID, Occupation_ID or DischargeType_ID is a
    // foreign key (CoreFlow.md §3.1 — nucentra has no FKs on lookup codes at all), so a patient can hold a
    // code that no longer exists in its LU_* table and the join simply yields NULL. Only DischargeType_Name
    // reaches the browser today; the other five are modelled because the procedure selects them, and a
    // model that drops columns stops being a description of the result set.
    //
    // ── THE TWO DEFENSIVE NULLABILITIES, both mirroring a coercion the endpoint has always performed ────
    //
    //   • Patient_BirthDate is DateTime? although the column is DATETIME NOT NULL. The DataTable code this
    //     replaced ran it through a helper that returned null for DBNull rather than throwing, and Dapper
    //     THROWS mapping a NULL onto a non-nullable DateTime — so a non-nullable property here would turn
    //     a harmless empty date input into a 500.
    //   • Patient_Age is int? for the same reason: the endpoint coerces a null to 0 and must keep doing so.
    //
    // Everything else typed non-nullable is NOT NULL in the table (CoreFlow.md §3.8). The genuinely
    // nullable business columns are Patient_AddLine2, the three iFOBT columns and the three discharge
    // columns — and the discharge trio is the one that carries meaning by being null: A NULL
    // DischargeType_ID IS THE DEFINITION OF AN ACTIVE PATIENT.
    public class PatientBasicDetail
    {
        public string Patient_ID { get; set; } = string.Empty;
        public string Patient_Name { get; set; } = string.Empty;
        public string Patient_Email { get; set; } = string.Empty;
        public string Patient_Phone { get; set; } = string.Empty;

        // Twelve digits, no dashes — the controller strips every non-digit before saving (CoreFlow.md §3.8).
        public string Patient_NRIC { get; set; } = string.Empty;

        // Both DERIVED from the NRIC server-side on every save and never trusted from the client; see
        // PatientSaveInput. Nullable here for the defensive reason above, not because the columns are.
        public DateTime? Patient_BirthDate { get; set; }
        public int? Patient_Age { get; set; }

        public string Race_ID { get; set; } = string.Empty;
        public string? Race_Name { get; set; }
        public string Source_ID { get; set; } = string.Empty;
        public string? Source_Name { get; set; }

        // "MALE" / "FEMALE", derived from the NRIC's last digit. Free text in the schema — no lookup table
        // and no check constraint.
        public string Patient_Gender { get; set; } = string.Empty;

        public string Religion_ID { get; set; } = string.Empty;
        public string? Religion_Name { get; set; }
        public string MaritalStatus_ID { get; set; } = string.Empty;
        public string? MaritalStatus_Name { get; set; }
        public string Occupation_ID { get; set; } = string.Empty;
        public string? Occupation_Name { get; set; }

        // The residential address is stored by NAME, not by LU_LOCATION id — the same denormalization
        // dbo.Staff uses (CoreFlow.md §3.4). The form drives its cascading dropdowns with integer ids and
        // persists the labels, so nothing detects a city that is not in the state beside it.
        public string Patient_ResState { get; set; } = string.Empty;
        public string Patient_ResCity { get; set; } = string.Empty;
        public string Patient_ResPostcode { get; set; } = string.Empty;
        public string Patient_AddLine1 { get; set; } = string.Empty;

        // The ONLY optional free-text field on the form. Both write procedures NULLIF a blank one, so it is
        // never the empty string in the table — it is NULL or it has content.
        public string? Patient_AddLine2 { get; set; }

        public string Patient_EmergencyName { get; set; } = string.Empty;
        public string Patient_EmergencyRelationship { get; set; } = string.Empty;
        public string Patient_EmergencyNumber { get; set; } = string.Empty;

        // The immunochemical faecal occult blood test the whole programme turns on (CoreFlow.md §1).
        // NULL status = not recorded; the two completion columns are forced to NULL by the procedures
        // whenever the status is not 1, so "completed on this date" cannot outlive "completed".
        public bool? Patient_iFOBTStatus { get; set; }
        public DateTime? Patient_iFOBTCompletionDate { get; set; }
        public bool? Patient_iFOBTResults { get; set; }

        // 🔴 NULL DischargeType_ID = ACTIVE. This is the flag the two list procedures partition on, and
        // there is no IsActive column anywhere to disagree with it.
        public string? DischargeType_ID { get; set; }
        public string? DischargeType_Name { get; set; }
        public DateTime? Patient_DischargeDate { get; set; }
        public string? Patient_DischargeRemarks { get; set; }
    }
}
