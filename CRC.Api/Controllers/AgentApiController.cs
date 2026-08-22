using System.Globalization;
using CRC.Api.Infrastructure;
using CRC.Data.Data;
using CRC.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace CRC.Api.Controllers
{
    // =================================================================================================
    // THE AGENT API — nucentra's one machine-callable surface. Eight endpoints under /api/agent for a
    // single external caller (an n8n WhatsApp workflow): SEVEN READS AND, at the bottom of this file,
    // THE ONE WRITE. CoreFlow.md §13.
    //
    // Four attributes on this class do things that no other controller in nucentra does, and every one of
    // them is a deliberate hole in a global default. Read all four before adding an action.
    //
    // 🔴 1. [AllowAnonymous] DISABLES THE GLOBAL AuthorizeFilter FOR THIS CONTROLLER.
    //    Program.cs installs `options.Filters.Add(new AuthorizeFilter())`, so every action in the portal
    //    requires a signed-in user unless it says otherwise. CoreFlow.md §2.2 states that a grep for
    //    AllowAnonymous is a complete audit of the portal's public surface and that it returns THREE
    //    lines: two on AccountController.Login, and THIS ONE. That is a deliberate, documented widening of
    //    the portal's public surface — not an oversight, and not something to copy onto the next
    //    controller.
    //
    // 🔴 2. AgentApiKeyFilter IS THE ONLY THING CLOSING THAT GAP.
    //    Authentication here is the X-Agent-Key header and nothing else. If the [ServiceFilter] below is
    //    removed, if the AddScoped<AgentApiKeyFilter>() registration in Program.cs is dropped (which
    //    would throw at request time rather than fail open — the one failure here that is loud), or if
    //    the filter is ever edited to continue past a bad key, every endpoint on this controller becomes
    //    an unauthenticated read. The endpoints below return patient names, phone numbers, screening
    //    results and clinician schedules, so the cost of that is a patient-data leak. The two
    //    tests that prove the guard — a call with no header and a call with a wrong key, both expecting
    //    401 — belong in every verification pass, not just the one that built the filter.
    //
    // 🔴 3. [IgnoreAntiforgeryToken] DISABLES THE GLOBAL AutoValidateAntiforgeryTokenAttribute.
    //    Program.cs validates every non-GET action in the portal (§2.4). An external caller has no
    //    __Host-CSRF cookie, cannot be issued one, and therefore cannot produce the X-CSRF-TOKEN header
    //    that pairs with it — so without this attribute every POST to this controller returns 400 with no
    //    useful message. This is safe here for the reason antiforgery exists: CSRF is an attack on
    //    AMBIENT credentials, and this controller has none. It authenticates on a header a browser will
    //    never attach by itself.
    //
    // 🔴 4. EVERY WRITE MADE THROUGH THIS CONTROLLER IS AUDITED AS AGENT_SERVICE.
    //    Not as the patient, not as the coordinator who approved the message, and above all not as user
    //    0. AgentApiKeyFilter resolves the seeded AGENT_SERVICE row from dbo.Users PER REQUEST and builds
    //    the ClaimsPrincipal that DatabaseHelper.CurrentUserId reads, so the actor arrives at the 19
    //    audit-actor procedures the same way a logged-in user's would. A missing row is a 503 and no
    //    action runs. See CoreFlow.md §13.3.
    //
    // House style is unchanged from the rest of the portal (§0, §11.3): hand-built camelCase anonymous
    // objects, never a serialized CRC.Data model (§12 #4); catch SqlException then Exception, log both,
    // return AgentErrorResponse.ForUser; no SQL and no procedure name anywhere in this project — every
    // read goes through IDatabaseData (§12 #2).
    // =================================================================================================
    [ApiController]
    [Route("api/agent")]
    [AllowAnonymous]
    [ServiceFilter(typeof(AgentApiKeyFilter))]
    [IgnoreAntiforgeryToken]
    public class AgentApiController : ControllerBase
    {
        private readonly IDatabaseData _data;
        private readonly ILogger<AgentApiController> _logger;

        public AgentApiController(IDatabaseData data, ILogger<AgentApiController> logger)
        {
            _data = data;
            _logger = logger;
        }

        // GET: /api/agent/branches
        //
        // Endpoint 5 of the eight. The agent asks "which hospitals can I offer?" before it asks anything
        // about staff, hours or a patient, so this is the cheapest of the eight and the only one whose
        // worst-case leak is a list of hospital names — which is why the guard was built and proven
        // against it, before any endpoint existed that returns a patient.
        //
        // Calls the EXISTING GetActiveBranchesAsync (spBranch_ListActive), the same method the staff and
        // appointment branch dropdowns use. Active branches only, ordered by branch name; an empty list
        // is a legitimate answer, not an error.
        //
        // 🔴 THE THREE PROPERTY NAMES BELOW ARE A PUBLISHED CONTRACT. branchId / name / state, camelCase,
        // mapped by hand out of BranchOption (Branch_ID / Branch_Name / Branch_State) so that renaming a
        // data-layer property is a compile-time change with no effect on the wire, and a procedure
        // gaining a column does not silently gain a JSON field. `branchId` is the value every other agent
        // endpoint takes back as its branchId argument; `name` and `state` are what the agent says out
        // loud to a patient. CoreFlow.md §13.4 publishes them, and the other six reads follow the same
        // style: the model's noun without its table prefix, lower-camel.
        [HttpGet("branches")]
        public async Task<IActionResult> GetBranches()
        {
            try
            {
                var branches = await _data.GetActiveBranchesAsync();

                return Ok(new
                {
                    success = true,
                    data = branches.Select(b => new
                    {
                        branchId = b.Branch_ID,
                        name = b.Branch_Name,
                        state = b.Branch_State
                    })
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException listing active branches for the Agent API.");
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving branches."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error listing active branches for the Agent API.");
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving branches."));
            }
        }

        // =============================================================================================
        // THE THREE PRIVACY RULES THIS CONTROLLER ENFORCES, STATED ONCE BEFORE THE ENDPOINTS THAT
        // ENFORCE THEM. AgentApiPlan.md, "What must never leave through this API".
        //
        // 🔴 1. nricLast4, NEVER THE FULL NRIC. Two of the procedures below already reduce it in SQL and
        //    never select the column. GetPatient is the ONE endpoint in this API where the full value is
        //    even in memory — PatientBasicDetail carries it, because spPatientBasic_GetById is the Patient
        //    Edit form's read — and it is reduced in C# there instead. Search this file for Patient_NRIC:
        //    it appears exactly once, inside a Substring.
        //
        // 🔴 2. THE PATIENT PROJECTION IS DELIBERATELY NARROW. GetPatient maps a handful of fields out of
        //    a 27-column model. Address, e-mail, emergency contact, birth date, race, religion, marital
        //    status and occupation are all present on the model and all deliberately absent from the wire.
        //    Widening it is a privacy decision, not a convenience.
        //
        // 🔴 3. Staff_Phone IS FOR THE CLINICIAN-CONFIRMATION STEP. Endpoints 6 and 7 return it because
        //    the agent has to ask a clinician to confirm an hour before a patient is told anything. THE
        //    API IS NOT WHAT STOPS IT REACHING A PATIENT — the agent's own system prompt is. Anyone
        //    reusing these two endpoints for a patient-facing surface must strip it themselves.
        //
        // ON ROUTE ORDER: "patients/queue" and "patients/by-phone" are literal segments and
        // "patients/{patientId}" is a parameter segment. Attribute routing scores a literal above a
        // parameter, so the two named routes win regardless of the order they appear in this file and
        // there is no ambiguity to resolve. Proven by driving all three (§13.4).
        // =============================================================================================

        // GET: /api/agent/patients/queue
        //
        // Endpoint 1 of the eight, and the one that returns the most patient data in a single response:
        // every ACTIVE patient in the programme. It is what the negative tests should be pointed at,
        // because the cost of the guard failing here is the whole cohort.
        //
        // Calls GetAgentScreeningQueueAsync (spAgentPatient_ListScreeningQueue). Active means
        // DischargeType_ID IS NULL and nothing else — there is no status column (CoreFlow.md §3.8).
        // Ordered Patient_ID DESC by the procedure; an empty list is a legitimate answer, not an error.
        //
        // 🔴 screeningState AND openAppointmentCount ARE NAMED EXACTLY THAT BECAUSE n8n's DAILY SWEEP
        // BRANCHES ON BOTH. screeningState selects the message; openAppointmentCount is the only
        // duplicate-booking guard that exists anywhere in nucentra (dbo.PatientAppointment has nothing
        // unique except its primary key — §3.9), and a non-zero value means the sweep skips this patient.
        // Renaming either is a breaking change to a workflow that lives outside this repository.
        [HttpGet("patients/queue")]
        public async Task<IActionResult> GetScreeningQueue()
        {
            try
            {
                var queue = await _data.GetAgentScreeningQueueAsync();

                return Ok(new
                {
                    success = true,
                    data = queue.Select(p => new
                    {
                        patientId = p.Patient_ID,
                        name = p.Patient_Name,
                        phone = p.Patient_Phone,

                        // The procedure reduced the NRIC in SQL and never selected the column, so there
                        // is no full value on this path to leak. Rule 1 above.
                        nricLast4 = p.NricLast4,

                        // NO_PHONE / UNRECORDED / INCOMPLETE / POSITIVE / NEGATIVE / RESULT_PENDING.
                        // RESULT_PENDING is status = 1 with a NULL result — sample done, lab result not
                        // in yet — and is a different fact from UNRECORDED (nothing recorded at all).
                        screeningState = p.ScreeningState,

                        // Nullable all the way to the wire — a JSON null means "never recorded", which is
                        // a different thing from false, and the agent must be able to tell them apart.
                        iFobtStatus = p.Patient_iFOBTStatus,
                        iFobtResult = p.Patient_iFOBTResults,

                        // ISO yyyy-MM-dd, InvariantCulture, or null when nothing was recorded. NOT the
                        // portal's dd/MM/yyyy: this caller is a machine, and a culture-formatted date is
                        // a parsing bug waiting for a server with a different locale.
                        iFobtCompletionDate = p.Patient_iFOBTCompletionDate?.ToString("yyyy-MM-dd",
                            CultureInfo.InvariantCulture),

                        openAppointmentCount = p.OpenAppointmentCount,
                        hasAssessment = p.HasAssessment
                    })
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException listing the screening queue for the Agent API.");
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving the screening queue."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error listing the screening queue for the Agent API.");
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving the screening queue."));
            }
        }

        // GET: /api/agent/patients/by-phone?phone=60123456789
        //
        // Endpoint 2 of the eight: an inbound WhatsApp number arrives and the agent needs to know who is
        // speaking. Calls FindAgentPatientsByPhoneAsync (spAgentPatient_FindByPhone).
        //
        // 🔴 matchCount IS NOT DECORATION, AND IT IS WHY THIS ENDPOINT'S ENVELOPE HAS A THIRD PROPERTY.
        // The procedure returns ZERO, ONE OR MANY rows and all three are normal answers, because
        // dbo.PatientBasic HAS NOTHING UNIQUE EXCEPT ITS PRIMARY KEY (CoreFlow.md §3.8) — not the NRIC,
        // not the e-mail, not the phone. A shared household number resolving to two people is a state the
        // schema permits. On top of that the match is on the LAST NINE DIGITS rather than on equality,
        // because Meta delivers 60123456789 and the portal stores whatever an administrator typed; nine
        // digits is not unique either, so 012-345 6789 and 011-2345 6789 collide.
        //
        // THE AGENT IS REQUIRED TO ASK A DISAMBIGUATING QUESTION WHEN matchCount > 1 AND MUST NEVER TAKE
        // THE FIRST ROW. Handing back a count rather than making the caller measure the array is what
        // makes that branch impossible to overlook in an n8n expression.
        //
        // matchCount = 0 is a SUCCESSFUL answer, not a failure: it means "this number is not one of ours",
        // which the agent handles by asking rather than by retrying.
        [HttpGet("patients/by-phone")]
        public async Task<IActionResult> FindPatientsByPhone([FromQuery] string? phone)
        {
            // Refused before the data layer is touched. Calling the procedure with an empty string would
            // take its early return and answer "no rows", which reads as "no such patient" — a caller
            // that forgot the parameter would be told, in good faith, that the number is not registered.
            if (string.IsNullOrWhiteSpace(phone))
            {
                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "A phone number is required. Supply it as ?phone=, in any format — digits, dashes, "
                    + "spaces and a leading + or 60 are all accepted."));
            }

            try
            {
                var matches = await _data.FindAgentPatientsByPhoneAsync(phone.Trim());

                return Ok(new
                {
                    success = true,
                    matchCount = matches.Count,
                    data = matches.Select(p => new
                    {
                        patientId = p.Patient_ID,
                        name = p.Patient_Name,

                        // The number AS STORED, not the digits the match was made on.
                        phone = p.Patient_Phone,

                        // Reduced in SQL by the procedure; the full column is never selected. Rule 1.
                        nricLast4 = p.NricLast4,

                        iFobtStatus = p.Patient_iFOBTStatus,
                        iFobtResult = p.Patient_iFOBTResults,

                        // Left null rather than coerced to "": a null discharge type IS the definition of
                        // an active patient (CoreFlow.md §3.8) and "" would erase that meaning.
                        dischargeTypeId = p.DischargeType_ID,

                        // Derived from the property above and nothing else. It carries no information the
                        // caller does not already have — it exists because "null means active" is a
                        // nucentra convention an external workflow has no way to know, and reading it
                        // backwards would have the agent treat every active patient as discharged.
                        isActive = p.DischargeType_ID == null
                    })
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException finding patients by phone for the Agent API.");
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error searching for the patient."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error finding patients by phone for the Agent API.");
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error searching for the patient."));
            }
        }

        // GET: /api/agent/patients/PAT-000042
        //
        // Endpoint 3 of the eight: one patient's record, read after identity has been confirmed. Calls
        // the EXISTING GetPatientByIdAsync (spPatientBasic_GetById) — the same method PatientController.
        // GetBasic and StaffPatientController.GetBasic use. One method per procedure, whoever calls it.
        //
        // The two answers match PatientController.GetBasic's wording exactly: a hit is
        // { success = true, data = … } and a miss is { success = false, message = "Patient not found." }.
        // A miss is NOT an error and writes nothing to app-*.log — an id that does not exist is an
        // ordinary answer to an ordinary question.
        //
        // 🔴🔴 THE FULL NRIC. PatientBasicDetail CARRIES Patient_NRIC IN FULL — twelve digits, the whole
        // national identity number — because spPatientBasic_GetById is the Patient Edit form's read and
        // that form has to show it. THIS IS THE ONLY PLACE IN THE ENTIRE AGENT API WHERE THE FULL VALUE IS
        // EVEN IN MEMORY; the other two patient endpoints go through procedures that reduce it in SQL and
        // never select the column at all.
        //
        // ONLY nricLast4 IS PROJECTED, COMPUTED HERE FROM THE LAST FOUR CHARACTERS OF THE TRIMMED VALUE,
        // AND THE COLUMN ITSELF IS NEVER RETURNED. The agent confirms identity by asking the patient for
        // four digits and comparing what it is told against what it holds — it must never be ABLE to
        // state more than that, whatever a prompt injected into a WhatsApp message asks it for. An
        // agent that can print an NRIC is an agent that will, eventually, print it to the wrong person.
        //
        // 🔴 THE PROJECTION BELOW IS DELIBERATELY NARROW, AND WIDENING IT IS A PRIVACY DECISION RATHER
        // THAN A CONVENIENCE. PatientBasicDetail has 27 columns plus six joined lookup names. What the
        // agent gets is the name and phone it needs to hold a conversation, the iFOBT trio the whole
        // programme turns on, and the discharge state that says whether this patient is still in it.
        // Deliberately absent and present on the model: the full NRIC, the e-mail, the residential
        // address, the emergency contact, the birth date and age, and race / religion / marital status /
        // occupation. Nothing clinical beyond the iFOBT trio exists on this row to leak — the assessment,
        // colonoscopy and follow-up tables are not reachable from this API at all.
        //
        // ON "BRANCH IF PRESENT": THERE IS NO BRANCH ON A PATIENT. dbo.PatientBasic has no Branch_ID and
        // no branch of any kind (CoreFlow.md §3.8) — a patient is not attached to a facility, only their
        // APPOINTMENTS are, and those are endpoint 4. Checked against the table's DDL and against
        // PatientBasicDetail before concluding it, because its absence reads like an oversight.
        [HttpGet("patients/{patientId}")]
        public async Task<IActionResult> GetPatient(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return Ok(new { success = false, message = "Patient not found." });
            }

            try
            {
                var row = await _data.GetPatientByIdAsync(patientId.Trim());

                if (row == null)
                {
                    return Ok(new { success = false, message = "Patient not found." });
                }

                // 🔴 THE ONLY MENTION OF THE NRIC ANYWHERE IN THIS PROJECT'S CODE, AND IT IS A REDUCTION.
                // Trimmed first because the stored value is VARCHAR(100) holding twelve digits and a
                // trailing space would otherwise become one of the four. A value shorter than four
                // characters is handed back whole rather than throwing — there is nothing to hide in it.
                var nric = row.Patient_NRIC?.Trim() ?? string.Empty;
                var nricLast4 = nric.Length >= 4 ? nric.Substring(nric.Length - 4) : nric;

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        patientId = row.Patient_ID,
                        name = row.Patient_Name,
                        phone = row.Patient_Phone,

                        nricLast4,

                        iFobtStatus = row.Patient_iFOBTStatus,
                        iFobtResult = row.Patient_iFOBTResults,
                        iFobtCompletionDate = row.Patient_iFOBTCompletionDate?.ToString("yyyy-MM-dd",
                            CultureInfo.InvariantCulture),

                        // Both left null when absent rather than coerced to "": the null IS the state.
                        dischargeTypeId = row.DischargeType_ID,
                        dischargeTypeName = row.DischargeType_Name,
                        isActive = row.DischargeType_ID == null
                    }
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException loading patient {PatientId} for the Agent API.",
                    patientId);
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving the patient."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading patient {PatientId} for the Agent API.",
                    patientId);
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving the patient."));
            }
        }

        // GET: /api/agent/patients/PAT-000042/appointments
        //
        // Endpoint 4 of the eight, and the agent calls it BEFORE it proposes any booking: if a future
        // appointment with status "Scheduled" already exists, it must tell the patient what they already
        // have rather than offering a second one. Nothing in the schema would stop that second booking —
        // dbo.PatientAppointment has nothing unique except its primary key (CoreFlow.md §3.9) — so this
        // read and the screening queue's openAppointmentCount are the whole of the duplicate guard.
        //
        // Calls the EXISTING GetAppointmentsByPatientAsync (spPatientAppointment_ListByPatient), the same
        // method the Appointment tab of /Patient/Edit uses.
        //
        // AN UNKNOWN PATIENT ID RETURNS AN EMPTY LIST, NOT AN ERROR, and that is the procedure's doing
        // rather than a choice made here: spPatientAppointment_ListByPatient is a plain
        // WHERE Patient_ID = @Patient_ID with no existence check, so an id that matches nothing simply
        // selects nothing. Confirmed by driving it with a made-up id (§13.4). The agent must therefore not
        // read an empty list as "this patient is not registered" — endpoint 3 answers that question.
        //
        // 🔴 THE ORDER IS THE CONTRACT AND THE CALLER MUST NOT RE-SORT: date DESC, then start time DESC,
        // then id DESC — newest first. .Select preserves it; there is deliberately no OrderBy here.
        [HttpGet("patients/{patientId}/appointments")]
        public async Task<IActionResult> GetPatientAppointments(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return Ok(new { success = true, data = Array.Empty<object>() });
            }

            try
            {
                var appointments = await _data.GetAppointmentsByPatientAsync(patientId.Trim());

                return Ok(new
                {
                    success = true,
                    data = appointments.Select(a => new
                    {
                        appointmentId = a.PatientAppointment_ID,

                        // ISO yyyy-MM-dd or null. The portal's own endpoint returns this same column
                        // TWICE, as dd/MM/yyyy for a table cell and yyyy-MM-dd for a date input; a
                        // machine caller needs one format and it is the unambiguous one.
                        appointmentDate = a.PatientAppointment_Date?.ToString("yyyy-MM-dd",
                            CultureInfo.InvariantCulture),

                        // Already "08:00" strings from CONVERT(…, 108) in the procedure; passed through
                        // verbatim. Named startTime/endTime here rather than the portal's from/to.
                        startTime = a.PatientAppointment_StartTime,
                        endTime = a.PatientAppointment_EndTime,

                        // "Scheduled" / "Attended" / "Not Attended" — defined in C# in four places and
                        // nowhere in the database: no lookup table, no check constraint, no default
                        // (CoreFlow.md §3.9). The agent tests for a FUTURE "Scheduled" one.
                        status = a.PatientAppointment_Status,

                        staffId = a.Staff_ID,

                        // The three joined names are LEFT JOINs onto tables nothing constrains the ids
                        // to, so each is genuinely nullable and each is coerced to "" — the agent reads
                        // these out loud to a patient, and a JSON null would become the word "null" in a
                        // WhatsApp message.
                        staffName = a.Staff_Name ?? "",

                        branchId = a.Branch_ID,
                        branchName = a.Branch_Name ?? "",

                        appointmentTypeId = a.PjAppType_ID,
                        appointmentType = a.PjAppType_Name ?? ""
                    })
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex,
                    "SqlException listing appointments for patient {PatientId} for the Agent API.",
                    patientId);
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving appointments."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error listing appointments for patient {PatientId} for the Agent API.",
                    patientId);
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving appointments."));
            }
        }

        // GET: /api/agent/staff?branchId=022367001
        //
        // Endpoint 6 of the eight: the clinicians BASED at one branch (Staff_Base = branchId), so the
        // agent can name the doctor attached to an hour it is proposing. Calls GetAgentStaffByBranchAsync
        // (spAgentStaff_ListByBranch); ordered by staff name.
        //
        // An unknown branch id returns an EMPTY LIST rather than an error — Staff_Base holds a
        // Branch.Branch_ID by convention only, with no foreign key to violate (CoreFlow.md §3.4).
        //
        // 🔴 phone IS Staff_Phone AND IT IS HERE FOR THE CLINICIAN-CONFIRMATION STEP — the agent asks a
        // clinician to confirm an hour before a patient is told anything. IT IS NOT FOR RELAYING TO A
        // PATIENT, AND THIS API IS NOT WHAT STOPS THAT HAPPENING: the agent's own system prompt is the
        // enforcement point. Said here so nobody reads the projection as permission, and so that anyone
        // reusing this endpoint for a patient-facing surface knows they must strip it themselves.
        //
        // staffTypeName is null for any clinician whose Staff_Type is no longer in dbo.LU_STAFFTYPE —
        // a LEFT JOIN, deliberately, because an INNER one would drop a clinician who is standing in the
        // branch out of the branch's list, silently. Left null rather than coerced so the caller can tell
        // "unknown code" from a type genuinely named "".
        [HttpGet("staff")]
        public async Task<IActionResult> GetStaffByBranch([FromQuery] string? branchId)
        {
            if (string.IsNullOrWhiteSpace(branchId))
            {
                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "A branch id is required. Supply it as ?branchId=, using a branchId from "
                    + "GET /api/agent/branches."));
            }

            try
            {
                var staff = await _data.GetAgentStaffByBranchAsync(branchId.Trim());

                return Ok(new
                {
                    success = true,
                    data = staff.Select(s => new
                    {
                        staffId = s.Staff_ID,
                        name = s.Staff_Name,
                        phone = s.Staff_Phone,
                        staffType = s.Staff_Type,
                        staffTypeName = s.StaffType_Name,
                        branchId = s.Staff_Base
                    })
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException listing staff at branch {BranchId} for the Agent API.",
                    branchId);
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving staff."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error listing staff at branch {BranchId} for the Agent API.", branchId);
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving staff."));
            }
        }

        // GET: /api/agent/slots/open?branchId=022367001&fromDate=2026-09-01&toDate=2026-09-30&staffType=END
        //
        // Endpoint 7 of the eight: every OPEN HOUR at one branch between two dates, optionally narrowed to
        // one staff type. Calls FindAgentOpenSlotsByBranchAsync (spAgentSlots_FindOpenByBranch); ordered
        // by date, then start time, then staff name.
        //
        // ONE ROW IS ONE HOUR. dbo.StaffSlots holds exactly one on-the-hour hour per row, by check
        // constraint (CoreFlow.md §3.7), so a clinician free from 9 to 12 comes back as THREE rows and it
        // is the caller that groups them into something a patient would recognise.
        //
        // 🔴 THIS READ IS ADVISORY AND A SLOT RETURNED HERE CAN BE TAKEN A SECOND LATER. It runs outside
        // any transaction and holds no lock, and nothing in the schema would catch the double-claim:
        // dbo.PatientAppointment has no unique constraint beyond its identity and dbo.StaffSlots has
        // nothing unique on PatientAppointment_ID (§3.9). The only defence is the re-read INSIDE
        // SaveAppointmentAsync's own transaction, which refuses with the typed reason SlotTaken (§6.7).
        // A SlotTaken answer from the booking endpoint below is therefore A NORMAL OUTCOME OF A CORRECT
        // SYSTEM, not a bug in this endpoint and not a client error — the caller re-runs this read and
        // tries again. Do not "fix" it by moving this into a transaction: it is the picker, not the check.
        //
        // 🔴 phone IS Staff_Phone, FOR THE CLINICIAN-CONFIRMATION STEP AND NOT FOR A PATIENT — the same
        // rule and the same enforcement point as endpoint 6 above.
        [HttpGet("slots/open")]
        public async Task<IActionResult> GetOpenSlots(
            [FromQuery] string? branchId,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate,
            [FromQuery] string? staffType)
        {
            if (string.IsNullOrWhiteSpace(branchId))
            {
                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "A branch id is required. Supply it as &branchId=, using a branchId from "
                    + "GET /api/agent/branches."));
            }

            // 🔴 TryParseExact WITH ONE FORMAT AND InvariantCulture, NEVER DateTime.Parse. A plain Parse
            // accepts a locale-dependent string, so "01/09/2026" would be read as 1 September on one
            // server and 9 January on another — the same request producing a different month depending on
            // where it landed, with no error anywhere. A failure here is a refusal in the house envelope
            // that NAMES THE EXPECTED FORMAT, not an exception and not a 500: the caller mistyped an
            // argument, which is a thing it can fix, and it can only fix it if it is told what was wrong.
            if (!DateTime.TryParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsedFromDate))
            {
                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "fromDate is required and must be yyyy-MM-dd (for example 2026-09-01)."));
            }

            if (!DateTime.TryParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsedToDate))
            {
                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "toDate is required and must be yyyy-MM-dd (for example 2026-09-30)."));
            }

            // staffType is GENUINELY OPTIONAL and null means "do not filter"
            // (`@Staff_Type IS NULL OR s.Staff_Type = @Staff_Type`). A BLANK STRING IS NOT THE SAME THING
            // — it would match no staff type at all and the endpoint would answer "no availability" to a
            // caller that meant "any clinician" — so blank is converted to null HERE, before the call,
            // which is the same rule every optional filter in this solution follows (§5.7).
            var normalizedStaffType = string.IsNullOrWhiteSpace(staffType) ? null : staffType.Trim();

            try
            {
                var slots = await _data.FindAgentOpenSlotsByBranchAsync(
                    branchId.Trim(), parsedFromDate, parsedToDate, normalizedStaffType);

                return Ok(new
                {
                    success = true,
                    data = slots.Select(s => new
                    {
                        // The value the booking write takes back as its slotIds argument — round-trip it
                        // unchanged.
                        slotId = s.StaffSlot_ID,

                        staffId = s.Staff_ID,
                        staffName = s.Staff_Name,
                        staffPhone = s.Staff_Phone,
                        staffType = s.Staff_Type,

                        slotDate = s.SlotDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),

                        // Already "09:00" strings from CONVERT(…, 108) in the procedure — see
                        // AgentOpenSlotItem for why they are strings and not TimeSpans. Passed through
                        // verbatim; formatting them again would only be a chance to get them wrong.
                        startTime = s.SlotStartTime,
                        endTime = s.SlotEndTime
                    })
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex,
                    "SqlException finding open slots at branch {BranchId} between {FromDate} and {ToDate} "
                    + "for the Agent API.", branchId, fromDate, toDate);
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving open slots."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error finding open slots at branch {BranchId} between {FromDate} and "
                    + "{ToDate} for the Agent API.", branchId, fromDate, toDate);
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving open slots."));
            }
        }

        // =============================================================================================
        // ENDPOINT 8 — THE ONE WRITE. Everything above this line reads; everything below it books a real
        // clinician's hour on a clinical system. CoreFlow.md §13.5.
        // =============================================================================================

        // 🔴 THE APPOINTMENT TYPE THIS API IS ALLOWED TO BOOK, AND THE ONLY ONE.
        //
        // "01" is PATIENT ASSESSMENT (CoreFlow.md §7.3). It is A STRING WITH A LEADING ZERO and it is not
        // the integer 1 — PjAppType_ID is VARCHAR(100) like almost every other business key in nucentra
        // (§0), so a caller that sends 1, or "1", is not sending this value.
        //
        // AgentApiPlan.md DECISION 4 — SURVEILLANCE, OPTION C, PROPOSE-ONLY — is why this is a constant
        // and not a parameter. The agent never books a "04" SURVEILLANCE appointment. The coordinator
        // opens the slot range and books those by hand, for two reasons that stand independently:
        //
        //   • A surveillance visit is scheduled 12 or 24 months out, and spStaffSlots_CreateRange refuses
        //     a range longer than 31 days per call (§5.5) — so in practice THERE IS NO SLOT THAT FAR OUT
        //     TO CONSUME, and a booking attempt would fail on the slot check anyway.
        //   • 🔴 A "04" APPOINTMENT COULD NEVER BE CLINICALLY RECORDED EVEN IF IT WERE BOOKED.
        //     GetJourneyTemplate recognises exactly three strings — "PATIENT ASSESSMENT", "COLONOSCOPY",
        //     "PATIENT FOLLOW UP" — and answers 400 "Unsupported journey type." to anything else; there
        //     is no dbo.PatientSurveillance, no spPatientSurveillance_*, no view and no .js (§7.3). So a
        //     surveillance visit CAN be booked and can never be written up. Booking one through an
        //     automation would consume a clinician's hour to create a record nobody can complete.
        //
        // TO MOVE TO OPTION A (agent books surveillance inside a short horizon), CHANGE ONE THING: replace
        // the equality test in BookAppointment with membership of a set containing "01" and "04", and
        // widen the refusal message. Nothing else in this file, in CRC.Data or in the database is
        // type-aware — SaveAppointmentAsync passes PjAppType_ID straight through and validates nothing
        // about it (§5.7), which is exactly why the rule has to live here.
        private const string AllowedAppointmentTypeId = "01";

        // The request body for POST /api/agent/appointments.
        //
        // A NESTED CLASS INSIDE THE CONTROLLER, like every other request DTO in this solution
        // (CoreFlow.md §10, §11.3) — PatientController.SaveAppointmentRequest is the one this mirrors.
        //
        // 🔴 THERE ARE DELIBERATELY NO DATA ANNOTATIONS ON IT — NO [Required], NO [Range], NO
        // [MinLength]. THIS IS NOT AN OVERSIGHT.
        //
        // [ApiController] on this class turns an invalid ModelState into an automatic 400 carrying a
        // ProblemDetails body: { "type": …, "title": "One or more validation errors occurred.",
        // "status": 400, "errors": { … } }. That is NOT the envelope this API speaks, and it is not the
        // envelope the caller branches on — an n8n workflow reading `success` and `reason` off every
        // other endpoint would get neither, from a response shape it has never seen, for the request that
        // matters most. With no attributes there is nothing for the automatic filter to find, ModelState
        // stays valid, and EVERY validation answer below is written here, in the house envelope, in
        // wording chosen for a machine caller that has to fix its own payload.
        //
        // What that does NOT buy: a body that is not valid JSON at all, or a "slotIds": ["x"] that cannot
        // bind to int[], is still a deserialization failure and still answers 400 ProblemDetails before
        // this action runs. That case is unreachable from a caller that sends the documented shape, and
        // closing it would mean binding the body as a string and parsing it by hand.
        //
        // Every string is nullable and defaults are absent on purpose: an omitted field must be
        // distinguishable from a blank one only insofar as both are refused, and coercing to "" in the
        // DTO would hide which of the two the caller sent from anyone reading this class.
        public class BookAppointmentRequest
        {
            public string? PatientId { get; set; }

            // yyyy-MM-dd. A STRING, not a DateTime: see the TryParseExact in the action for why the
            // parse is done by hand and refused by hand.
            public string? AppointmentDate { get; set; }

            public string? StaffId { get; set; }

            // StaffSlot_IDs from GET /api/agent/slots/open, one per hour.
            public int[]? SlotIds { get; set; }

            // "01", and nothing else — see AllowedAppointmentTypeId above.
            public string? PjAppTypeId { get; set; }

            public string? BranchId { get; set; }

            // "Scheduled", and nothing else — see the check in the action.
            public string? Status { get; set; }
        }

        // POST: /api/agent/appointments
        //
        // Endpoint 8 of the eight, and THE ONLY WRITE IN THIS API. It is the machine-facing twin of
        // PatientController.SaveAppointment: the same data-layer call, the same typed failure reasons,
        // the same audit deferral — and different wording, because the reader is a workflow rather than a
        // person, and different rules where a machine caller can send something a form cannot.
        //
        // 🔴 THIS ENDPOINT ONLY EVER INSERTS. There is no appointmentId in the request DTO and
        // PatientAppointment_ID is hard-wired to 0 below (the data layer's insert/update switch —
        // AppointmentSaveInput). It never updates and never deletes, and it must not learn how:
        // AgentApiPlan.md puts a coordinator's approval in front of this call precisely because the
        // consequences of a wrong booking cannot be undone from here.
        //
        // 🔴 THERE IS NO CANCELLATION CONCEPT IN NUCENTRA (CoreFlow.md §3.9). A status change touches no
        // slots — marking an appointment "Not Attended" leaves every hour it holds consumed. The only way
        // to free an hour is to DELETE the appointment, which is POST /Patient/DeleteAppointment, in the
        // portal, by a human. So a booking made here consumes a real clinician hour that this API cannot
        // give back, and that asymmetry — trivially easy to take, impossible to return — is the whole
        // argument for the human approval gate that sits in front of it.
        //
        // 🔴 DO NOT WRITE A SECOND BOOKING PATH HERE. The transaction, the in-transaction slot read that
        // IS the concurrency check, the four slot checks, the contiguity check, the start/end derivation
        // and the two slot procedures all live inside SaveAppointmentAsync and are reused WHOLE
        // (CoreFlow.md §6.7, §12 #1). There is no correct way to reimplement them from a controller: the
        // availability answer is worth something only while the transaction that asked holds its locks,
        // so a "simpler" version that reads the slots here and then saves would let two callers both see
        // an hour as free and both take it — and nothing in the schema would catch it, because
        // dbo.PatientAppointment has no unique constraint beyond its identity (§3.9). This prompt added
        // no procedure and no data-layer method for exactly that reason.
        [HttpPost("appointments")]
        public async Task<IActionResult> BookAppointment([FromBody] BookAppointmentRequest? request)
        {
            if (request == null)
            {
                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "A JSON body is required: patientId, appointmentDate, staffId, slotIds, pjAppTypeId, "
                    + "branchId and status."));
            }

            // Trimmed once, here, and every check below reads these locals rather than the DTO — the same
            // shape PatientController.SaveAppointment uses. Trimming whitespace is not the kind of
            // rewrite the status check refuses further down: " PAT-000011 " and "PAT-000011" name the same
            // patient, whereas "attended" and "Attended" are stored as different values (§3.9).
            var patientId = request.PatientId?.Trim() ?? "";
            var staffId = request.StaffId?.Trim() ?? "";
            var branchId = request.BranchId?.Trim() ?? "";
            var pjAppTypeId = request.PjAppTypeId?.Trim() ?? "";
            var status = request.Status?.Trim() ?? "";
            var dateStr = request.AppointmentDate?.Trim() ?? "";
            var rawSlotIds = request.SlotIds ?? Array.Empty<int>();

            // Kept for the audit line on a refusal, before the ids have been cleaned up — a refusal is
            // worth reading later, and reading it against the ids as SENT is the point.
            var auditSlotIds = (IReadOnlyList<int>)rawSlotIds;

            // ── VALIDATION, BY HAND, IN ORDER, FIRST FAILURE WINS ────────────────────────────────────
            //
            // Nucentra_WhatsApp_Agent_Plan.md §3.8 is the list of formats the portal rejects a caller for;
            // these are the same rules PatientController enforces, not a parallel set.

            // 1. The three ids that are business keys. NONE of them is a foreign key and NOTHING
            //    downstream checks that any of them resolves: spPatientAppointment_Insert validates
            //    nothing at all (§5.7), so an appointment can be booked against three ids that mean
            //    nothing. Blank is the one shape that can be caught here; a well-formed id for a patient
            //    who does not exist is caught by the agent calling endpoint 3 first, and by the
            //    coordinator who approves the booking.
            if (string.IsNullOrWhiteSpace(patientId) ||
                string.IsNullOrWhiteSpace(staffId) ||
                string.IsNullOrWhiteSpace(branchId))
            {
                AgentAuditLog.AgentAppointmentRefused(HttpContext, "missing id", patientId, staffId,
                    branchId, dateStr, auditSlotIds);

                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "patientId, staffId and branchId are all required. Take patientId from "
                    + "GET /api/agent/patients/by-phone, staffId from GET /api/agent/slots/open and "
                    + "branchId from GET /api/agent/branches."));
            }

            // 2. 🔴 TryParseExact WITH ONE FORMAT AND InvariantCulture, EXACTLY AS PatientController DOES
            //    AND EXACTLY AS ENDPOINT 7 DOES. A plain DateTime.Parse accepts a locale-dependent string,
            //    so "01/09/2026" would be read as 1 September on one server and 9 January on another — the
            //    same request booking a different month depending on where it landed, with nothing logged
            //    anywhere and a real clinician hour consumed on the wrong day. Refused, naming the format.
            if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var appointmentDate))
            {
                AgentAuditLog.AgentAppointmentRefused(HttpContext, "date not yyyy-MM-dd", patientId,
                    staffId, branchId, dateStr, auditSlotIds);

                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "appointmentDate is required and must be yyyy-MM-dd (for example 2026-09-01)."));
            }

            // 3. THE SLOT IDS. AppointmentSaveInput's own comment states the contract this satisfies:
            //    "already de-duplicated and stripped of non-positive values by the caller. Their COUNT is
            //    load-bearing: the slot-existence check is a comparison against how many of them the
            //    in-transaction read gave back." So [17, 17] reaching the data layer would ask for two
            //    slots, get one row back, and be refused SlotNotFound for a request that is perfectly
            //    valid — which is why the de-duplication happens HERE and cannot be skipped.
            //
            //    🔴 DE-DUPLICATING IS NOT THE SAME KIND OF REWRITE THE STATUS CHECK REFUSES. [17, 17] and
            //    [17] ask for the identical set of hours, so removing the duplicate changes nothing about
            //    what is booked. Normalising "attended" to "Attended" would change what is STORED.
            //
            //    A NON-POSITIVE ID IS REFUSED HERE RATHER THAN FILTERED OUT, AND THIS IS THE ONE PLACE
            //    THIS ENDPOINT IS DELIBERATELY STRICTER THAN PatientController. The portal drops them
            //    (`.Where(x => x > 0)`) because its slot picker cannot produce one — a checkbox either
            //    carries a real StaffSlot_ID or is not there. An n8n expression very much can produce one,
            //    out of an unresolved reference or a missing field, and silently dropping it would book
            //    FEWER HOURS THAN THE CALLER ASKED FOR and answer success. On a booking that consumes a
            //    clinician's hour and cannot be cancelled, a request that means two hours must never
            //    quietly become one.
            if (rawSlotIds.Length == 0 || rawSlotIds.Any(id => id <= 0))
            {
                AgentAuditLog.AgentAppointmentRefused(HttpContext, "bad slotIds", patientId, staffId,
                    branchId, dateStr, auditSlotIds);

                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "slotIds must contain at least one positive StaffSlot_ID from "
                    + "GET /api/agent/slots/open. One id is one hour, and several must be consecutive "
                    + "hours on the same day with the same staff member."));
            }

            var slotIds = rawSlotIds.Distinct().ToList();

            // 4. 🔴 THE STATUS MUST BE EXACTLY "Scheduled" — NOT CASE-INSENSITIVELY, AND NOT NORMALISED.
            //    PatientAppointment_Status is a VARCHAR(100) with NO check constraint, NO default and NO
            //    lookup table; the three real values live in C# in four places and nowhere else
            //    (CoreFlow.md §3.9). The portal's own HashSet is case-INSENSITIVE on the way in and the
            //    value is then stored AS SENT — so a booking saved as "attended" satisfies the validation,
            //    appears in the status filter as its own entry beside "Attended", and 🔴 SILENTLY STOPS
            //    COUNTING TOWARD THAT CLINICIAN'S HOURS, because spStaff_GetPerformance sums
            //    `WHERE PatientAppointment_Status = 'Attended'` — a plain equality (§5.5).
            //
            //    This endpoint only ever creates NEW bookings, and a new booking is "Scheduled". So the
            //    other two values are refused because they are meaningless here, and a case variant is
            //    refused rather than corrected: ACCEPTING A SHAPE AND THEN REWRITING IT HIDES THE
            //    CALLER'S BUG. A workflow that sends "scheduled" today is a workflow that sends
            //    "attended" to some other endpoint tomorrow, and the second one nobody catches. Ordinal
            //    equality, so the refusal is exactly as strict as the column's own collation is not.
            if (!string.Equals(status, "Scheduled", StringComparison.Ordinal))
            {
                AgentAuditLog.AgentAppointmentRefused(HttpContext, "status not Scheduled", patientId,
                    staffId, branchId, dateStr, auditSlotIds);

                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "status must be exactly \"Scheduled\", with that capitalisation. This endpoint only "
                    + "creates new bookings; the status is stored exactly as sent and is never corrected."));
            }

            // 5. 🔴 THE APPOINTMENT TYPE MUST BE THE STRING "01". See AllowedAppointmentTypeId above for
            //    the whole argument — AgentApiPlan.md decision 4, surveillance option C, propose-only —
            //    and for the one-line change that relaxes it. Refused BEFORE any data call: nothing below
            //    this line runs, no connection is opened, no slot is read and no transaction is begun.
            if (!string.Equals(pjAppTypeId, AllowedAppointmentTypeId, StringComparison.Ordinal))
            {
                AgentAuditLog.AgentAppointmentRefused(HttpContext, "pjAppTypeId not 01", patientId,
                    staffId, branchId, dateStr, auditSlotIds);

                return Ok(AgentErrorResponse.ForUser(HttpContext,
                    "pjAppTypeId must be the string \"01\" (PATIENT ASSESSMENT). This API books "
                    + "assessment appointments only — a \"04\" SURVEILLANCE visit is opened and booked by "
                    + "the coordinator in the portal, and a surveillance appointment cannot be clinically "
                    + "recorded at all. Note that \"01\" is a string with a leading zero, not the number 1."));
            }

            try
            {
                // 🔴 THE EXISTING METHOD, REUSED WHOLE. One SqlData method per procedure, whoever calls
                // it (§12 #1) — this prompt added no procedure, no IDatabaseData method and no second
                // write path. Everything that needs rows read under a lock is inside it.
                //
                // 🔴 PatientAppointment_ID = 0 IS THE INSERT SWITCH AND IT IS HARD-WIRED. The data layer
                // reads `<= 0` as "insert"; anything else is the id being updated (AppointmentSaveInput).
                // 0 is also the value compared against each slot's PatientAppointment_ID for the
                // SlotTaken check, and no IDENTITY can be 0 — so on this path EVERY already-booked slot
                // fails, with none of the "a slot carrying this appointment's own id is allowed"
                // permission that makes the portal's EDIT path work. That is correct here: there is no
                // edit path here.
                //
                // Note what is NOT passed: the start and end times. They are DERIVED inside the
                // transaction from the slots being consumed — the earliest requested hour's start and the
                // latest one's end — because the slots are read under the transaction's lock and their
                // times are not known until then. A caller that computed them itself would be computing
                // them from a read taken outside the lock, which is the exact race the transaction exists
                // to prevent (§6.6, §6.7).
                var result = await _data.SaveAppointmentAsync(new AppointmentSaveInput
                {
                    PatientAppointment_ID = 0,
                    Patient_ID = patientId,
                    PatientAppointment_Date = appointmentDate,
                    Staff_ID = staffId,
                    SlotIds = slotIds,
                    PjAppType_ID = pjAppTypeId,
                    Branch_ID = branchId,
                    PatientAppointment_Status = status
                });

                if (result.Reason != AppointmentSaveFailure.Ok)
                {
                    // 🔴 A 200 CARRYING success:false, NOT A 4xx, AND THAT IS A DELIBERATE CHOICE ABOUT
                    // WHAT THESE OUTCOMES MEAN.
                    //
                    // SlotTaken says somebody booked that hour BETWEEN the agent's slot read and this
                    // write. GET /api/agent/slots/open runs outside any transaction and holds no lock, so
                    // it is ADVISORY by construction (§6.7, §13.4): the only thing that decides whether an
                    // hour is still free is the read inside SaveAppointmentAsync's transaction. An
                    // administrator taking that hour in the portal a second earlier is a correct system
                    // behaving correctly, and the caller's response is to re-run slot discovery and try
                    // the next hour — not to alert anyone, not to retry the same body, and not to log a
                    // client error. A 4xx would file that normal race under "the agent sent something
                    // wrong", which is both untrue and the kind of untruth that gets a working workflow
                    // switched off. The same reasoning covers the rest: every reason here is an outcome
                    // the flow is DESIGNED to produce, and the request that produced it was well formed.
                    //
                    // A genuine fault — a dropped connection, a constraint violation — still throws and
                    // is caught below. That is the line between the two: `reason` means "we asked and the
                    // answer was no", the catch blocks mean "we could not ask".
                    //
                    // 🔴 `reason` IS THE ENUM NAME, VERBATIM, AND IT IS A PUBLISHED CONTRACT.
                    // AppointmentSaveFailure.SlotTaken reaches the wire as the string "SlotTaken". Not
                    // "slot_taken", not "SLOT_TAKEN", not "Slot taken" — an n8n Switch node branches on
                    // this exact string, so lower-casing it, prettifying it or collapsing two reasons
                    // onto one shared value is a breaking change to a workflow no compiler here can see.
                    // ToString() on the enum is what guarantees it: rename a member and the wire follows,
                    // which is the only rename anybody would want to be automatic.
                    //
                    // The MESSAGE is this endpoint's own and deliberately differs from PatientController's
                    // — that one says "Please reload the slots and try again", which is advice for a
                    // person looking at a screen. THE DATA LAYER DECIDES WHAT FAILED; THE CONTROLLER
                    // DECIDES WHAT THE CALLER IS TOLD (AppointmentSaveFailure's own comment makes this
                    // argument at length, and it is why the enum exists instead of a message string).
                    // Two callers, two vocabularies, one set of reasons.
                    var message = result.Reason switch
                    {
                        AppointmentSaveFailure.SlotNotFound =>
                            "One or more slotIds do not exist for that staff member on that date. Re-run "
                            + "GET /api/agent/slots/open and book from the ids it returns.",

                        // 🔴 THESE TWO ARE CURRENTLY UNREACHABLE, AND A CALLER MUST STILL HANDLE THEM.
                        // SaveAppointmentAsync's slot read is spStaffSlots_List narrowed to @Staff_ID and
                        // to @FromDate = @ToDate = the appointment's date, so another clinician's slot and
                        // another day's slot are NOT IN THE RESULT AT ALL and come back as SlotNotFound
                        // instead — measured behaviour, before and after the Dapper migration, documented
                        // on the enum members themselves. SlotWrongStaff is not even checked in code (the
                        // procedure does not project Staff_ID, so there is nothing to compare);
                        // SlotWrongDate is checked, because SlotDate IS projected. Both are mapped here
                        // anyway: "unreachable today" is not "impossible tomorrow", and the day that read
                        // stops being narrowed these arrive on the wire with no code change anywhere.
                        AppointmentSaveFailure.SlotWrongStaff =>
                            "One or more slotIds belong to a different staff member. All slots must belong "
                            + "to the staffId being booked.",
                        AppointmentSaveFailure.SlotWrongDate =>
                            "One or more slotIds are on a different date. All slots must be on "
                            + "appointmentDate.",

                        AppointmentSaveFailure.SlotTaken =>
                            "One or more slotIds were taken by another booking after they were read. "
                            + "This is expected: the open-slots read is advisory. Re-run "
                            + "GET /api/agent/slots/open and offer the patient another hour.",
                        AppointmentSaveFailure.SlotsNotConsecutive =>
                            "slotIds must be consecutive hours on the same day (for example 09:00-10:00 "
                            + "then 10:00-11:00). An appointment is one unbroken block.",

                        // InsertFailed, and anything a future enum member adds. The procedure SETs its
                        // identity from SCOPE_IDENTITY() immediately after the INSERT and has no path
                        // that skips it, so this is defensive — but it rolls back rather than assigning
                        // slots to appointment 0, and a caller has to be told something.
                        _ => "The appointment could not be created. Nothing was written."
                    };

                    // The refusal is audited too, with the reason — the transaction rolled back, so
                    // dbo.AuditTrails holds NOTHING about this attempt (the procedures write those rows
                    // inside the transaction) and this line is the only record that it happened.
                    AgentAuditLog.AgentAppointmentRefused(HttpContext, result.Reason.ToString(), patientId,
                        staffId, branchId, dateStr, slotIds);

                    return Ok(new
                    {
                        success = false,
                        reason = result.Reason.ToString(),
                        message
                    });
                }

                // 🔴 THE AUDIT LINE IS WRITTEN ONLY NOW — AFTER SaveAppointmentAsync HAS RETURNED Ok,
                // WHICH MEANS ITS TRANSACTION COMMITTED. Never inside a flow that might roll back
                // (§11.3, §6.6). The dbo.AuditTrails row for this booking was written the other way
                // round, by spPatientAppointment_Insert INSIDE the transaction, so a rollback would have
                // taken it with it — and its actor is the AGENT_SERVICE User_ID that AgentApiKeyFilter
                // resolved and put on HttpContext.User before this action ran (§13.3). That chain is the
                // one thing in this endpoint that fails SILENTLY if it is broken: the appointment is
                // created, the slot is consumed, this response says success, and the audit row names user
                // 0. The health check for it is the joined query in §0.1 / §13.5, and it is not optional.
                //
                // The times come back from the result because the data layer derived them inside the
                // transaction; everything else is the request, which is all a brand-new row could be
                // described by. The key is never logged, here or anywhere.
                AgentAuditLog.AgentAppointmentBooked(HttpContext, result.PatientAppointment_ID, patientId,
                    staffId, branchId, appointmentDate, slotIds, result.StartTime, result.EndTime,
                    pjAppTypeId, status);

                // The success shape is two properties and no more. appointmentId is the identity
                // spPatientAppointment_Insert generated inside the transaction (§3.9) — the caller stores
                // it, quotes it back to the patient, and hands it to a human if the booking has to be
                // undone, because POST /Patient/DeleteAppointment is the only way to release those hours.
                return Ok(new
                {
                    success = true,
                    appointmentId = result.PatientAppointment_ID
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex,
                    "SqlException booking an appointment for the Agent API. PatientId={PatientId} "
                    + "StaffId={StaffId} BranchId={BranchId} Date={Date}",
                    patientId, staffId, branchId, dateStr);
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error saving appointment."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error booking an appointment for the Agent API. PatientId={PatientId} "
                    + "StaffId={StaffId} BranchId={BranchId} Date={Date}",
                    patientId, staffId, branchId, dateStr);
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error saving appointment."));
            }
        }
    }
}
