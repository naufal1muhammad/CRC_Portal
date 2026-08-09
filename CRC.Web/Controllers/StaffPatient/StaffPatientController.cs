using CRC.Data.Data;
using CRC.Data.Models;
using CRC.Web.Infrastructure;
using CRC.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CRC.Web.Controllers.StaffPatient
{
    // The STAFF workspace: one patient, their whole clinical journey, and their documents. See
    // CoreFlow.md §7 for what a journey IS and §4.9 for this controller's fifteen actions.
    //
    // 🔴 IT IS THE ONLY CONTROLLER IN NUCENTRA THAT MIXES POLICIES PER ACTION, AND THE SPLIT IS THE POINT:
    // every READ is AdminOrSuperOrStaff, so an administrator may look at a patient journey; the three
    // clinical WRITES are StaffOnly, so only a clinician may record one — which genuinely excludes the
    // SUPERUSER. `Details`, the page itself, is StaffOnly too. There is no class-level [Authorize] to fall
    // back on, and the global AuthorizeFilter (§2.2) is what keeps a missing attribute failing closed.
    // Do not "tidy" these onto the class.
    public class StaffPatientController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly IDocumentStorage _documentStorage;
        private readonly ILogger<StaffPatientController> _logger;

        public StaffPatientController(IDatabaseData data, IDocumentStorage documentStorage, ILogger<StaffPatientController> logger)
        {
            _data = data;
            _documentStorage = documentStorage;
            _logger = logger;
        }

        [Authorize(Policy = "StaffOnly")]
        [HttpGet]
        public IActionResult Details(string id)
        {
            ViewBag.PatientId = id; // used by JS
            return View();
        }

        // 🔴 THE CLINICIAN, NOT THE AUDIT ACTOR. This is dbo.Staff.Staff_ID, carried in the "StaffId" claim
        // that AccountController adds only for User_Type = 3, and it becomes PatientJourney.Staff_ID and
        // PatientJourneyAudit.Staff_ID. It is NOT the @User_ID of CoreFlow.md §0.1 — none of the twelve
        // journey procedures declares that parameter at all — and the two identities must never be crossed.
        private string? GetStaffId()
        {
            // IMPORTANT: use the claim you added during login
            return User.FindFirst("StaffId")?.Value;
        }

        // Formats a journey's business date for a <input type="datetime-local">.
        // PatientJourney_Date is DATETIME NOT NULL, so there is no null branch to reproduce.
        private static string ToLocalInput(DateTime value) => value.ToString("yyyy-MM-ddTHH:mm");

        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public IActionResult GetJourneyTemplate(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return BadRequest("Missing journey type.");

            var jt = type.Trim();

            if (jt.Equals("PATIENT ASSESSMENT", StringComparison.OrdinalIgnoreCase))
                return PartialView("~/Views/StaffPatient/Templates/_PatientAssessment.cshtml");

            if (jt.Equals("COLONOSCOPY", StringComparison.OrdinalIgnoreCase))
                return PartialView("~/Views/StaffPatient/Templates/_PatientColonoscopy.cshtml");

            if (jt.Equals("PATIENT FOLLOW UP", StringComparison.OrdinalIgnoreCase))
                return PartialView("~/Views/StaffPatient/Templates/_PatientFollowUp.cshtml");

            return BadRequest("Unsupported journey type.");
        }

        // GET: /StaffPatient/GetBasic?patientId=...
        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public async Task<IActionResult> GetBasic(string? patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
                return Ok(new { success = false, message = "Invalid patient." });

            try
            {
                // The SAME procedure /Patient/GetBasic reads (Prompt 5's GetPatientByIdAsync) — one method
                // per procedure, two callers. The JSON is NOT the same: this page is read-only, so it
                // projects the six lookup NAMES and not their ids, and it formats `age` as a STRING where
                // /Patient/GetBasic returns a number. Both shapes are live contracts; neither moves.
                var patientRow = await _data.GetPatientByIdAsync(patientId.Trim());

                if (patientRow == null)
                    return Ok(new { success = false, message = "Patient not found." });

                static string ToDateInputString(DateTime? value) =>
                    value.HasValue ? value.Value.ToString("yyyy-MM-dd") : ""; // for <input type="date">

                var patient = new
                {
                    patientId = patientRow.Patient_ID,
                    name = patientRow.Patient_Name,
                    email = patientRow.Patient_Email,
                    phone = patientRow.Patient_Phone,
                    nric = patientRow.Patient_NRIC,

                    // A STRING, deliberately: the DataTable code this replaced produced "" for a NULL age
                    // and the digits otherwise, and this page prints it straight into a read-only field.
                    age = patientRow.Patient_Age?.ToString() ?? "",
                    birthDate = ToDateInputString(patientRow.Patient_BirthDate),

                    // All six lookup names are LEFT JOINs onto tables nothing constrains the codes to, so
                    // every one can be null while its id is set — "" here, because DBNull.ToString() was ""
                    // and the page assigns these straight into the markup.
                    raceName = patientRow.Race_Name ?? "",
                    sourceName = patientRow.Source_Name ?? "",
                    gender = patientRow.Patient_Gender,
                    religionName = patientRow.Religion_Name ?? "",
                    maritalStatusName = patientRow.MaritalStatus_Name ?? "",
                    resState = patientRow.Patient_ResState,
                    resCity = patientRow.Patient_ResCity,
                    resPostcode = patientRow.Patient_ResPostcode,
                    addLine1 = patientRow.Patient_AddLine1,
                    addLine2 = patientRow.Patient_AddLine2 ?? "",
                    emergencyName = patientRow.Patient_EmergencyName,
                    emergencyRelationship = patientRow.Patient_EmergencyRelationship,
                    emergencyNumber = patientRow.Patient_EmergencyNumber,
                    occupationName = patientRow.Occupation_Name ?? "",

                    iFobtStatus = patientRow.Patient_iFOBTStatus,
                    iFobtCompletionDate = ToDateInputString(patientRow.Patient_iFOBTCompletionDate),
                    iFobtResults = patientRow.Patient_iFOBTResults,

                    dischargeTypeName = patientRow.DischargeType_Name ?? "",
                    dischargeDate = ToDateInputString(patientRow.Patient_DischargeDate),
                    dischargeRemarks = patientRow.Patient_DischargeRemarks ?? ""
                };

                return Ok(new { success = true, patient });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading patient details." });
            }
        }

        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public async Task<IActionResult> GetTimeline(string? patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
                return Ok(new { success = false, message = "Invalid patient." });

            try
            {
                patientId = patientId.Trim();

                // DB columns created by SYSUTCDATETIME() are UTC, but may come back as "Kind=Unspecified".
                // Force UTC and serialize as DateTimeOffset so JSON includes +00:00 (timezone-aware).
                static DateTimeOffset? Utc(DateTime? value)
                {
                    if (value == null) return null;

                    var utc = DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
                    return new DateTimeOffset(utc);
                }

                // 1) Timeline rows — ordered PatientJourney_Date ASC, PatientJourney_ID ASC by the
                //    procedure. That ordering IS the patient's clinical sequence: nucentra has no stage
                //    column to sort on (CoreFlow.md §7). Not re-sorted here.
                var timeline = await _data.GetJourneyTimelineAsync(patientId);

                // 2) Optional: full audit history (for UI display)
                Dictionary<int, List<object>> auditByJourney = new();
                try
                {
                    var audits = await _data.GetJourneyAuditsAsync(patientId);

                    auditByJourney = audits
                        .GroupBy(a => a.PatientJourney_ID)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(a => (object)new
                            {
                                action = a.Audit_Action,
                                at = Utc(a.Audit_At), // ✅ timezone-aware UTC
                                staffId = a.Staff_ID,
                                staffName = a.Staff_Name ?? "",
                                note = a.Audit_Note
                            }).ToList()
                        );
                }
                catch
                {
                    // If the SP isn't deployed yet, timeline still works.
                }

                var rows = timeline.Select(r => new
                {
                    patientJourneyId = r.PatientJourney_ID,
                    journeyType = r.PjAppType_Name,

                    // Journey date is a "business date" chosen by user/staff.
                    // Leave as DateTime and let UI render it (you already format it).
                    journeyDate = r.PatientJourney_Date,

                    // audit timestamps stored in UTC
                    createdAt = Utc(r.CreatedAt),
                    createdByStaffName = r.CreatedByStaffName ?? "",

                    updatedAt = Utc(r.UpdatedAt),
                    updatedByStaffName = r.UpdatedByStaffName ?? "",

                    auditEvents = auditByJourney.TryGetValue(r.PatientJourney_ID, out var ev)
                        ? ev
                        : new List<object>()
                }).ToList();

                return Ok(new { success = true, data = rows });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading timeline." });
            }
        }

        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public async Task<IActionResult> GetPatientAssessment(int patientJourneyId)
        {
            if (patientJourneyId <= 0)
                return Ok(new { success = false, message = "Invalid journey." });

            try
            {
                // 1) Journey row
                var journeyRow = await _data.GetJourneyByIdAsync(patientJourneyId);
                if (journeyRow == null)
                    return Ok(new { success = false, message = "Journey not found." });

                var journey = new
                {
                    patientJourneyId = patientJourneyId,
                    journeyType = journeyRow.PjAppType_Name,
                    journeyDateInput = ToLocalInput(journeyRow.PatientJourney_Date)
                };

                // 2) Assessment row — a COLUMN-KEYED DICTIONARY, held as `object?` so it serializes by its
                //    runtime type exactly as the DataTable version did. The browser receives the raw column
                //    names ("PatientJourney_ID", "iFOBTPositive_Date", "Risks_Smoking"), which is what
                //    wwwroot/js/staffPatient/templates/patientAssessment.js reads; a POCO would be
                //    camelCased by the serializer and break the form silently. See IDatabaseData.
                //
                //    Null is a real state, not an error: the procedure INNER JOINs dbo.PatientAssessment,
                //    so a journey of another type answers { success: true, assessment: null }.
                object? assessment = await _data.GetAssessmentByJourneyIdAsync(patientJourneyId);

                return Ok(new { success = true, journey, assessment });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading assessment." });
            }
        }

        public class SavePatientAssessmentRequest
        {
            public int PatientJourneyId { get; set; } // 0 = create
            public string PatientId { get; set; } = "";
            public DateTime PatientJourneyDate { get; set; } // journey date chosen by staff
            public string? AuditNote { get; set; }

            public DateTime iFOBTPositive_Date { get; set; }
            public bool Risks_Smoking { get; set; }
            public bool Risks_AlcoholConsumption { get; set; }
            public bool Risks_InflammatoryBowelDisease { get; set; }
            public bool Risks_Diet { get; set; }
            public string Risks_SedentaryLifestyle { get; set; } = "";

            public bool Symptoms_WeightLoss { get; set; }
            public bool Symptoms_AppetiteLoss { get; set; }
            public bool Symptoms_Lethargic { get; set; }
            public bool Symptoms_AbdominalPain { get; set; }
            public bool Symptoms_Constipation { get; set; }
            public bool Symptoms_Diarrhea { get; set; }
            public bool Symptoms_RectalBleedingMucous { get; set; }
            public bool Symptoms_RectalBleedingNoMucous { get; set; }
            public bool Symptoms_Tenesmus { get; set; }

            public bool MedicalHistory_Diabetes { get; set; }
            public bool MedicalHistory_Hypertension { get; set; }
            public bool MedicalHistory_Dyslipidemia { get; set; }
            public bool MedicalHistory_Bleeding { get; set; }
            public bool MedicalHistory_Asthma { get; set; }

            public bool AllergyHistory_Medication { get; set; }
            public string? AllergyHistory_MedicationDetails { get; set; }
            public bool AllergyHistory_Food { get; set; }
            public string? AllergyHistory_FoodDetails { get; set; }

            public bool MedicationHistory_Anticoagulant { get; set; }
            public string? MedicationHistory_AnticoagulantDetails { get; set; }
            public bool MedicationHistory_Narcotics { get; set; }
            public string? MedicationHistory_NarcoticsDetails { get; set; }
            public bool MedicationHistory_Insulin { get; set; }
            public string? MedicationHistory_InsulinDetails { get; set; }
            public bool MedicationHistory_AntiHypertensives { get; set; }
            public string? MedicationHistory_AntiHypertensivesDetails { get; set; }

            public DateTime? PreviousScope_Date { get; set; }

            public bool FamilyHistory_FirstDegree { get; set; }
            public bool FamilyHistory_SecondDegree { get; set; }

            public string PhysicalExamination_Details { get; set; } = "";

            public bool Investigation_FBC { get; set; }
            public bool Investigation_BUSE { get; set; }
            public bool Investigation_RBS { get; set; }
            public bool Investigation_LFT { get; set; }
            public bool Investigation_Coag { get; set; }

            public bool Management_BowelPrep { get; set; }
            public bool Management_Procedure { get; set; }
            public bool Management_Consent { get; set; }
            public bool Management_Advise { get; set; }
        }

        // Maps the request onto the data layer's input model. Both the create and the update path send the
        // same object; SqlData adds @Patient_ID or @PatientJourney_ID depending on which procedure it is
        // calling, because the two declare one different parameter each.
        //
        // 🔴 Staff_ID here is the CLINICIAN from the caller's "StaffId" claim, an ordinary business value.
        // It is not, and must never become, DatabaseHelper.CurrentUserId.
        private static PatientAssessmentSaveInput ToAssessmentInput(SavePatientAssessmentRequest model, string staffId) =>
            new()
            {
                Patient_ID = model.PatientId.Trim(),
                PatientJourney_ID = model.PatientJourneyId,
                PatientJourney_Date = model.PatientJourneyDate,
                Staff_ID = staffId,
                Audit_Note = model.AuditNote,

                iFOBTPositive_Date = model.iFOBTPositive_Date,
                Risks_Smoking = model.Risks_Smoking,
                Risks_AlcoholConsumption = model.Risks_AlcoholConsumption,
                Risks_InflammatoryBowelDisease = model.Risks_InflammatoryBowelDisease,
                Risks_Diet = model.Risks_Diet,
                Risks_SedentaryLifestyle = model.Risks_SedentaryLifestyle,

                Symptoms_WeightLoss = model.Symptoms_WeightLoss,
                Symptoms_AppetiteLoss = model.Symptoms_AppetiteLoss,
                Symptoms_Lethargic = model.Symptoms_Lethargic,
                Symptoms_AbdominalPain = model.Symptoms_AbdominalPain,
                Symptoms_Constipation = model.Symptoms_Constipation,
                Symptoms_Diarrhea = model.Symptoms_Diarrhea,
                Symptoms_RectalBleedingMucous = model.Symptoms_RectalBleedingMucous,
                Symptoms_RectalBleedingNoMucous = model.Symptoms_RectalBleedingNoMucous,
                Symptoms_Tenesmus = model.Symptoms_Tenesmus,

                MedicalHistory_Diabetes = model.MedicalHistory_Diabetes,
                MedicalHistory_Hypertension = model.MedicalHistory_Hypertension,
                MedicalHistory_Dyslipidemia = model.MedicalHistory_Dyslipidemia,
                MedicalHistory_Bleeding = model.MedicalHistory_Bleeding,
                MedicalHistory_Asthma = model.MedicalHistory_Asthma,

                AllergyHistory_Medication = model.AllergyHistory_Medication,
                AllergyHistory_MedicationDetails = model.AllergyHistory_MedicationDetails,
                AllergyHistory_Food = model.AllergyHistory_Food,
                AllergyHistory_FoodDetails = model.AllergyHistory_FoodDetails,

                MedicationHistory_Anticoagulant = model.MedicationHistory_Anticoagulant,
                MedicationHistory_AnticoagulantDetails = model.MedicationHistory_AnticoagulantDetails,
                MedicationHistory_Narcotics = model.MedicationHistory_Narcotics,
                MedicationHistory_NarcoticsDetails = model.MedicationHistory_NarcoticsDetails,
                MedicationHistory_Insulin = model.MedicationHistory_Insulin,
                MedicationHistory_InsulinDetails = model.MedicationHistory_InsulinDetails,
                MedicationHistory_AntiHypertensives = model.MedicationHistory_AntiHypertensives,
                MedicationHistory_AntiHypertensivesDetails = model.MedicationHistory_AntiHypertensivesDetails,

                PreviousScope_Date = model.PreviousScope_Date,

                FamilyHistory_FirstDegree = model.FamilyHistory_FirstDegree,
                FamilyHistory_SecondDegree = model.FamilyHistory_SecondDegree,

                PhysicalExamination_Details = model.PhysicalExamination_Details ?? "",

                Investigation_FBC = model.Investigation_FBC,
                Investigation_BUSE = model.Investigation_BUSE,
                Investigation_RBS = model.Investigation_RBS,
                Investigation_LFT = model.Investigation_LFT,
                Investigation_Coag = model.Investigation_Coag,

                Management_BowelPrep = model.Management_BowelPrep,
                Management_Procedure = model.Management_Procedure,
                Management_Consent = model.Management_Consent,
                Management_Advise = model.Management_Advise
            };

        [Authorize(Policy = "StaffOnly")]
        [HttpPost]
        public async Task<IActionResult> SavePatientAssessment([FromBody] SavePatientAssessmentRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.PatientId))
                return BadRequest(new { success = false, message = "Invalid request." });

            var staffId = GetStaffId();
            if (string.IsNullOrWhiteSpace(staffId))
                return Ok(new { success = false, message = "Your account is not linked to a Staff_ID." });

            try
            {
                var input = ToAssessmentInput(model, staffId);

                if (model.PatientJourneyId <= 0)
                {
                    // CREATE — one procedure, three tables (PatientJourney, PatientAssessment,
                    // PatientJourneyAudit), inside ITS OWN transaction. No transaction is opened here.
                    var newJourneyId = await _data.CreateAssessmentWithJourneyAsync(input);

                    return Ok(new { success = true, patientJourneyId = newJourneyId });
                }
                else
                {
                    // UPDATE — the same three tables, and 🔴 NO SECOND JOURNEY ROW: the procedure UPDATEs
                    // dbo.PatientJourney rather than inserting, which is what keeps a re-saved assessment
                    // from appearing twice on the timeline.
                    await _data.UpdateAssessmentWithJourneyAsync(input);

                    return Ok(new { success = true, patientJourneyId = model.PatientJourneyId });
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException saving patient assessment PatientJourneyId={PatientJourneyId}", model.PatientJourneyId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving assessment."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving patient assessment PatientJourneyId={PatientJourneyId}", model.PatientJourneyId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving assessment."));
            }
        }

        // ------------------------------
        // COLONOSCOPY
        // ------------------------------

        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public async Task<IActionResult> GetPatientColonoscopy(int patientJourneyId)
        {
            if (patientJourneyId <= 0)
                return Ok(new { success = false, message = "Invalid journey." });

            try
            {
                var journeyRow = await _data.GetJourneyByIdAsync(patientJourneyId);
                if (journeyRow == null)
                    return Ok(new { success = false, message = "Journey not found." });

                var journey = new
                {
                    patientJourneyId = patientJourneyId,
                    journeyType = journeyRow.PjAppType_Name,
                    journeyDateInput = ToLocalInput(journeyRow.PatientJourney_Date)
                };

                // The JSON property is called `assessment` on all three endpoints, colonoscopy included.
                // That is what the three template scripts read; it is not a copy-paste slip to fix.
                object? assessment = await _data.GetColonoscopyByJourneyIdAsync(patientJourneyId);

                return Ok(new { success = true, journey, assessment });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading colonoscopy." });
            }
        }

        public class SavePatientColonoscopyRequest
        {
            public int PatientJourneyId { get; set; }
            public string PatientId { get; set; } = "";
            public DateTime PatientJourneyDate { get; set; }
            public string? AuditNote { get; set; }

            public bool ColonoscopyStatus { get; set; }
            public string? ColonoscopyStatus_Details { get; set; }
            public int BowelPreparation { get; set; }

            public bool Findings_Anus { get; set; }
            public string? Findings_AnusDetails { get; set; }
            public bool Findings_Rectum { get; set; }
            public string? Findings_RectumDetails { get; set; }
            public bool Findings_SigmoidColon { get; set; }
            public string? Findings_SigmoidColonDetails { get; set; }
            public bool Findings_DescendingColon { get; set; }
            public string? Findings_DescendingColonDetails { get; set; }
            public bool Findings_SplenicFlexure { get; set; }
            public string? Findings_SplenicFlexureDetails { get; set; }
            public bool Findings_TransverseColon { get; set; }
            public string? Findings_TransverseColonDetails { get; set; }
            public bool Findings_HepaticFlexure { get; set; }
            public string? Findings_HepaticFlexureDetails { get; set; }
            public bool Findings_AscendingColon { get; set; }
            public string? Findings_AscendingColonDetails { get; set; }
            public bool Findings_Caecum { get; set; }
            public string? Findings_CaecumDetails { get; set; }

            public bool HPE_Status { get; set; }
            public string? HPE_Details { get; set; }

            public string Complications { get; set; } = "";
            public string? Complications_Details { get; set; }

            public string DischargePlan { get; set; } = "";

            // NEW: Medication details stored as JSON array
            public string? Medication_Details { get; set; }
        }

        private static PatientColonoscopySaveInput ToColonoscopyInput(SavePatientColonoscopyRequest model, string staffId) =>
            new()
            {
                Patient_ID = model.PatientId.Trim(),
                PatientJourney_ID = model.PatientJourneyId,
                PatientJourney_Date = model.PatientJourneyDate,
                Staff_ID = staffId,
                Audit_Note = model.AuditNote,

                ColonoscopyStatus = model.ColonoscopyStatus,
                ColonoscopyStatus_Details = model.ColonoscopyStatus_Details,
                BowelPreparation = model.BowelPreparation,

                Findings_Anus = model.Findings_Anus,
                Findings_AnusDetails = model.Findings_AnusDetails,
                Findings_Rectum = model.Findings_Rectum,
                Findings_RectumDetails = model.Findings_RectumDetails,
                Findings_SigmoidColon = model.Findings_SigmoidColon,
                Findings_SigmoidColonDetails = model.Findings_SigmoidColonDetails,
                Findings_DescendingColon = model.Findings_DescendingColon,
                Findings_DescendingColonDetails = model.Findings_DescendingColonDetails,
                Findings_SplenicFlexure = model.Findings_SplenicFlexure,
                Findings_SplenicFlexureDetails = model.Findings_SplenicFlexureDetails,
                Findings_TransverseColon = model.Findings_TransverseColon,
                Findings_TransverseColonDetails = model.Findings_TransverseColonDetails,
                Findings_HepaticFlexure = model.Findings_HepaticFlexure,
                Findings_HepaticFlexureDetails = model.Findings_HepaticFlexureDetails,
                Findings_AscendingColon = model.Findings_AscendingColon,
                Findings_AscendingColonDetails = model.Findings_AscendingColonDetails,
                Findings_Caecum = model.Findings_Caecum,
                Findings_CaecumDetails = model.Findings_CaecumDetails,

                HPE_Status = model.HPE_Status,
                HPE_Details = model.HPE_Details,

                Complications = model.Complications ?? "",
                Complications_Details = model.Complications_Details,

                DischargePlan = model.DischargePlan ?? "",

                Medication_Details = model.Medication_Details
            };

        [Authorize(Policy = "StaffOnly")]
        [HttpPost]
        public async Task<IActionResult> SavePatientColonoscopy([FromBody] SavePatientColonoscopyRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.PatientId))
                return BadRequest(new { success = false, message = "Invalid request." });

            var staffId = GetStaffId();
            if (string.IsNullOrWhiteSpace(staffId))
                return Ok(new { success = false, message = "Your account is not linked to a Staff_ID." });

            try
            {
                var input = ToColonoscopyInput(model, staffId);

                if (model.PatientJourneyId <= 0)
                {
                    // CREATE
                    var newJourneyId = await _data.CreateColonoscopyWithJourneyAsync(input);

                    return Ok(new { success = true, patientJourneyId = newJourneyId });
                }
                else
                {
                    // UPDATE
                    await _data.UpdateColonoscopyWithJourneyAsync(input);
                    return Ok(new { success = true, patientJourneyId = model.PatientJourneyId });
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException saving colonoscopy PatientJourneyId={PatientJourneyId}", model.PatientJourneyId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving colonoscopy."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving colonoscopy PatientJourneyId={PatientJourneyId}", model.PatientJourneyId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving colonoscopy."));
            }
        }

        // ------------------------------
        // PATIENT FOLLOW UP
        // ------------------------------

        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public async Task<IActionResult> GetPatientFollowUp(int patientJourneyId)
        {
            if (patientJourneyId <= 0)
                return Ok(new { success = false, message = "Invalid journey." });

            try
            {
                // 1) Journey row
                var journeyRow = await _data.GetJourneyByIdAsync(patientJourneyId);
                if (journeyRow == null)
                    return Ok(new { success = false, message = "Journey not found." });

                var journey = new
                {
                    patientJourneyId = patientJourneyId,
                    journeyType = journeyRow.PjAppType_Name,
                    journeyDateInput = ToLocalInput(journeyRow.PatientJourney_Date)
                };

                // 2) Follow up row
                object? assessment = await _data.GetFollowUpByJourneyIdAsync(patientJourneyId);

                return Ok(new { success = true, journey, assessment });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading follow up." });
            }
        }

        public class SavePatientFollowUpRequest
        {
            public int PatientJourneyId { get; set; } // 0 = create
            public string PatientId { get; set; } = "";
            public DateTime PatientJourneyDate { get; set; }
            public string? AuditNote { get; set; }

            public string HPE_Results { get; set; } = "";
            public string DischargePlan { get; set; } = "";
            public bool DischargeSummary_Status { get; set; }
        }

        private static PatientFollowUpSaveInput ToFollowUpInput(SavePatientFollowUpRequest model, string staffId) =>
            new()
            {
                Patient_ID = model.PatientId.Trim(),
                PatientJourney_ID = model.PatientJourneyId,
                PatientJourney_Date = model.PatientJourneyDate,
                Staff_ID = staffId,
                Audit_Note = model.AuditNote,

                HPE_Results = model.HPE_Results ?? "",
                DischargePlan = model.DischargePlan ?? "",
                DischargeSummary_Status = model.DischargeSummary_Status
            };

        [Authorize(Policy = "StaffOnly")]
        [HttpPost]
        public async Task<IActionResult> SavePatientFollowUp([FromBody] SavePatientFollowUpRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.PatientId))
                return BadRequest(new { success = false, message = "Invalid request." });

            var staffId = GetStaffId();
            if (string.IsNullOrWhiteSpace(staffId))
                return Ok(new { success = false, message = "Your account is not linked to a Staff_ID." });

            try
            {
                var input = ToFollowUpInput(model, staffId);

                if (model.PatientJourneyId <= 0)
                {
                    // CREATE
                    var newJourneyId = await _data.CreateFollowUpWithJourneyAsync(input);

                    return Ok(new { success = true, patientJourneyId = newJourneyId });
                }
                else
                {
                    // UPDATE
                    await _data.UpdateFollowUpWithJourneyAsync(input);
                    return Ok(new { success = true, patientJourneyId = model.PatientJourneyId });
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException saving patient follow up PatientJourneyId={PatientJourneyId}", model.PatientJourneyId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving follow up."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving patient follow up PatientJourneyId={PatientJourneyId}", model.PatientJourneyId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving follow up."));
            }
        }

        //------------------------------------------------------
        //DOCUMENTS
        //------------------------------------------------------

        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public async Task<IActionResult> GetPatientDocumentTypes()
        {
            try
            {
                // The UPLOAD form's list — spLU_PatientDocumentType_List, the plain lookup. NOT
                // GetPatientDocumentTypeFiltersAsync, which unions in types that are in use but no longer
                // in the lookup: a search filter wants those, an upload form must not offer them.
                var types = await _data.GetPatientDocumentTypesAsync();

                var list = types
                    .Select(t => new
                    {
                        documentTypeId = t.Id,
                        documentTypeName = t.Name
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading patient document types." });
            }
        }

        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public async Task<IActionResult> GetPatientDocuments(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return Ok(new { success = true, data = Array.Empty<object>() });
            }

            try
            {
                var documents = await _data.GetPatientDocumentsAsync(patientId);

                // Metadata only — the blob key is deliberately NOT projected. The container is private, so a
                // storage key is useless to the browser and is exactly the kind of detail that should never
                // leave the server. The file itself is fetched through GetPatientDocumentUrl, which mints a
                // short-lived read SAS for one document at click time.
                var list = documents
                    .Select(d => new
                    {
                        documentId = d.PatientDocument_ID,
                        patientId = d.Patient_ID,
                        patientName = d.Patient_Name ?? string.Empty,
                        docTypeId = d.PatientDocumentType_ID ?? string.Empty,
                        docTypeName = d.PatientDocumentType_Name ?? string.Empty,
                        fileName = d.FileName,

                        // Already a formatted string in the column — VARCHAR(100), Malaysian local time
                        // with an offset. Nothing parses or re-formats it here, and nothing should.
                        uploadedOn = d.UploadedOn ?? string.Empty
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading patient documents." });
            }
        }

        // Uploads one or more files (multipart) into the PRIVATE blob container and records their metadata.
        // The request size limits are raised on purpose: several 20 MB documents have to fit inside a single
        // multipart body, and the ASP.NET Core default of roughly 30 MB would reject a two-file batch outright,
        // before any of the code below ever runs.
        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpPost]
        [RequestSizeLimit(120_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 120_000_000)]
        public async Task<IActionResult> UploadPatientDocuments(
    string patientId,
    string patientName,
    List<IFormFile> files,
    List<string> docTypeIds,
    List<string> docTypeNames)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return BadRequest(new { success = false, message = "Patient ID is required." });
            }

            if (files == null || files.Count == 0)
            {
                return Ok(new { success = false, message = "No files uploaded." });
            }

            // Validate the WHOLE batch first, in a pass of its own. A bad file has to fail BEFORE any blob is
            // written — otherwise the files that happened to be validated earlier are already in the container
            // when the request is rejected, and a refused upload leaves orphaned patient data behind.
            foreach (var candidate in files)
            {
                if (candidate is null)
                {
                    return Ok(new { success = false, message = "One of the selected files could not be read." });
                }

                var (ok, message) = DocumentValidation.Validate(candidate);
                if (!ok)
                {
                    return Ok(new { success = false, message });
                }
            }

            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];

                    var docTypeId = (docTypeIds != null && i < docTypeIds.Count)
                        ? docTypeIds[i]
                        : string.Empty;

                    var docTypeName = (docTypeNames != null && i < docTypeNames.Count)
                        ? docTypeNames[i]
                        : string.Empty;

                    var safeFileName = DocumentValidation.SafeFileName(file.FileName);
                    var contentType = file.ContentType ?? "application/octet-stream";

                    // Server-generated key: patients/{Patient_ID}/{guid}{ext}. Nothing the user typed becomes
                    // part of it, and the bytes are streamed straight to the container — never to disk.
                    var blobName = DocumentValidation.BuildBlobName("patients", patientId, file.FileName);

                    await using var stream = file.OpenReadStream();
                    await _documentStorage.UploadAsync(stream, blobName, contentType);

                    // The @User_ID actor for the dbo.AuditTrails row spPatientDocument_Insert writes is
                    // supplied by SqlData from the claim, exactly as DatabaseHelper used to inject it —
                    // it is not a business argument and does not appear here. See CoreFlow.md §0.1.
                    await _data.AddPatientDocumentAsync(new PatientDocumentInput
                    {
                        Patient_ID = patientId,
                        Patient_Name = patientName ?? string.Empty,
                        PatientDocumentType_ID = docTypeId,
                        PatientDocumentType_Name = docTypeName,
                        FileName = safeFileName,
                        BlobName = blobName,
                        ContentType = contentType
                    });

                    // DocumentId is 0 because spPatientDocument_Insert writes its own AuditTrails row but does
                    // not hand the new identity back to the caller; the blob key is what ties this audit line
                    // to the row it describes.
                    AuditLog.PatientDocumentUploaded(HttpContext, patientId, 0, docTypeId ?? string.Empty,
                        blobName, safeFileName, file.Length);
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading patient documents for PatientId={PatientId}", patientId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error uploading patient documents."));
            }
        }

        // Mints a short-lived read SAS URL for ONE document so the browser can fetch it straight from the
        // private container.
        //
        // The design, plainly: the container is private, so this five-minute URL is the ONLY way the browser
        // ever reaches the bytes. It is minted per click by this authenticated action, handed back once, never
        // persisted to the database and never rendered into the page's HTML. That is exactly what the old
        // static-file document links got wrong: static files are served ahead of authentication and get no
        // authorisation check at all, so every one of those links was public, and stayed public.
        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public async Task<IActionResult> GetPatientDocumentUrl(int id)
        {
            if (id <= 0)
            {
                return Ok(new { success = false, message = "Invalid document ID." });
            }

            try
            {
                var document = await _data.GetPatientDocumentByIdAsync(id);

                if (document == null)
                {
                    return Ok(new { success = false, message = "Document not found." });
                }

                var blobName = document.BlobName;
                var fileName = document.FileName;
                var patientId = document.Patient_ID;

                if (string.IsNullOrWhiteSpace(blobName))
                {
                    return Ok(new { success = false, message = "Document not found." });
                }

                var url = _documentStorage.GetReadSasUrl(blobName, TimeSpan.FromMinutes(5));

                // Minting a SAS for a patient record IS a read of patient data, so it belongs on the audit
                // channel — and it has to be written before the URL leaves the server, because the download
                // itself happens against storage where the application can no longer observe it.
                AuditLog.PatientDocumentDownloaded(HttpContext, patientId, id, fileName);

                return Ok(new { success = true, url = url.ToString(), fileName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating download URL for PatientDocumentId={PatientDocumentId}", id);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error opening document."));
            }
        }

        public class DeletePatientDocumentRequest
        {
            public int DocumentId { get; set; }
        }

        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpPost]
        public async Task<IActionResult> DeletePatientDocument([FromBody] DeletePatientDocumentRequest model)
        {
            if (model == null || model.DocumentId <= 0)
            {
                return Ok(new { success = false, message = "Invalid document ID." });
            }

            try
            {
                // Read the row first, only so the audit line can name the patient the document belonged to —
                // spPatientDocument_Delete hands back the blob key and nothing else.
                var document = await _data.GetPatientDocumentByIdAsync(model.DocumentId);

                var patientId = document?.Patient_ID ?? string.Empty;

                // NULL means no row was deleted, so there is nothing in storage to remove. The @User_ID
                // actor for the procedure's dbo.AuditTrails row is supplied by SqlData from the claim.
                var deletedBlobName = await _data.DeletePatientDocumentAsync(model.DocumentId);

                if (!string.IsNullOrWhiteSpace(deletedBlobName))
                {
                    try
                    {
                        await _documentStorage.DeleteAsync(deletedBlobName);
                    }
                    catch (Exception ex)
                    {
                        // Deliberately not fatal. The metadata row is already gone and the AuditTrails entry
                        // already stands, so failing the request here would only confuse the user about what
                        // happened: from their side the document HAS been deleted. What is actually left is an
                        // orphaned blob, which is an operational clean-up job — hence a warning in app-*.log.
                        _logger.LogWarning(ex, "Failed to delete blob {BlobName} for PatientDocumentId={PatientDocumentId}",
                            deletedBlobName, model.DocumentId);
                    }

                    AuditLog.PatientDocumentDeleted(HttpContext, patientId, model.DocumentId, deletedBlobName);
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting patient document PatientDocumentId={PatientDocumentId}", model.DocumentId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error deleting patient document."));
            }
        }
    }
}
