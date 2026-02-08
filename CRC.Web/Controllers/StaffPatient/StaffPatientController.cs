using CRC.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRC.Web.Controllers.StaffPatient
{
    public class StaffPatientController : Controller
    {
        private readonly DatabaseHelper _db;

        public StaffPatientController(DatabaseHelper db)
        {
            _db = db;
        }

        [Authorize(Policy = "StaffOnly")]
        [HttpGet]
        public IActionResult Details(string id)
        {
            ViewBag.PatientId = id; // used by JS
            return View();
        }

        private string? GetStaffId()
        {
            // IMPORTANT: use the claim you added during login
            return User.FindFirst("StaffId")?.Value;
        }

        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public IActionResult GetJourneyTemplate(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return BadRequest("Missing journey type.");

            var jt = type.Trim();

            if (jt.Equals("PATIENT ASSESSMENT", StringComparison.OrdinalIgnoreCase))
                return PartialView("~/Views/StaffPatient/Templates/_PatientAssessment.cshtml");

            if (jt.Equals("PATIENT COLONOSCOPY", StringComparison.OrdinalIgnoreCase))
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
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientBasic_GetById",
                    new[] { new SqlParameter("@Patient_ID", patientId.Trim()) }
                );

                if (dt.Rows.Count == 0)
                    return Ok(new { success = false, message = "Patient not found." });

                var row = dt.Rows[0];

                string ToDateInputString(object value)
                {
                    if (value == null || value == DBNull.Value) return "";
                    var d = Convert.ToDateTime(value);
                    return d.ToString("yyyy-MM-dd"); // for <input type="date">
                }

                var patient = new
                {
                    patientId = row["Patient_ID"]?.ToString() ?? "",
                    name = row["Patient_Name"]?.ToString() ?? "",
                    email = row["Patient_Email"]?.ToString() ?? "",
                    phone = row["Patient_Phone"]?.ToString() ?? "",
                    nric = row["Patient_NRIC"]?.ToString() ?? "",
                    age = row["Patient_Age"] == DBNull.Value ? "" : row["Patient_Age"]?.ToString() ?? "",
                    birthDate = ToDateInputString(row["Patient_BirthDate"]),

                    raceName = row["Race_Name"]?.ToString() ?? "",
                    sourceName = row["Source_Name"]?.ToString() ?? "",
                    gender = row["Patient_Gender"]?.ToString() ?? "",
                    religionName = row["Religion_Name"]?.ToString() ?? "",
                    maritalStatusName = row["MaritalStatus_Name"]?.ToString() ?? "",
                    resState = row["Patient_ResState"]?.ToString() ?? "",
                    resCity = row["Patient_ResCity"]?.ToString() ?? "",
                    resPostcode = row["Patient_ResPostcode"]?.ToString() ?? "",
                    addLine1 = row["Patient_AddLine1"]?.ToString() ?? "",
                    addLine2 = row["Patient_AddLine2"]?.ToString() ?? "",
                    emergencyName = row["Patient_EmergencyName"]?.ToString() ?? "",
                    emergencyRelationship = row["Patient_EmergencyRelationship"]?.ToString() ?? "",
                    emergencyNumber = row["Patient_EmergencyNumber"]?.ToString() ?? "",
                    occupationName = row["Occupation_Name"]?.ToString() ?? ""
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

                // ----- helpers -----
                static DateTime? Dt(object v)
                {
                    if (v == null || v == DBNull.Value) return null;
                    return Convert.ToDateTime(v);
                }

                // DB columns created by SYSUTCDATETIME() are UTC, but may come back as "Kind=Unspecified".
                // Force UTC and serialize as DateTimeOffset so JSON includes +00:00 (timezone-aware).
                static DateTimeOffset? Utc(object v)
                {
                    var dt = Dt(v);
                    if (dt == null) return null;

                    var utc = DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc);
                    return new DateTimeOffset(utc);
                }

                // 1) Timeline rows
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientJourney_TimelineByPatient",
                    new[] { new SqlParameter("@Patient_ID", patientId) }
                );

                // 2) Optional: full audit history (for UI display)
                Dictionary<int, List<object>> auditByJourney = new();
                try
                {
                    var dtAudit = await _db.ExecuteDataTableAsync(
                        "spPatientJourney_AuditsByPatient",
                        new[] { new SqlParameter("@Patient_ID", patientId) }
                    );

                    auditByJourney = dtAudit.Rows.Cast<DataRow>()
                        .GroupBy(r => Convert.ToInt32(r["PatientJourney_ID"]))
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(r => (object)new
                            {
                                action = r["Audit_Action"]?.ToString() ?? "",
                                at = Utc(r["Audit_At"]), // ✅ timezone-aware UTC
                                staffId = r["Staff_ID"]?.ToString() ?? "",
                                staffName = r["Staff_Name"]?.ToString() ?? "",
                                note = r["Audit_Note"] == DBNull.Value ? null : r["Audit_Note"]?.ToString()
                            }).ToList()
                        );
                }
                catch
                {
                    // If the SP isn't deployed yet, timeline still works.
                }

                var rows = dt.Rows.Cast<DataRow>().Select(r =>
                {
                    var journeyId = Convert.ToInt32(r["PatientJourney_ID"]);

                    return new
                    {
                        patientJourneyId = journeyId,
                        journeyType = r["PjAppType_Name"]?.ToString() ?? "",

                        // Journey date is a "business date" chosen by user/staff.
                        // Leave as DateTime? and let UI render it (you already format it).
                        journeyDate = Dt(r["PatientJourney_Date"]),

                        // ✅ audit timestamps stored in UTC
                        createdAt = Utc(r["CreatedAt"]),
                        createdByStaffName = r["CreatedByStaffName"]?.ToString() ?? "",

                        updatedAt = Utc(r["UpdatedAt"]),
                        updatedByStaffName = r["UpdatedByStaffName"]?.ToString() ?? "",

                        auditEvents = auditByJourney.TryGetValue(journeyId, out var ev)
                            ? ev
                            : new List<object>()
                    };
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
                var dtJ = await _db.ExecuteDataTableAsync(
                    "spPatientJourney_GetById",
                    new[] { new SqlParameter("@PatientJourney_ID", patientJourneyId) }
                );
                if (dtJ.Rows.Count == 0)
                    return Ok(new { success = false, message = "Journey not found." });

                var j = dtJ.Rows[0];

                string ToLocalInput(object v)
                {
                    if (v == null || v == DBNull.Value) return "";
                    var d = Convert.ToDateTime(v);
                    return d.ToString("yyyy-MM-ddTHH:mm");
                }

                var journey = new
                {
                    patientJourneyId = patientJourneyId,
                    journeyType = j["PjAppType_Name"]?.ToString() ?? "",
                    journeyDateInput = ToLocalInput(j["PatientJourney_Date"])
                };

                // 2) Assessment row
                var dtA = await _db.ExecuteDataTableAsync(
                    "spPatientAssessment_GetByJourneyId",
                    new[] { new SqlParameter("@PatientJourney_ID", patientJourneyId) }
                );

                object? assessment = null;
                if (dtA.Rows.Count > 0)
                {
                    var a = dtA.Rows[0];
                    assessment = dtA.Columns.Cast<DataColumn>()
                        .ToDictionary(c => c.ColumnName, c => a[c] == DBNull.Value ? null : a[c]);
                }

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
                if (model.PatientJourneyId <= 0)
                {
                    // CREATE
                    var prms = new List<SqlParameter>
            {
                new SqlParameter("@Patient_ID", model.PatientId.Trim()),
                new SqlParameter("@PatientJourney_Date", model.PatientJourneyDate),
                new SqlParameter("@Staff_ID", staffId),
                new SqlParameter("@Audit_Note", (object?)model.AuditNote ?? DBNull.Value),

                new SqlParameter("@iFOBTPositive_Date", model.iFOBTPositive_Date),
                new SqlParameter("@Risks_Smoking", model.Risks_Smoking),
                new SqlParameter("@Risks_AlcoholConsumption", model.Risks_AlcoholConsumption),
                new SqlParameter("@Risks_InflammatoryBowelDisease", model.Risks_InflammatoryBowelDisease),
                new SqlParameter("@Risks_Diet", model.Risks_Diet),
                new SqlParameter("@Risks_SedentaryLifestyle", model.Risks_SedentaryLifestyle),

                new SqlParameter("@Symptoms_WeightLoss", model.Symptoms_WeightLoss),
                new SqlParameter("@Symptoms_AppetiteLoss", model.Symptoms_AppetiteLoss),
                new SqlParameter("@Symptoms_Lethargic", model.Symptoms_Lethargic),
                new SqlParameter("@Symptoms_AbdominalPain", model.Symptoms_AbdominalPain),
                new SqlParameter("@Symptoms_Constipation", model.Symptoms_Constipation),
                new SqlParameter("@Symptoms_Diarrhea", model.Symptoms_Diarrhea),
                new SqlParameter("@Symptoms_RectalBleedingMucous", model.Symptoms_RectalBleedingMucous),
                new SqlParameter("@Symptoms_RectalBleedingNoMucous", model.Symptoms_RectalBleedingNoMucous),
                new SqlParameter("@Symptoms_Tenesmus", model.Symptoms_Tenesmus),

                new SqlParameter("@MedicalHistory_Diabetes", model.MedicalHistory_Diabetes),
                new SqlParameter("@MedicalHistory_Hypertension", model.MedicalHistory_Hypertension),
                new SqlParameter("@MedicalHistory_Dyslipidemia", model.MedicalHistory_Dyslipidemia),
                new SqlParameter("@MedicalHistory_Bleeding", model.MedicalHistory_Bleeding),
                new SqlParameter("@MedicalHistory_Asthma", model.MedicalHistory_Asthma),

                new SqlParameter("@AllergyHistory_Medication", model.AllergyHistory_Medication),
                new SqlParameter("@AllergyHistory_MedicationDetails", (object?)model.AllergyHistory_MedicationDetails ?? DBNull.Value),
                new SqlParameter("@AllergyHistory_Food", model.AllergyHistory_Food),
                new SqlParameter("@AllergyHistory_FoodDetails", (object?)model.AllergyHistory_FoodDetails ?? DBNull.Value),

                new SqlParameter("@MedicationHistory_Anticoagulant", model.MedicationHistory_Anticoagulant),
                new SqlParameter("@MedicationHistory_AnticoagulantDetails", (object?)model.MedicationHistory_AnticoagulantDetails ?? DBNull.Value),
                new SqlParameter("@MedicationHistory_Narcotics", model.MedicationHistory_Narcotics),
                new SqlParameter("@MedicationHistory_NarcoticsDetails", (object?)model.MedicationHistory_NarcoticsDetails ?? DBNull.Value),
                new SqlParameter("@MedicationHistory_Insulin", model.MedicationHistory_Insulin),
                new SqlParameter("@MedicationHistory_InsulinDetails", (object?)model.MedicationHistory_InsulinDetails ?? DBNull.Value),
                new SqlParameter("@MedicationHistory_AntiHypertensives", model.MedicationHistory_AntiHypertensives),
                new SqlParameter("@MedicationHistory_AntiHypertensivesDetails", (object?)model.MedicationHistory_AntiHypertensivesDetails ?? DBNull.Value),

                new SqlParameter("@PreviousScope_Date", (object?)model.PreviousScope_Date ?? DBNull.Value),

                new SqlParameter("@FamilyHistory_FirstDegree", model.FamilyHistory_FirstDegree),
                new SqlParameter("@FamilyHistory_SecondDegree", model.FamilyHistory_SecondDegree),

                new SqlParameter("@PhysicalExamination_Details", model.PhysicalExamination_Details ?? ""),

                new SqlParameter("@Investigation_FBC", model.Investigation_FBC),
                new SqlParameter("@Investigation_BUSE", model.Investigation_BUSE),
                new SqlParameter("@Investigation_RBS", model.Investigation_RBS),
                new SqlParameter("@Investigation_LFT", model.Investigation_LFT),
                new SqlParameter("@Investigation_Coag", model.Investigation_Coag),

                new SqlParameter("@Management_BowelPrep", model.Management_BowelPrep),
                new SqlParameter("@Management_Procedure", model.Management_Procedure),
                new SqlParameter("@Management_Consent", model.Management_Consent),
                new SqlParameter("@Management_Advise", model.Management_Advise)
            };

                    var dt = await _db.ExecuteDataTableAsync("spPatientAssessment_CreateWithJourney", prms.ToArray());

                    var newJourneyId = dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["PatientJourney_ID"]) : 0;

                    return Ok(new { success = true, patientJourneyId = newJourneyId });
                }
                else
                {
                    // UPDATE
                    var prms = new List<SqlParameter>
            {
                new SqlParameter("@PatientJourney_ID", model.PatientJourneyId),
                new SqlParameter("@PatientJourney_Date", model.PatientJourneyDate),
                new SqlParameter("@Staff_ID", staffId),
                new SqlParameter("@Audit_Note", (object?)model.AuditNote ?? DBNull.Value),

                new SqlParameter("@iFOBTPositive_Date", model.iFOBTPositive_Date),
                new SqlParameter("@Risks_Smoking", model.Risks_Smoking),
                new SqlParameter("@Risks_AlcoholConsumption", model.Risks_AlcoholConsumption),
                new SqlParameter("@Risks_InflammatoryBowelDisease", model.Risks_InflammatoryBowelDisease),
                new SqlParameter("@Risks_Diet", model.Risks_Diet),
                new SqlParameter("@Risks_SedentaryLifestyle", model.Risks_SedentaryLifestyle),

                new SqlParameter("@Symptoms_WeightLoss", model.Symptoms_WeightLoss),
                new SqlParameter("@Symptoms_AppetiteLoss", model.Symptoms_AppetiteLoss),
                new SqlParameter("@Symptoms_Lethargic", model.Symptoms_Lethargic),
                new SqlParameter("@Symptoms_AbdominalPain", model.Symptoms_AbdominalPain),
                new SqlParameter("@Symptoms_Constipation", model.Symptoms_Constipation),
                new SqlParameter("@Symptoms_Diarrhea", model.Symptoms_Diarrhea),
                new SqlParameter("@Symptoms_RectalBleedingMucous", model.Symptoms_RectalBleedingMucous),
                new SqlParameter("@Symptoms_RectalBleedingNoMucous", model.Symptoms_RectalBleedingNoMucous),
                new SqlParameter("@Symptoms_Tenesmus", model.Symptoms_Tenesmus),

                new SqlParameter("@MedicalHistory_Diabetes", model.MedicalHistory_Diabetes),
                new SqlParameter("@MedicalHistory_Hypertension", model.MedicalHistory_Hypertension),
                new SqlParameter("@MedicalHistory_Dyslipidemia", model.MedicalHistory_Dyslipidemia),
                new SqlParameter("@MedicalHistory_Bleeding", model.MedicalHistory_Bleeding),
                new SqlParameter("@MedicalHistory_Asthma", model.MedicalHistory_Asthma),

                new SqlParameter("@AllergyHistory_Medication", model.AllergyHistory_Medication),
                new SqlParameter("@AllergyHistory_MedicationDetails", (object?)model.AllergyHistory_MedicationDetails ?? DBNull.Value),
                new SqlParameter("@AllergyHistory_Food", model.AllergyHistory_Food),
                new SqlParameter("@AllergyHistory_FoodDetails", (object?)model.AllergyHistory_FoodDetails ?? DBNull.Value),

                new SqlParameter("@MedicationHistory_Anticoagulant", model.MedicationHistory_Anticoagulant),
                new SqlParameter("@MedicationHistory_AnticoagulantDetails", (object?)model.MedicationHistory_AnticoagulantDetails ?? DBNull.Value),
                new SqlParameter("@MedicationHistory_Narcotics", model.MedicationHistory_Narcotics),
                new SqlParameter("@MedicationHistory_NarcoticsDetails", (object?)model.MedicationHistory_NarcoticsDetails ?? DBNull.Value),
                new SqlParameter("@MedicationHistory_Insulin", model.MedicationHistory_Insulin),
                new SqlParameter("@MedicationHistory_InsulinDetails", (object?)model.MedicationHistory_InsulinDetails ?? DBNull.Value),
                new SqlParameter("@MedicationHistory_AntiHypertensives", model.MedicationHistory_AntiHypertensives),
                new SqlParameter("@MedicationHistory_AntiHypertensivesDetails", (object?)model.MedicationHistory_AntiHypertensivesDetails ?? DBNull.Value),

                new SqlParameter("@PreviousScope_Date", (object?)model.PreviousScope_Date ?? DBNull.Value),

                new SqlParameter("@FamilyHistory_FirstDegree", model.FamilyHistory_FirstDegree),
                new SqlParameter("@FamilyHistory_SecondDegree", model.FamilyHistory_SecondDegree),

                new SqlParameter("@PhysicalExamination_Details", model.PhysicalExamination_Details ?? ""),

                new SqlParameter("@Investigation_FBC", model.Investigation_FBC),
                new SqlParameter("@Investigation_BUSE", model.Investigation_BUSE),
                new SqlParameter("@Investigation_RBS", model.Investigation_RBS),
                new SqlParameter("@Investigation_LFT", model.Investigation_LFT),
                new SqlParameter("@Investigation_Coag", model.Investigation_Coag),

                new SqlParameter("@Management_BowelPrep", model.Management_BowelPrep),
                new SqlParameter("@Management_Procedure", model.Management_Procedure),
                new SqlParameter("@Management_Consent", model.Management_Consent),
                new SqlParameter("@Management_Advise", model.Management_Advise)
            };

                    await _db.ExecuteNonQueryAsync("spPatientAssessment_UpdateWithJourney", prms.ToArray());

                    return Ok(new { success = true, patientJourneyId = model.PatientJourneyId });
                }
            }
            catch (SqlException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch
            {
                return Ok(new { success = false, message = "Error saving assessment." });
            }
        }

        // ------------------------------
        // PATIENT COLONOSCOPY
        // ------------------------------

        [Authorize(Policy = "AdminOrSuperOrStaff")]
        [HttpGet]
        public async Task<IActionResult> GetPatientColonoscopy(int patientJourneyId)
        {
            if (patientJourneyId <= 0)
                return Ok(new { success = false, message = "Invalid journey." });

            try
            {
                // 1) Journey row
                var dtJ = await _db.ExecuteDataTableAsync(
                    "spPatientJourney_GetById",
                    new[] { new SqlParameter("@PatientJourney_ID", patientJourneyId) }
                );
                if (dtJ.Rows.Count == 0)
                    return Ok(new { success = false, message = "Journey not found." });

                var j = dtJ.Rows[0];

                string ToLocalInput(object v)
                {
                    if (v == null || v == DBNull.Value) return "";
                    var d = Convert.ToDateTime(v);
                    return d.ToString("yyyy-MM-ddTHH:mm");
                }

                var journey = new
                {
                    patientJourneyId = patientJourneyId,
                    journeyType = j["PjAppType_Name"]?.ToString() ?? "",
                    journeyDateInput = ToLocalInput(j["PatientJourney_Date"])
                };

                // 2) Colonoscopy row
                var dtA = await _db.ExecuteDataTableAsync(
                    "spPatientColonoscopy_GetByJourneyId",
                    new[] { new SqlParameter("@PatientJourney_ID", patientJourneyId) }
                );

                object? assessment = null;
                if (dtA.Rows.Count > 0)
                {
                    var a = dtA.Rows[0];
                    assessment = dtA.Columns.Cast<DataColumn>()
                        .ToDictionary(c => c.ColumnName, c => a[c] == DBNull.Value ? null : a[c]);
                }

                return Ok(new { success = true, journey, assessment });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading colonoscopy." });
            }
        }

        public class SavePatientColonoscopyRequest
        {
            public int PatientJourneyId { get; set; } // 0 = create
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
        }

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
                if (model.PatientJourneyId <= 0)
                {
                    // CREATE
                    var prms = new List<SqlParameter>
                    {
                        new SqlParameter("@Patient_ID", model.PatientId.Trim()),
                        new SqlParameter("@PatientJourney_Date", model.PatientJourneyDate),
                        new SqlParameter("@Staff_ID", staffId),
                        new SqlParameter("@Audit_Note", (object?)model.AuditNote ?? DBNull.Value),

                        new SqlParameter("@ColonoscopyStatus", model.ColonoscopyStatus),
                        new SqlParameter("@ColonoscopyStatus_Details", (object?)model.ColonoscopyStatus_Details ?? DBNull.Value),
                        new SqlParameter("@BowelPreparation", model.BowelPreparation),

                        new SqlParameter("@Findings_Anus", model.Findings_Anus),
                        new SqlParameter("@Findings_AnusDetails", (object?)model.Findings_AnusDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_Rectum", model.Findings_Rectum),
                        new SqlParameter("@Findings_RectumDetails", (object?)model.Findings_RectumDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_SigmoidColon", model.Findings_SigmoidColon),
                        new SqlParameter("@Findings_SigmoidColonDetails", (object?)model.Findings_SigmoidColonDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_DescendingColon", model.Findings_DescendingColon),
                        new SqlParameter("@Findings_DescendingColonDetails", (object?)model.Findings_DescendingColonDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_SplenicFlexure", model.Findings_SplenicFlexure),
                        new SqlParameter("@Findings_SplenicFlexureDetails", (object?)model.Findings_SplenicFlexureDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_TransverseColon", model.Findings_TransverseColon),
                        new SqlParameter("@Findings_TransverseColonDetails", (object?)model.Findings_TransverseColonDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_HepaticFlexure", model.Findings_HepaticFlexure),
                        new SqlParameter("@Findings_HepaticFlexureDetails", (object?)model.Findings_HepaticFlexureDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_AscendingColon", model.Findings_AscendingColon),
                        new SqlParameter("@Findings_AscendingColonDetails", (object?)model.Findings_AscendingColonDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_Caecum", model.Findings_Caecum),
                        new SqlParameter("@Findings_CaecumDetails", (object?)model.Findings_CaecumDetails ?? DBNull.Value),

                        new SqlParameter("@HPE_Status", model.HPE_Status),
                        new SqlParameter("@HPE_Details", (object?)model.HPE_Details ?? DBNull.Value),

                        new SqlParameter("@Complications", model.Complications ?? ""),
                        new SqlParameter("@Complications_Details", (object?)model.Complications_Details ?? DBNull.Value),

                        new SqlParameter("@DischargePlan", model.DischargePlan ?? "")
                    };

                    var dt = await _db.ExecuteDataTableAsync("spPatientColonoscopy_CreateWithJourney", prms.ToArray());

                    var newJourneyId = dt.Rows.Count > 0
                        ? Convert.ToInt32(dt.Rows[0]["PatientJourney_ID"])
                        : 0;

                    return Ok(new { success = true, patientJourneyId = newJourneyId });
                }
                else
                {
                    // UPDATE
                    var prms = new List<SqlParameter>
                    {
                        new SqlParameter("@PatientJourney_ID", model.PatientJourneyId),
                        new SqlParameter("@PatientJourney_Date", model.PatientJourneyDate),
                        new SqlParameter("@Staff_ID", staffId),
                        new SqlParameter("@Audit_Note", (object?)model.AuditNote ?? DBNull.Value),

                        new SqlParameter("@ColonoscopyStatus", model.ColonoscopyStatus),
                        new SqlParameter("@ColonoscopyStatus_Details", (object?)model.ColonoscopyStatus_Details ?? DBNull.Value),
                        new SqlParameter("@BowelPreparation", model.BowelPreparation),

                        new SqlParameter("@Findings_Anus", model.Findings_Anus),
                        new SqlParameter("@Findings_AnusDetails", (object?)model.Findings_AnusDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_Rectum", model.Findings_Rectum),
                        new SqlParameter("@Findings_RectumDetails", (object?)model.Findings_RectumDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_SigmoidColon", model.Findings_SigmoidColon),
                        new SqlParameter("@Findings_SigmoidColonDetails", (object?)model.Findings_SigmoidColonDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_DescendingColon", model.Findings_DescendingColon),
                        new SqlParameter("@Findings_DescendingColonDetails", (object?)model.Findings_DescendingColonDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_SplenicFlexure", model.Findings_SplenicFlexure),
                        new SqlParameter("@Findings_SplenicFlexureDetails", (object?)model.Findings_SplenicFlexureDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_TransverseColon", model.Findings_TransverseColon),
                        new SqlParameter("@Findings_TransverseColonDetails", (object?)model.Findings_TransverseColonDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_HepaticFlexure", model.Findings_HepaticFlexure),
                        new SqlParameter("@Findings_HepaticFlexureDetails", (object?)model.Findings_HepaticFlexureDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_AscendingColon", model.Findings_AscendingColon),
                        new SqlParameter("@Findings_AscendingColonDetails", (object?)model.Findings_AscendingColonDetails ?? DBNull.Value),
                        new SqlParameter("@Findings_Caecum", model.Findings_Caecum),
                        new SqlParameter("@Findings_CaecumDetails", (object?)model.Findings_CaecumDetails ?? DBNull.Value),

                        new SqlParameter("@HPE_Status", model.HPE_Status),
                        new SqlParameter("@HPE_Details", (object?)model.HPE_Details ?? DBNull.Value),

                        new SqlParameter("@Complications", model.Complications ?? ""),
                        new SqlParameter("@Complications_Details", (object?)model.Complications_Details ?? DBNull.Value),

                        new SqlParameter("@DischargePlan", model.DischargePlan ?? "")
                    };

                    await _db.ExecuteNonQueryAsync("spPatientColonoscopy_UpdateWithJourney", prms.ToArray());
                    return Ok(new { success = true, patientJourneyId = model.PatientJourneyId });
                }
            }
            catch (SqlException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch
            {
                return Ok(new { success = false, message = "Error saving colonoscopy." });
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
                var dtJ = await _db.ExecuteDataTableAsync(
                    "spPatientJourney_GetById",
                    new[] { new SqlParameter("@PatientJourney_ID", patientJourneyId) }
                );
                if (dtJ.Rows.Count == 0)
                    return Ok(new { success = false, message = "Journey not found." });

                var j = dtJ.Rows[0];

                string ToLocalInput(object v)
                {
                    if (v == null || v == DBNull.Value) return "";
                    var d = Convert.ToDateTime(v);
                    return d.ToString("yyyy-MM-ddTHH:mm");
                }

                var journey = new
                {
                    patientJourneyId = patientJourneyId,
                    journeyType = j["PjAppType_Name"]?.ToString() ?? "",
                    journeyDateInput = ToLocalInput(j["PatientJourney_Date"])
                };

                // 2) Follow up row
                var dtA = await _db.ExecuteDataTableAsync(
                    "spPatientFollowUp_GetByJourneyId",
                    new[] { new SqlParameter("@PatientJourney_ID", patientJourneyId) }
                );

                object? assessment = null;
                if (dtA.Rows.Count > 0)
                {
                    var a = dtA.Rows[0];
                    assessment = dtA.Columns.Cast<DataColumn>()
                        .ToDictionary(c => c.ColumnName, c => a[c] == DBNull.Value ? null : a[c]);
                }

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
                if (model.PatientJourneyId <= 0)
                {
                    // CREATE
                    var prms = new List<SqlParameter>
                    {
                        new SqlParameter("@Patient_ID", model.PatientId.Trim()),
                        new SqlParameter("@PatientJourney_Date", model.PatientJourneyDate),
                        new SqlParameter("@Staff_ID", staffId),
                        new SqlParameter("@Audit_Note", (object?)model.AuditNote ?? DBNull.Value),

                        new SqlParameter("@HPE_Results", model.HPE_Results ?? ""),
                        new SqlParameter("@DischargePlan", model.DischargePlan ?? ""),
                        new SqlParameter("@DischargeSummary_Status", model.DischargeSummary_Status)
                    };

                    var dt = await _db.ExecuteDataTableAsync("spPatientFollowUp_CreateWithJourney", prms.ToArray());

                    var newJourneyId = dt.Rows.Count > 0
                        ? Convert.ToInt32(dt.Rows[0]["PatientJourney_ID"])
                        : 0;

                    return Ok(new { success = true, patientJourneyId = newJourneyId });
                }
                else
                {
                    // UPDATE
                    var prms = new List<SqlParameter>
                    {
                        new SqlParameter("@PatientJourney_ID", model.PatientJourneyId),
                        new SqlParameter("@PatientJourney_Date", model.PatientJourneyDate),
                        new SqlParameter("@Staff_ID", staffId),
                        new SqlParameter("@Audit_Note", (object?)model.AuditNote ?? DBNull.Value),

                        new SqlParameter("@HPE_Results", model.HPE_Results ?? ""),
                        new SqlParameter("@DischargePlan", model.DischargePlan ?? ""),
                        new SqlParameter("@DischargeSummary_Status", model.DischargeSummary_Status)
                    };

                    await _db.ExecuteNonQueryAsync("spPatientFollowUp_UpdateWithJourney", prms.ToArray());
                    return Ok(new { success = true, patientJourneyId = model.PatientJourneyId });
                }
            }
            catch (SqlException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch
            {
                return Ok(new { success = false, message = "Error saving follow up." });
            }
        }
    }
}
