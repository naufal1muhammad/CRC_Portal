using CRC.Data.Data;
using CRC.Data.Models;
using CRC.Web.Infrastructure;
using CRC.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;

namespace CRC.Web.Controllers.Patient
{
    [Authorize(Policy = "AdminOrSuper")]
    public class PatientController : Controller
    {
        // 🔴 THIS CONTROLLER HOLDS BOTH DATA SURFACES ON PURPOSE, AND ONLY UNTIL PROMPT 6.
        //
        // PatientController is two screens wearing one class name: the PATIENT half (the Active and
        // Discharged lists, and the Basic Details + Discharge tabs of /Patient/Edit) and the APPOINTMENT
        // half (the Appointment tab). Prompt 5 migrated the patient half to the Dapper layer; the
        // appointment half — GetAppointmentLookups, GetAppointmentStaffList, GetAppointmentSlots,
        // GetAppointments, SaveAppointment, DeleteAppointment — is Prompt 6's work and still builds
        // SqlParameter[] arrays against DatabaseHelper, including the hand-rolled SqlTransaction in
        // SaveAppointment that becomes SaveAppointmentAsync, the second of the data layer's two
        // transactional units of work (CoreFlow.md §6.6).
        //
        // So `_data` is the destination and `_db` is what is left of the journey. PROMPT 6 REMOVES `_db`,
        // along with `using System.Data;` and `using Microsoft.Data.SqlClient;`, once nothing below needs
        // them. Do not add a new call to `_db` in the patient half to keep the two sides looking alike.
        private readonly IDatabaseData _data;
        private readonly DatabaseHelper _db;
        private readonly IDocumentStorage _documentStorage;
        private readonly ILogger<PatientController> _logger;

        public PatientController(IDatabaseData data, DatabaseHelper db, IDocumentStorage documentStorage, ILogger<PatientController> logger)
        {
            _data = data;
            _db = db;
            _documentStorage = documentStorage;
            _logger = logger;
        }
        //------------------------------------------------------
        //ACTIVE PATIENTS
        //------------------------------------------------------

        // GET: /Patient/Active
        [HttpGet]
        public IActionResult Active()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetActivePatients()
        {
            try
            {
                var patients = await _data.GetActivePatientsAsync();

                // The procedure also returns the three iFOBT columns; this list has never shown them, and
                // the JSON is a contract wwwroot/js/patient/active-list.js reads. Two properties only.
                var list = patients
                    .Select(p => new
                    {
                        patientId = p.Patient_ID,
                        name = p.Patient_Name
                    })
                    .ToList();

                return Ok(list);
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading active patients." });
            }
        }

        public class DeletePatientRequest
        {
            public string? PatientId { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> DeletePatient([FromBody] DeletePatientRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.PatientId))
            {
                return Ok(new { success = false, message = "Invalid patient ID." });
            }

            var patientId = model.PatientId.Trim();

