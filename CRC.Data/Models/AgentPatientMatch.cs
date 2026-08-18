namespace CRC.Data.Models
{
    // One patient matched to an inbound WhatsApp number, from spAgentPatient_FindByPhone: seven columns,
    // ordered Patient_ID DESC.
    //
    // 🔴 ZERO, ONE AND MANY ARE ALL NORMAL ANSWERS, WHICH IS WHY THIS IS A LIST AND NOT A SINGLE ROW.
    // Nothing on dbo.PatientBasic is unique except the primary key (CoreFlow.md §3.8) — not the NRIC, not
    // the e-mail, not the phone — so a shared household number resolving to two patients is a state the
    // schema permits. On top of that the match is on the LAST NINE DIGITS rather than on equality (the two
    // sides never agree on their prefix: Meta sends 60123456789, the portal stores whatever an
    // administrator typed), and nine digits is not unique either: 012-345 6789 and 011-2345 6789 are two
    // different people with the same tail. THE CALLER MUST ASK A DISAMBIGUATING QUESTION ON MORE THAN ONE
    // ROW AND MUST NEVER TAKE THE FIRST.
    //
    // A @Phone with fewer than nine digits is answered by an early SELECT TOP 0 that emits THE SAME SEVEN
    // COLUMNS IN THE SAME ORDER WITH THE SAME TYPES. That matters to this model: Dapper maps by name and
    // the caller reads the grid unconditionally, so a differently-shaped grid on one path would be a
    // mapping failure that compiles, returns 200 and logs nothing. Zero rows means nothing is mapped, so
    // the CAST(NULL AS …) types on that path never reach a property.
    //
    // 🔴 THE FULL NRIC IS NOT HERE AND MUST NOT BE ADDED — the procedure projects the last four digits
    // only, for the reason given on AgentScreeningQueueItem.
    //
    // This is a strict subset of AgentScreeningQueueItem's columns and deliberately NOT the same model:
    // sharing one would leave ScreeningState, OpenAppointmentCount and HasAssessment silently at their
    // defaults on every row of this result (CoreFlow.md §11.2 — reuse the shape, never the name).
    public class AgentPatientMatch
    {
        // VARCHAR(100) NOT NULL, the primary key.
        public string Patient_ID { get; set; } = string.Empty;

        // VARCHAR(100) NOT NULL.
        public string Patient_Name { get; set; } = string.Empty;

        // VARCHAR(100) NOT NULL — the number AS STORED, unnormalised. It is what an administrator typed
        // ("012-345 6789", "+60 12-345 6789"), not the digits the match was made on.
        public string Patient_Phone { get; set; } = string.Empty;

        // NOT nullable: RIGHT(LTRIM(RTRIM(Patient_NRIC)), 4) over a VARCHAR(100) NOT NULL column, so it is
        // "" at worst and never NULL.
        public string NricLast4 { get; set; } = string.Empty;

        // NULLABLE — BIT NULL on the table. NULL = never recorded.
        public bool? Patient_iFOBTStatus { get; set; }

        // NULLABLE — BIT NULL on the table. 1 = positive.
        public bool? Patient_iFOBTResults { get; set; }

        // 🔴 NULLABLE, AND THE NULL IS THE MEANING. VARCHAR(100) NULL on the table, and A NULL
        // DischargeType_ID IS THE DEFINITION OF AN ACTIVE PATIENT (CoreFlow.md §3.8). There is no
        // IsActive column, no status and no soft-delete marker to disagree with it. This procedure does
        // NOT filter on it — unlike spAgentPatient_ListScreeningQueue, which returns active patients only
        // — because an inbound message from a discharged patient still has to be answered; the caller
        // decides what to say to them.
        public string? DischargeType_ID { get; set; }
    }
}
