namespace CRC.Data.Models
{
    // One row of the DISCHARGED patient list, as spPatientBasic_ListDischarged returns it — the five
    // columns of PatientListItem plus Patient_DischargeDate, ordered by
    // `Patient_DischargeDate DESC, Patient_ID DESC` (most recently discharged first; the caller must not
    // re-sort, and the tiebreak matters because a DATETIME column holding midnights ties constantly).
    //
    // "Discharged" is `DischargeType_ID IS NOT NULL`, the exact complement of spPatientBasic_ListActive's
    // filter, so the two lists partition dbo.PatientBasic with no row in both and no row in neither.
    //
    // See PatientListItem for why this is a second model rather than a shared one.
    //
    // 🔴 Patient_DischargeDate IS NULLABLE HERE EVEN THOUGH THE FILTER IMPLIES IT IS SET. The procedure
    // selects rows on DischargeType_ID, not on the date, and the schema constrains neither: nothing stops
    // a DischargeType_ID with a NULL Patient_DischargeDate (SaveBasic always writes the pair together, but
    // a hand-written UPDATE need not). Dapper THROWS mapping a NULL onto a non-nullable DateTime, so a row
    // the schema permits would become a 500 on the discharged-patients page rather than a blank cell.
    public class PatientDischargedItem
    {
        public string Patient_ID { get; set; } = string.Empty;
        public string Patient_Name { get; set; } = string.Empty;
        public DateTime? Patient_DischargeDate { get; set; }

        public bool? Patient_iFOBTStatus { get; set; }
        public DateTime? Patient_iFOBTCompletionDate { get; set; }
        public bool? Patient_iFOBTResults { get; set; }
    }
}