            try
            {
                // spPatient_DeleteCascade captures the patient's document blob keys before it removes the
                // PatientDocument rows and returns them as its result set, so this reads the keys back
                // rather than firing and forgetting. Without deleting those blobs the documents would sit in
                // storage forever: that costs money, and — far worse — it retains patient data after the
                // patient record itself has been deleted.
                //
                // THE BLOB DELETION STAYS HERE, IN THE CONTROLLER. IDocumentStorage is a CRC.Web service and
                // CRC.Data has no reference to CRC.Web and must not gain one, so the data layer hands back
                // keys and this loop acts on them (CoreFlow.md §6.6).
                var result = await _data.DeletePatientCascadeAsync(patientId);

                var blobCount = 0;
                foreach (var blobName in result.BlobNames)
                {
                    try
                    {
                        await _documentStorage.DeleteAsync(blobName);
                        blobCount++;
                    }
                    catch (Exception ex)
                    {
                        // Best effort, per blob. The database rows are already gone and the patient is
                        // deleted, so a storage hiccup must not fail the request — it leaves an orphaned
                        // blob to clean up, which is an operational problem, not a user-facing one.
                        _logger.LogWarning(ex, "Failed to delete blob {BlobName} for deleted PatientId={PatientId}",
                            blobName, patientId);
                    }
                }

                AuditLog.PatientDocumentsPurged(HttpContext, patientId, blobCount);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting patient PatientId={PatientId}", patientId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error deleting patient."));
            }
        }

        //------------------------------------------------------
        //DISCHARGED PATIENTS
        //------------------------------------------------------

        // GET: /Patient/Discharged
        [HttpGet]
        public IActionResult Discharged()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDischargedPatients()
        {
            try
            {
                var patients = await _data.GetDischargedPatientsAsync();

                // dischargeDate is dd/MM/yyyy for display and "" when the column is null — NOT the
                // yyyy-MM-dd the rest of the portal uses, because this one is rendered straight into a
                // table cell rather than into an <input type="date">. wwwroot/js/patient/discharged-list.js
                // prints it verbatim, so both the format and the empty string are contract.
                var list = patients
                    .Select(p => new
                    {
                        patientId = p.Patient_ID,
                        name = p.Patient_Name,
                        dischargeDate = p.Patient_DischargeDate.HasValue
                            ? p.Patient_DischargeDate.Value.ToString("dd/MM/yyyy")
                            : ""
                    })
                    .ToList();

                return Ok(list);
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading discharged patients." });
            }
        }

        //------------------------------------------------------
        //BASIC DETAILS AND DISCHARGE
        //------------------------------------------------------

        // GET: /Patient/Edit/{id?}
        // id == null or empty => Add new patient
        // id has value        => Edit existing patient
        [HttpGet]
        public IActionResult Edit(string? id)
        {
            ViewData["PatientId"] = id ?? string.Empty;
            return View();
        }

        private static int CalculateAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }

        // yyyy-MM-dd for an <input type="date">, or null when there is no date. The DataTable version of
        // this took an `object` and had to defend against DBNull and against a value that would not parse;
        // Dapper hands over a DateTime? already typed, so the guard is the null check and nothing else.
        //
        // The format string is left WITHOUT an explicit culture, exactly as it was, so the output is
        // byte-identical to what this endpoint returned before. (It is not strictly culture-free: "yyyy"
        // renders the year of the CURRENT CULTURE'S CALENDAR, so a server running under a non-Gregorian
        // calendar would emit a year an <input type="date"> cannot read. Pinning it to InvariantCulture
        // would be the correct fix and is a behaviour change, however small — not this prompt's to make.)
        private static string? ToDateInputString(DateTime? value)
        {
            return value?.ToString("yyyy-MM-dd");
        }

        [HttpGet]
        public async Task<IActionResult> GetBasicLookups()
        {
            try
            {
                // Five of the fourteen spLU_* procedures, all wrapped in Prompt 1 and shared with the Staff
                // and Branch screens. Each returns {code, name} with the columns named after its own table
                // (Race_ID/Race_Name, Source_ID/Source_Name, …), which is why SqlData maps them by ORDINAL
                // onto LookupItem — see SqlData.QueryLookupAsync. Their ORDER BY is part of the contract;
                // this endpoint must not re-sort.
                var raceItems = await _data.GetRacesAsync();
                var sourceItems = await _data.GetSourcesAsync();
                var religionItems = await _data.GetReligionsAsync();
                var maritalItems = await _data.GetMaritalStatusesAsync();
                var occupationItems = await _data.GetOccupationsAsync();

                // The blank-id filter is kept on all five. It cannot fire — both columns of every LU_* code
                // table are NOT NULL — but it is what the endpoint has always done, and a dropdown with a
                // valueless option is a worse failure than a shorter list.
                var races = raceItems
                    .Select(x => new { id = x.Id, name = x.Name })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .ToList();

                var sources = sourceItems
                    .Select(x => new { id = x.Id, name = x.Name })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .ToList();

                var religions = religionItems
                    .Select(x => new { id = x.Id, name = x.Name })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .ToList();

                var maritalStatuses = maritalItems
                    .Select(x => new { id = x.Id, name = x.Name })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .ToList();

                var occupations = occupationItems
                    .Select(x => new { id = x.Id, name = x.Name })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .ToList();

                return Ok(new
                {
                    success = true,
                    races,
                    sources,
                    religions,
                    maritalStatuses,
                    occupations
                });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading lookups." });
            }
        }

        // Location lookups (for Basic Details -> Residential)
        [HttpGet]
        public async Task<IActionResult> GetStates()
        {
            try
            {
                // `id` is a JSON NUMBER here, because LU_LOCATION is the one reference table keyed on an
                // INT (CoreFlow.md §3.1) and edit-basic.js sends it straight back as the stateId query
                // parameter. /Staff/GetStaffLookups serializes the same column as a STRING; both shapes are
                // live and this migration changes neither.
                var states = await _data.GetStatesAsync();

                var list = states
                    .Select(x => new { id = x.LocationId, name = x.Name })
                    .Where(x => x.id > 0 && !string.IsNullOrWhiteSpace(x.name))
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading states." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCitiesByState(int stateId)
        {
            if (stateId <= 0)
                return Ok(new { success = true, data = Array.Empty<object>() });

            try
            {
                // An unknown stateId is not an error: the procedure returns an empty set and the page shows
                // an empty City dropdown.
                var cities = await _data.GetCitiesByStateAsync(stateId);

                var list = cities
                    .Select(x => new { id = x.LocationId, name = x.Name })
                    .Where(x => x.id > 0 && !string.IsNullOrWhiteSpace(x.name))
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading cities." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPostcodesByCity(int cityId)
        {
            if (cityId <= 0)
                return Ok(new { success = true, data = Array.Empty<object>() });

            try
            {
                // A postcode's Name IS the postcode ("82100"), which is why it is text and the id beside it
                // is the number.
                var postcodes = await _data.GetPostcodesByCityAsync(cityId);

                var list = postcodes
                    .Select(x => new { id = x.LocationId, name = x.Name })
                    .Where(x => x.id > 0 && !string.IsNullOrWhiteSpace(x.name))
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading postcodes." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBasic(string? patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                // New patient – nothing yet
                return Ok(new { success = true, patient = (object?)null });
            }

            try
            {
                var row = await _data.GetPatientByIdAsync(patientId.Trim());

                if (row == null)
                {
                    return Ok(new { success = false, message = "Patient not found." });
                }

                // 🔴 THE THREE COERCIONS BELOW ARE THE CONTRACT, NOT TIDINESS, and the difference between
                // them is what edit-basic.js reads:
                //   • addLine2 and dischargeRemarks become "" when the column is NULL, because the DataTable
                //     code produced "" (DBNull.ToString() is "") and the form assigns them to input values
                //     — a JSON null would put the word "null" in the box via `p.addLine2 || ''`… which is
                //     actually safe, but dischargeRemarks is read the same way and neither is worth risking.
                //   • dischargeTypeId and dischargeTypeName stay NULL, and that null is load-bearing:
                //     edit-basic.js computes `hasDischarge = !!(dischargeTypeId || dischargeTypeName)` to
                //     decide whether to open the Discharge tab in its discharged state. Coercing those two
                //     to "" would change nothing today (both are falsy) but would erase the one signal that
                //     distinguishes an active patient from a discharged one in this payload.
                //   • age falls back to 0, and the three date fields to null, exactly as before.
                var patient = new
                {
                    patientId = row.Patient_ID,
                    name = row.Patient_Name,
                    email = row.Patient_Email,
                    phone = row.Patient_Phone,
                    nric = row.Patient_NRIC,

                    birthDate = ToDateInputString(row.Patient_BirthDate),
                    age = row.Patient_Age ?? 0,
                    gender = row.Patient_Gender,

                    raceId = row.Race_ID,
                    sourceId = row.Source_ID,
                    religionId = row.Religion_ID,
                    maritalStatusId = row.MaritalStatus_ID,
                    occupationId = row.Occupation_ID,

                    resState = row.Patient_ResState,
                    resCity = row.Patient_ResCity,
                    resPostcode = row.Patient_ResPostcode,
                    addLine1 = row.Patient_AddLine1,
                    addLine2 = row.Patient_AddLine2 ?? "",

                    emergencyName = row.Patient_EmergencyName,
                    emergencyRelationship = row.Patient_EmergencyRelationship,
                    emergencyNumber = row.Patient_EmergencyNumber,

                    iFobtStatus = row.Patient_iFOBTStatus,
                    iFobtCompletionDate = ToDateInputString(row.Patient_iFOBTCompletionDate),
                    iFobtResults = row.Patient_iFOBTResults,

                    dischargeTypeId = row.DischargeType_ID,
                    dischargeTypeName = row.DischargeType_Name,
                    dischargeDate = ToDateInputString(row.Patient_DischargeDate),
                    dischargeRemarks = row.Patient_DischargeRemarks ?? ""
                };

                return Ok(new { success = true, patient });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading patient details." });
            }
        }

        // DTO for saving basic details (Basic Details + Discharge tab)
        public class SaveBasicRequest
        {
            public string? PatientId { get; set; }

            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string NRIC { get; set; } = string.Empty;

            public string RaceId { get; set; } = string.Empty;
            public string SourceId { get; set; } = string.Empty;
            public string ReligionId { get; set; } = string.Empty;
            public string MaritalStatusId { get; set; } = string.Empty;
            public string OccupationId { get; set; } = string.Empty;

            public string ResState { get; set; } = string.Empty;       // store Name
            public string ResCity { get; set; } = string.Empty;        // store Name
            public string ResPostcode { get; set; } = string.Empty;    // store Name
            public string AddLine1 { get; set; } = string.Empty;
            public string AddLine2 { get; set; } = string.Empty;

            public string EmergencyName { get; set; } = string.Empty;
            public string EmergencyRelationship { get; set; } = string.Empty;
            public string EmergencyNumber { get; set; } = string.Empty;

            // Additional (iFOBT)
            public bool? IFOBTStatus { get; set; }
            public string? IFOBTCompletionDate { get; set; } // yyyy-MM-dd
            public bool? IFOBTResults { get; set; }

            // Discharge
            public bool IsDischarged { get; set; }
            public string? DischargeTypeId { get; set; }          // LU_DISCHARGETYPE.DischargeType_ID
            public string DischargeDate { get; set; } = string.Empty; // yyyy-MM-dd
            public string DischargeRemarks { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetDischargeTypes()
        {
            try
            {
                // NORMAL / BENIGN POLYPS / PRECANCEROUS POLYPS / CANCER, ordered by name. Note this list is
                // NOT filtered on a blank id, unlike GetBasicLookups above — a difference with no practical
                // effect (both columns are NOT NULL) that is preserved because it is what the endpoint does.
                var dischargeTypes = await _data.GetDischargeTypesAsync();

                var list = dischargeTypes
                    .Select(x => new
                    {
                        dischargeTypeId = x.Id,
                        dischargeTypeName = x.Name
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading discharge types." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveBasic([FromBody] SaveBasicRequest model)
        {
            if (model == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            // ----------- BASIC FIELDS -----------
            string name = (model.Name ?? "").Trim();
            string email = (model.Email ?? "").Trim();
            string phone = (model.Phone ?? "").Trim();
            string nricRaw = (model.NRIC ?? "").Trim();

            string raceId = (model.RaceId ?? "").Trim();
            string sourceId = (model.SourceId ?? "").Trim();
            string religionId = (model.ReligionId ?? "").Trim();
            string maritalStatusId = (model.MaritalStatusId ?? "").Trim();
            string occupationId = (model.OccupationId ?? "").Trim();

            string resState = (model.ResState ?? "").Trim();
            string resCity = (model.ResCity ?? "").Trim();
            string resPostcode = (model.ResPostcode ?? "").Trim();
            string addLine1 = (model.AddLine1 ?? "").Trim();
            string addLine2 = (model.AddLine2 ?? "").Trim();

            string emergencyName = (model.EmergencyName ?? "").Trim();
            string emergencyRel = (model.EmergencyRelationship ?? "").Trim();
            string emergencyNum = (model.EmergencyNumber ?? "").Trim();

            // ----------- ADDITIONAL (iFOBT) -----------
            bool? ifobtStatus = model.IFOBTStatus;
            string ifobtCompletionStr = (model.IFOBTCompletionDate ?? "").Trim();
            DateTime? ifobtCompletionDate = null;
            if (!string.IsNullOrWhiteSpace(ifobtCompletionStr) && DateTime.TryParse(ifobtCompletionStr, out var parsedIfobt))
            {
                ifobtCompletionDate = parsedIfobt.Date;
            }
            bool? ifobtResults = model.IFOBTResults;

            // If status is not COMPLETE, force-clear completion fields
            if (ifobtStatus != true)
            {
                ifobtCompletionDate = null;
                ifobtResults = null;
            }

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(nricRaw) ||
                string.IsNullOrWhiteSpace(raceId) ||
                string.IsNullOrWhiteSpace(sourceId) ||
                string.IsNullOrWhiteSpace(religionId) ||
                string.IsNullOrWhiteSpace(maritalStatusId) ||
                string.IsNullOrWhiteSpace(occupationId) ||
                string.IsNullOrWhiteSpace(resState) ||
                string.IsNullOrWhiteSpace(resCity) ||
                string.IsNullOrWhiteSpace(resPostcode) ||
                string.IsNullOrWhiteSpace(addLine1) ||
                string.IsNullOrWhiteSpace(emergencyName) ||
                string.IsNullOrWhiteSpace(emergencyRel) ||
                string.IsNullOrWhiteSpace(emergencyNum))
            {
                return Ok(new { success = false, message = "Please fill in all mandatory fields." });
            }

            // NRIC: must be exactly 12 digits
            var nricDigits = new string(nricRaw.Where(char.IsDigit).ToArray());
            if (nricDigits.Length != 12)
            {
                return Ok(new { success = false, message = "NRIC must be exactly 12 digits." });
            }

            // Derive BirthDate (YYMMDD) and Gender (last digit)
            if (!TryDeriveBirthDateFromNric(nricDigits, out var birthDate))
            {
                return Ok(new { success = false, message = "Invalid NRIC (unable to derive Birth Date)." });
            }

            var gender = TryDeriveGenderFromNric(nricDigits);

            if (string.IsNullOrWhiteSpace(gender))
            {
                return Ok(new { success = false, message = "Invalid NRIC (unable to derive Gender)." });
            }

            int age = CalculateAge(birthDate);

            // ----------- DISCHARGE FIELDS -----------
            bool isDischarged = model.IsDischarged;
            string dischargeTypeId = (model.DischargeTypeId ?? "").Trim();
            string dischargeDateStr = (model.DischargeDate ?? "").Trim();
            string dischargeRemarks = (model.DischargeRemarks ?? "").Trim();

            DateTime? dischargeDate = null;

            if (isDischarged)
            {
                if (string.IsNullOrWhiteSpace(dischargeTypeId) || string.IsNullOrWhiteSpace(dischargeDateStr))
                {
                    return Ok(new { success = false, message = "Please fill in Discharge Date and Discharge Type." });
                }

                if (!DateTime.TryParse(dischargeDateStr, out var parsedDischarge))
                {
                    return Ok(new { success = false, message = "Invalid Discharge Date." });
                }

                dischargeDate = parsedDischarge;
            }

            try
            {
                bool isNew = string.IsNullOrWhiteSpace(model.PatientId);

                // ----------- MANDATORY DOC CHECK (only for existing, discharging patients) -----------
                if (isDischarged)
                {
                    if (isNew)
                    {
                        return Ok(new
                        {
                            success = false,
                            message = "Please save patient details first, upload required documents, then set Discharge."
                        });
                    }

                    // The procedure returns what is MISSING, so an EMPTY RESULT IS THE PASS CONDITION and
                    // any row at all blocks the save. Nothing is written when it does.
                    var missing = await _data.GetMissingDischargeDocumentsAsync(
                        model.PatientId!.Trim(), dischargeTypeId);

                    if (missing.Count > 0)
                    {
                        var missingNames = missing
                            .Select(m => m.PatientDocumentType_Name)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();

                        var msgMissing =
                            "Please upload the following mandatory documents before discharging this patient: " +
                            string.Join(", ", missingNames);

                        return Ok(new { success = false, message = msgMissing });
                    }
                }

                // ----------- THE VALUES, DERIVED AND VALIDATED ABOVE, IN ONE OBJECT -----------
                //
                // PatientSaveInput mirrors spPatientBasic_Insert's and spPatientBasic_Update's parameter
                // lists one-for-one, minus @User_ID (the audit ACTOR, which SqlData supplies) and minus
                // @NewPatient_ID (the insert's OUTPUT parameter, which is an answer rather than an
                // argument). It decides nothing: everything above this line — the mandatory-field check,
                // the twelve-digit NRIC rule, the birth date and gender derived FROM the NRIC, the age, the
                // upper-casing, the iFOBT clearing rule, the discharge-document check and every message —
                // stays here in the controller, exactly as it was.
                //
                // 🔴 THE NULLS ARE THE POINT. The DataTable code this replaces wrote
                // `(object?)x ?? DBNull.Value` at four of these assignments; a C# null on a Dapper
                // parameter object already sends SQL NULL, so the dance goes and the value stays. Each one
                // was checked individually and NOTHING in this flow is deliberately sent as "": the only
                // optional free-text field is AddLine2, which was DBNull.Value when blank and is null now
                // (and both procedures NULLIF a blank one regardless). Verified by round trip — see the
                // Prompt 5 write-up.
                var input = new PatientSaveInput
                {
                    Patient_ID = isNew ? string.Empty : model.PatientId!.Trim(),

                    Patient_Name = name.ToUpperInvariant(),
                    Patient_Email = email,
                    Patient_Phone = phone,
                    Patient_NRIC = nricDigits,
                    Patient_BirthDate = birthDate,
                    Patient_Age = age,
                    Race_ID = raceId,
                    Source_ID = sourceId,
                    Patient_Gender = gender,
                    Religion_ID = religionId,
                    MaritalStatus_ID = maritalStatusId,
                    Occupation_ID = occupationId,

                    Patient_ResState = resState,
                    Patient_ResCity = resCity,
                    Patient_ResPostcode = resPostcode,
                    Patient_AddLine1 = addLine1,
                    Patient_AddLine2 = string.IsNullOrWhiteSpace(addLine2) ? null : addLine2,

                    Patient_EmergencyName = emergencyName.ToUpperInvariant(),
                    Patient_EmergencyRelationship = emergencyRel,
                    Patient_EmergencyNumber = emergencyNum,

                    Patient_iFOBTStatus = ifobtStatus,
                    Patient_iFOBTCompletionDate = ifobtCompletionDate,
                    Patient_iFOBTResults = ifobtResults,

                    // Update path only — spPatientBasic_Insert takes no discharge parameters and hard-codes
                    // NULL into all three columns. Sent unconditionally on an update, including as nulls,
                    // because that procedure assigns all three columns on every call.
                    DischargeType_ID = isDischarged ? dischargeTypeId : null,
                    Patient_DischargeDate = isDischarged && dischargeDate.HasValue ? dischargeDate.Value : null,
                    Patient_DischargeRemarks = isDischarged
                        ? (string.IsNullOrWhiteSpace(dischargeRemarks) ? null : dischargeRemarks)
                        : null
                };

                // ----------- INSERT NEW PATIENT -----------
                if (isNew)
                {
                    // The id is generated inside spPatientBasic_Insert ('PAT-' + a six-digit sequence) and
                    // comes back through its OUTPUT parameter; the caller cannot predict it.
                    var newId = await _data.CreatePatientAsync(input);

                    return Ok(new { success = true, patientId = newId });
                }

                // ----------- UPDATE EXISTING PATIENT (including discharge info) -----------
                await _data.UpdatePatientAsync(input);

                return Ok(new { success = true, patientId = input.Patient_ID });
            }
            catch
            {
                return Ok(new { success = false, message = "An unexpected error occurred while saving patient details." });
            }
        }

        private static bool TryDeriveBirthDateFromNric(string nric12Digits, out DateTime birthDate)
        {
            birthDate = default;

            if (string.IsNullOrWhiteSpace(nric12Digits) || nric12Digits.Length != 12)
                return false;

            if (!int.TryParse(nric12Digits.Substring(0, 2), out var yy)) return false;
            if (!int.TryParse(nric12Digits.Substring(2, 2), out var mm)) return false;
            if (!int.TryParse(nric12Digits.Substring(4, 2), out var dd)) return false;

            var currentYY = DateTime.Today.Year % 100;
            var year = (yy <= currentYY) ? 2000 + yy : 1900 + yy;

            var dateStr = $"{year:D4}-{mm:D2}-{dd:D2}";
            return DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDate);
        }

        private static string TryDeriveGenderFromNric(string nric12Digits)
        {
            if (string.IsNullOrWhiteSpace(nric12Digits) || nric12Digits.Length != 12)
                return "";

            var lastChar = nric12Digits[^1];
            if (!char.IsDigit(lastChar)) return "";

            var lastDigit = (int)char.GetNumericValue(lastChar);
            return (lastDigit % 2 == 1) ? "MALE" : "FEMALE";
        }


        //------------------------------------------------------
        //APPOINTMENTS
        //------------------------------------------------------

        private class SlotInfo
        {
            public int StaffSlotId { get; set; }
            public string StaffId { get; set; } = string.Empty;
            public DateTime SlotDate { get; set; }
            public TimeSpan SlotStartTime { get; set; }
            public TimeSpan SlotEndTime { get; set; }
            public int? PatientAppointmentId { get; set; }
        }

        public class SaveAppointmentRequest
        {
            public int? AppointmentId { get; set; }  // insert or update
            public string PatientId { get; set; } = string.Empty;

            // yyyy-MM-dd
            public string AppointmentDate { get; set; } = string.Empty;

            public string StaffId { get; set; } = string.Empty;

            // Selected StaffSlot_ID(s)
            public int[] SlotIds { get; set; } = Array.Empty<int>();

            public string PjAppTypeId { get; set; } = string.Empty;
            public string BranchId { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }

        public class DeleteAppointmentRequest
        {
            public int AppointmentId { get; set; }
        }

        // GET: /Patient/GetAppointmentLookups
        [HttpGet]
        public async Task<IActionResult> GetAppointmentLookups()
        {
            try
            {
                var dtTypes = await _db.ExecuteDataTableAsync("spLU_PJ_AppType_List", Array.Empty<SqlParameter>());
                var dtBranches = await _db.ExecuteDataTableAsync("spBranch_ListActive", Array.Empty<SqlParameter>());

                var types = dtTypes.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        id = r["PjAppType_ID"]?.ToString() ?? "",
                        name = r["PjAppType_Name"]?.ToString() ?? ""
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .ToList();

                var branches = dtBranches.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        branchId = r["Branch_ID"]?.ToString() ?? "",
                        branchName = r["Branch_Name"]?.ToString() ?? "",
                        branchState = r["Branch_State"]?.ToString() ?? ""
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.branchId))
                    .ToList();

                var statuses = new[]
                {
                    "Scheduled",
                    "Attended",
                    "Not Attended"
                };

                return Ok(new
                {
                    success = true,
                    types,
                    branches,
                    statuses
                });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading appointment lookups." });
            }
        }

        // GET: /Patient/GetAppointmentStaffList
        [HttpGet]
        public async Task<IActionResult> GetAppointmentStaffList()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync("spStaff_List", Array.Empty<SqlParameter>());

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        staffId = r["Staff_ID"]?.ToString(),
                        staffName = r["Staff_Name"]?.ToString()
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading staff list." });
            }
        }

        // GET: /Patient/GetAppointmentSlots?staffId=...&date=yyyy-MM-dd&appointmentId=123
        [HttpGet]
        public async Task<IActionResult> GetAppointmentSlots(string staffId, string date, int? appointmentId)
        {
            staffId = staffId?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(staffId))
                return Ok(new { success = true, data = Array.Empty<object>() });

            if (!DateTime.TryParseExact(date ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var slotDate))
                return Ok(new { success = false, message = "Invalid date." });

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@Staff_ID", staffId),
                    new SqlParameter("@FromDate", SqlDbType.Date) { Value = slotDate.Date },
                    new SqlParameter("@ToDate",   SqlDbType.Date) { Value = slotDate.Date }
                };

                var dt = await _db.ExecuteDataTableAsync("spStaffSlots_List", parameters);

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        staffSlotId = r["StaffSlot_ID"] != DBNull.Value ? Convert.ToInt32(r["StaffSlot_ID"]) : 0,
                        slotDate = slotDate.ToString("yyyy-MM-dd"),
                        slotStartTime = r["SlotStartTime"]?.ToString() ?? "",
                        slotEndTime = r["SlotEndTime"]?.ToString() ?? "",
                        patientAppointmentId = r["PatientAppointment_ID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PatientAppointment_ID"])
                    })
                    .Where(x => x.staffSlotId > 0)
                    .ToList();

                return Ok(new { success = true, data = list, appointmentId });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading staff slots." });
            }
        }

        // GET: /Patient/GetAppointments?patientId=...
        [HttpGet]
        public async Task<IActionResult> GetAppointments(string patientId)
        {
            patientId = patientId?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(patientId))
                return Ok(new { success = true, data = Array.Empty<object>() });

            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientAppointment_ListByPatient",
                    new[] { new SqlParameter("@Patient_ID", patientId) });

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r =>
                    {
                        var dateVal = r["PatientAppointment_Date"] == DBNull.Value
                            ? (DateTime?)null
                            : Convert.ToDateTime(r["PatientAppointment_Date"]).Date;

                        var fromStr = r["PatientAppointment_StartTime"]?.ToString() ?? "";
                        var toStr = r["PatientAppointment_EndTime"]?.ToString() ?? "";

                        return new
                        {
                            appointmentId = r["PatientAppointment_ID"] != DBNull.Value
                                ? Convert.ToInt32(r["PatientAppointment_ID"])
                                : 0,

                            appointmentDate = dateVal.HasValue ? dateVal.Value.ToString("dd/MM/yyyy") : "",
                            appointmentDateRaw = dateVal.HasValue ? dateVal.Value.ToString("yyyy-MM-dd") : "",

                            from = fromStr,
                            to = toStr,

                            typeId = r["PjAppType_ID"]?.ToString() ?? "",
                            typeName = r["PjAppType_Name"]?.ToString() ?? "",

                            branchId = r["Branch_ID"]?.ToString() ?? "",
                            branchName = r["Branch_Name"]?.ToString() ?? "",

                            status = r["PatientAppointment_Status"]?.ToString() ?? "",

                            staffId = r["Staff_ID"]?.ToString() ?? "",
                            staffName = r["Staff_Name"]?.ToString() ?? ""
                        };
                    })
                    .Where(x => x.appointmentId > 0)
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading appointments." });
            }
        }

        // POST: /Patient/SaveAppointment
        [HttpPost]
        public async Task<IActionResult> SaveAppointment([FromBody] SaveAppointmentRequest model)
        {
            if (model == null)
                return BadRequest(new { success = false, message = "Invalid request." });

            var appointmentId = model.AppointmentId ?? 0;
            var patientId = model.PatientId?.Trim() ?? "";
            var staffId = model.StaffId?.Trim() ?? "";
            var pjAppTypeId = model.PjAppTypeId?.Trim() ?? "";
            var branchId = model.BranchId?.Trim() ?? "";
            var status = model.Status?.Trim() ?? "";
            var dateStr = model.AppointmentDate?.Trim() ?? "";

            var slotIds = (model.SlotIds ?? Array.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (string.IsNullOrWhiteSpace(patientId) ||
                string.IsNullOrWhiteSpace(staffId) ||
                string.IsNullOrWhiteSpace(pjAppTypeId) ||
                string.IsNullOrWhiteSpace(branchId) ||
                string.IsNullOrWhiteSpace(status) ||
                string.IsNullOrWhiteSpace(dateStr) ||
                slotIds.Count == 0)
            {
                return Ok(new { success = false, message = "Please fill in all mandatory appointment fields and select at least one slot." });
            }

            if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var apptDate))
            {
                return Ok(new { success = false, message = "Invalid appointment date." });
            }

            var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Scheduled", "Attended", "Not Attended"
            };

            if (!allowedStatuses.Contains(status))
            {
                return Ok(new { success = false, message = "Invalid attendance status." });
            }

            try
            {
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                using var tx = conn.BeginTransaction();

                var slots = new List<SlotInfo>();

                // Load staff slots through existing StaffSlots sproc and then keep selected IDs
                using (var cmdSlots = await _db.CreateStoredProcedureCommandAsync(
                    conn,
                    tx,
                    "spStaffSlots_List",
                    new[]
                    {
                        new SqlParameter("@Staff_ID", staffId),
                        new SqlParameter("@FromDate", SqlDbType.Date) { Value = apptDate.Date },
                        new SqlParameter("@ToDate", SqlDbType.Date) { Value = apptDate.Date }
                    }))
                {

                    using var reader = await cmdSlots.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var staffSlotId = reader.GetInt32(0);
                        if (!slotIds.Contains(staffSlotId))
                        {
                            continue;
                        }

                        slots.Add(new SlotInfo
                        {
                            StaffSlotId = staffSlotId,
                            StaffId = staffId,
                            SlotDate = reader.GetDateTime(1),
                            SlotStartTime = TimeSpan.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                            SlotEndTime = TimeSpan.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                            PatientAppointmentId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
                        });
                    }
                }

                if (slots.Count != slotIds.Count)
                {
                    tx.Rollback();
                    return Ok(new { success = false, message = "One or more selected slots are invalid. Please reload the slots and try again." });
                }

                // Validate: all slots belong to selected staff + date
                if (slots.Any(s => !string.Equals(s.StaffId, staffId, StringComparison.OrdinalIgnoreCase)))
                {
                    tx.Rollback();
                    return Ok(new { success = false, message = "Selected slots do not match the selected staff." });
                }

                if (slots.Any(s => s.SlotDate.Date != apptDate.Date))
                {
                    tx.Rollback();
                    return Ok(new { success = false, message = "Selected slots do not match the selected appointment date." });
                }

                // Validate availability (allow booked-by-this-appointment during edit)
                if (slots.Any(s => s.PatientAppointmentId.HasValue && s.PatientAppointmentId.Value != appointmentId))
                {
                    tx.Rollback();
                    return Ok(new { success = false, message = "One or more selected slots are no longer available. Please reload the slots and try again." });
                }

                // Validate consecutive 1-hour slots
                var sorted = slots.OrderBy(s => s.SlotStartTime).ToList();
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    var expected = sorted[i].SlotStartTime + TimeSpan.FromHours(1);
                    if (sorted[i + 1].SlotStartTime != expected)
                    {
                        tx.Rollback();
                        return Ok(new { success = false, message = "Please select consecutive slots (e.g. 08:00-09:00 then 09:00-10:00)." });
                    }
                }

                var startTime = sorted.First().SlotStartTime;
                var endTime = sorted.Last().SlotEndTime;

                int finalAppointmentId = appointmentId;
                bool isInsert = appointmentId <= 0;

                string persistedPatientId = patientId;
                string persistedStaffId = staffId;
                DateTime persistedDate = apptDate;
                TimeSpan persistedStart = startTime;
                TimeSpan persistedEnd = endTime;
                string persistedTypeId = pjAppTypeId;
                string persistedBranchId = branchId;
                string persistedStatus = status;

                if (isInsert)
                {
                    // INSERT
                    using var cmd = await _db.CreateStoredProcedureCommandAsync(
                        conn,
                        tx,
                        "spPatientAppointment_Insert",
                        new[]
                        {
                            new SqlParameter("@Patient_ID", patientId),
                            new SqlParameter("@PatientAppointment_Date", SqlDbType.Date) { Value = apptDate.Date },
                            new SqlParameter("@Staff_ID", staffId),
                            new SqlParameter("@PatientAppointment_StartTime", SqlDbType.Time) { Value = startTime },
                            new SqlParameter("@PatientAppointment_EndTime", SqlDbType.Time) { Value = endTime },
                            new SqlParameter("@PjAppType_ID", pjAppTypeId),
                            new SqlParameter("@Branch_ID", branchId),
                            new SqlParameter("@PatientAppointment_Status", status)
                        });

                    var outId = new SqlParameter("@NewPatientAppointment_ID", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(outId);

                    await cmd.ExecuteNonQueryAsync();

                    finalAppointmentId = outId.Value == DBNull.Value ? 0 : Convert.ToInt32(outId.Value);

                    if (finalAppointmentId <= 0)
                    {
                        tx.Rollback();
                        return Ok(new { success = false, message = "Failed to create appointment." });
                    }
                }
                else
                {
                    // UPDATE appointment
                    using var cmd = await _db.CreateStoredProcedureCommandAsync(
                        conn,
                        tx,
                        "spPatientAppointment_Update",
                        new[]
                        {
                            new SqlParameter("@PatientAppointment_ID", appointmentId),
                            new SqlParameter("@PatientAppointment_Date", SqlDbType.Date) { Value = apptDate.Date },
                            new SqlParameter("@Staff_ID", staffId),
                            new SqlParameter("@PatientAppointment_StartTime", SqlDbType.Time) { Value = startTime },
                            new SqlParameter("@PatientAppointment_EndTime", SqlDbType.Time) { Value = endTime },
                            new SqlParameter("@PjAppType_ID", pjAppTypeId),
                            new SqlParameter("@Branch_ID", branchId),
                            new SqlParameter("@PatientAppointment_Status", status)
                        });

                    var outPatientId = new SqlParameter("@Out_Patient_ID", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                    var outStaffId = new SqlParameter("@Out_Staff_ID", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                    var outDate = new SqlParameter("@Out_Date", SqlDbType.Date) { Direction = ParameterDirection.Output };
                    var outStartTime = new SqlParameter("@Out_StartTime", SqlDbType.Time) { Direction = ParameterDirection.Output };
                    var outEndTime = new SqlParameter("@Out_EndTime", SqlDbType.Time) { Direction = ParameterDirection.Output };
                    var outTypeId = new SqlParameter("@Out_PjAppType_ID", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                    var outBranchId = new SqlParameter("@Out_Branch_ID", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                    var outStatus = new SqlParameter("@Out_Status", SqlDbType.VarChar, 100) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(outPatientId);
                    cmd.Parameters.Add(outStaffId);
                    cmd.Parameters.Add(outDate);
                    cmd.Parameters.Add(outStartTime);
                    cmd.Parameters.Add(outEndTime);
                    cmd.Parameters.Add(outTypeId);
                    cmd.Parameters.Add(outBranchId);
                    cmd.Parameters.Add(outStatus);

                    await cmd.ExecuteNonQueryAsync();

                    if (outPatientId.Value is string pid) persistedPatientId = pid;
                    if (outStaffId.Value is string sid) persistedStaffId = sid;
                    if (outDate.Value is DateTime d) persistedDate = d;
                    if (outStartTime.Value is TimeSpan st) persistedStart = st;
                    if (outEndTime.Value is TimeSpan et) persistedEnd = et;
                    if (outTypeId.Value is string tid) persistedTypeId = tid;
                    if (outBranchId.Value is string bid) persistedBranchId = bid;
                    if (outStatus.Value is string ps) persistedStatus = ps;

                    // Release previous slots (if any)
                    using (var cmdClear = await _db.CreateStoredProcedureCommandAsync(
                        conn,
                        tx,
                        "spStaffSlots_ClearAppointment",
                        new[]
                        {
                            new SqlParameter("@ApptId", SqlDbType.Int) { Value = appointmentId }
                        }))
                    {
                        await cmdClear.ExecuteNonQueryAsync();
                    }
                }

                // Assign selected slots to the appointment
                using (var cmdAssign = await _db.CreateStoredProcedureCommandAsync(
                    conn,
                    tx,
                    "spStaffSlots_AssignAppointment",
                    new[]
                    {
                        new SqlParameter("@ApptId", SqlDbType.Int) { Value = finalAppointmentId },
                        new SqlParameter("@StaffSlotIds", SqlDbType.VarChar, -1) { Value = string.Join(",", slotIds) }
                    }))
                {
                    await cmdAssign.ExecuteNonQueryAsync();
                }
                tx.Commit();

                if (isInsert)
                {
                    AuditLog.AppointmentCreated(HttpContext, finalAppointmentId, patientId, staffId,
                        apptDate, startTime, endTime, pjAppTypeId, branchId, status);
                }
                else
                {
                    AuditLog.AppointmentUpdated(HttpContext, finalAppointmentId, persistedPatientId, persistedStaffId,
                        persistedDate, persistedStart, persistedEnd, persistedTypeId, persistedBranchId, persistedStatus);
                }

                return Ok(new { success = true, appointmentId = finalAppointmentId });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException saving appointment AppointmentId={AppointmentId} PatientId={PatientId}", appointmentId, patientId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving appointment."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving appointment AppointmentId={AppointmentId} PatientId={PatientId}", appointmentId, patientId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving appointment."));
            }
        }

        // POST: /Patient/DeleteAppointment
        [HttpPost]
        public async Task<IActionResult> DeleteAppointment([FromBody] DeleteAppointmentRequest model)
        {
            if (model == null || model.AppointmentId <= 0)
                return BadRequest(new { success = false, message = "Invalid request." });

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@PatientAppointment_ID", model.AppointmentId)
                };

                await _db.ExecuteNonQueryAsync("spPatientAppointment_Delete", parameters);

                AuditLog.AppointmentDeleted(HttpContext, model.AppointmentId);

                return Ok(new { success = true });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException deleting appointment AppointmentId={AppointmentId}", model.AppointmentId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error deleting appointment."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting appointment AppointmentId={AppointmentId}", model.AppointmentId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error deleting appointment."));
            }
        }

        //------------------------------------------------------
        //PATIENT JOURNEY: All Patient Journey endpoints are under StaffPatientController.cs
        //------------------------------------------------------

        //------------------------------------------------------
        //DOCUMENTS: All patient document endpoints are under StaffPatientController.cs
        //------------------------------------------------------
    }
}