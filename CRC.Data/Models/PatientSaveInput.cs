namespace CRC.Data.Models
{
    // Everything spPatientBasic_Insert or spPatientBasic_Update needs, in one object.
    //
    // 🔴 THIS MIRRORS THE TWO PROCEDURES' PARAMETER LISTS ONE-FOR-ONE, WITHOUT THE `@`, and it is meant to
    // be diffed by eye against spPatientBasic_Insert.sql and spPatientBasic_Update.sql side by side. The
    // union is exact:
    //
    //     spPatientBasic_Insert  =  every property below EXCEPT Patient_ID and the three discharge ones
    //                               (a new patient is never discharged — the procedure hard-codes NULL,
    //                                NULL, NULL into those three columns and takes no parameter for them)
    //     spPatientBasic_Update  =  every property below, all 27
    //
    // plus, on both, `@User_ID INT = NULL` — the audit ACTOR, which is NOT here and never will be. SqlData
    // supplies it from DatabaseHelper.CurrentUserId; it is not a business argument (CoreFlow.md §0.1).
    // spPatientBasic_Insert also has `@NewPatient_ID VARCHAR(100) OUTPUT`, which is an answer rather than
    // an argument — see CreatePatientAsync.
    //
    // WHY A MODEL AND NOT A 27-ARGUMENT METHOD: the alternative is a signature in which
    // (email, phone, nric) and (resState, resCity, resPostcode) and (emergencyName, emergencyRelationship,
    // emergencyNumber) are nine adjacent `string`s. A caller cannot transpose two named properties by
    // accident and can very easily transpose two positional arguments — and every one of those transposes
    // compiles, runs, and writes a patient's phone number into their NRIC column.
    //
    // ── 🔴 EVERY VALUE HERE IS ALREADY DERIVED AND ALREADY VALIDATED. THIS TYPE DECIDES NOTHING ─────────
    //
    // PatientController owns all of it and keeps owning it: the mandatory-field check, the "NRIC must be
    // exactly 12 digits" rule, the NRIC → Patient_BirthDate and NRIC → Patient_Gender derivations, the age
    // arithmetic, the ToUpperInvariant() on the two name fields, the "if the iFOBT is not complete, clear
    // the completion fields" rule, the discharge-document check and every user-facing message. The data
    // layer's job is to send these values, not to second-guess them. If a rule ever moves in here, the
    // controller and the procedure become two places that both half-know it.
    //
    // ── NULL VERSUS EMPTY STRING, WHICH IS LOAD-BEARING ─────────────────────────────────────────────────
    //
    // The nullable properties below are nullable because the CONTROLLER SENDS NULL for them today, and a
    // C# null on a Dapper parameter object is a SQL NULL. The DataTable code this replaced wrote
    // `(object?)x ?? DBNull.Value` at each of those points; that dance disappears, the value does not.
    // Verified end to end by creating a patient with only the mandatory fields filled and diffing the row
    // against one created the old way: Patient_AddLine2, all three iFOBT columns and all three discharge
    // columns come back NULL, not "". NOTHING in this flow is deliberately sent as the empty string — the
    // one optional free-text field, Patient_AddLine2, is sent as null and both procedures NULLIF a blank
    // one anyway, so the column cannot hold "" by either route.
    public class PatientSaveInput
    {
        // IGNORED BY CreatePatientAsync: spPatientBasic_Insert generates the id itself as
        // 'PAT-' + a 6-digit sequence and hands it back through its OUTPUT parameter (CoreFlow.md §3.8).
        // Required, and validated by the caller, on the update path.
        public string Patient_ID { get; set; } = string.Empty;

        // Upper-cased by the controller before it gets here, as the old code did.
        public string Patient_Name { get; set; } = string.Empty;
        public string Patient_Email { get; set; } = string.Empty;
        public string Patient_Phone { get; set; } = string.Empty;

        // Twelve digits, already stripped of dashes and already length-checked.
        public string Patient_NRIC { get; set; } = string.Empty;

        // 🔴 DERIVED FROM THE NRIC, NEVER FROM THE FORM. The Edit page shows a birth date, an age and a
        // gender, and all three are read-only mirrors of what the browser computed for display; the server
        // recomputes them from Patient_NRIC on every save and sends its own answer. A hostile client can
        // therefore not store a birth date that disagrees with the identity number beside it.
        public DateTime Patient_BirthDate { get; set; }
        public int Patient_Age { get; set; }

        public string Race_ID { get; set; } = string.Empty;
        public string Source_ID { get; set; } = string.Empty;

        // "MALE" when the NRIC's last digit is odd, "FEMALE" when it is even. Derived, like the two above.
        public string Patient_Gender { get; set; } = string.Empty;

        public string Religion_ID { get; set; } = string.Empty;
        public string MaritalStatus_ID { get; set; } = string.Empty;
        public string Occupation_ID { get; set; } = string.Empty;

        // Stored as NAMES, not LU_LOCATION ids — see PatientBasicDetail.
        public string Patient_ResState { get; set; } = string.Empty;
        public string Patient_ResCity { get; set; } = string.Empty;
        public string Patient_ResPostcode { get; set; } = string.Empty;
        public string Patient_AddLine1 { get; set; } = string.Empty;

        // Optional. Null, not "" — see the note above.
        public string? Patient_AddLine2 { get; set; }

        // Upper-cased by the controller, like Patient_Name.
        public string Patient_EmergencyName { get; set; } = string.Empty;
        public string Patient_EmergencyRelationship { get; set; } = string.Empty;
        public string Patient_EmergencyNumber { get; set; } = string.Empty;

        // All three optional and all three sent as null when unset. The controller ALSO clears the two
        // completion fields itself whenever the status is not true, and both procedures clear them again in
        // SQL (`CASE WHEN @Patient_iFOBTStatus = 1 THEN … ELSE NULL END`). The belt and the braces are both
        // pre-existing; neither is redundant enough to remove in a migration that must not change
        // behaviour.
        public bool? Patient_iFOBTStatus { get; set; }
        public DateTime? Patient_iFOBTCompletionDate { get; set; }
        public bool? Patient_iFOBTResults { get; set; }

        // 🔴 THE UPDATE PATH ONLY — spPatientBasic_Insert takes no discharge parameters at all.
        //
        // All three are null for an active patient, and the update writes all three unconditionally, so
        // saving a discharged patient with the discharge cleared genuinely un-discharges them. That is the
        // portal's only route back to "active", and it is a side effect of the update being a full-row
        // overwrite rather than a deliberate feature.
        public string? DischargeType_ID { get; set; }
        public DateTime? Patient_DischargeDate { get; set; }
        public string? Patient_DischargeRemarks { get; set; }
    }
}
