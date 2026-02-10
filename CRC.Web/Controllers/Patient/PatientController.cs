using CRC.Web.Data;
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
        private readonly DatabaseHelper _db;
        private readonly IWebHostEnvironment _env;

        public PatientController(DatabaseHelper db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
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
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientBasic_ListActive",
                    Array.Empty<SqlParameter>()
                );

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        patientId = r["Patient_ID"]?.ToString(),
                        name = r["Patient_Name"]?.ToString()
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
                var parameters = new[]
                {
            new SqlParameter("@Patient_ID", patientId)
        };

                await _db.ExecuteNonQueryAsync("spPatient_DeleteCascade", parameters);

                return Ok(new { success = true });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error deleting patient." });
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
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientBasic_ListDischarged",
                    Array.Empty<SqlParameter>()
                );

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        patientId = r["Patient_ID"]?.ToString(),
                        name = r["Patient_Name"]?.ToString(),
                        dischargeDate = r["Patient_DischargeDate"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["Patient_DischargeDate"]).ToString("dd/MM/yyyy")
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

        private static string? ToDateInputString(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            if (DateTime.TryParse(value.ToString(), out var dt))
            {
                return dt.ToString("yyyy-MM-dd");
            }
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> GetBasicLookups()
        {
            try
            {
                var emptyParams = Array.Empty<SqlParameter>();

                var dtRace = await _db.ExecuteDataTableAsync("spLU_Race_List", emptyParams);
                var dtSource = await _db.ExecuteDataTableAsync("spLU_Source_List", emptyParams);
                var dtReligion = await _db.ExecuteDataTableAsync("spLU_Religion_List", emptyParams);
                var dtMarital = await _db.ExecuteDataTableAsync("spLU_MaritalStatus_List", emptyParams);
                var dtOccupation = await _db.ExecuteDataTableAsync("spLU_Occupation_List", emptyParams);

                var races = dtRace.Rows.Cast<DataRow>()
                    .Select(r => new { id = r["Race_ID"]?.ToString(), name = r["Race_Name"]?.ToString() })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .ToList();

                var sources = dtSource.Rows.Cast<DataRow>()
                    .Select(r => new { id = r["Source_ID"]?.ToString(), name = r["Source_Name"]?.ToString() })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .ToList();

                var religions = dtReligion.Rows.Cast<DataRow>()
                    .Select(r => new { id = r["Religion_ID"]?.ToString(), name = r["Religion_Name"]?.ToString() })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .ToList();

                var maritalStatuses = dtMarital.Rows.Cast<DataRow>()
                    .Select(r => new { id = r["MaritalStatus_ID"]?.ToString(), name = r["MaritalStatus_Name"]?.ToString() })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .ToList();

                var occupations = dtOccupation.Rows.Cast<DataRow>()
                    .Select(r => new { id = r["Occupation_ID"]?.ToString(), name = r["Occupation_Name"]?.ToString() })
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
                var dt = await _db.ExecuteDataTableAsync("spLU_LOCATION_ListStates", Array.Empty<SqlParameter>());

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        id = r["LocationId"] == DBNull.Value ? 0 : Convert.ToInt32(r["LocationId"]),
                        name = r["Name"]?.ToString() ?? ""
                    })
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
                var dt = await _db.ExecuteDataTableAsync(
                    "spLU_LOCATION_ListCityByState",
                    new[] { new SqlParameter("@StateId", SqlDbType.Int) { Value = stateId } });

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        id = r["LocationId"] == DBNull.Value ? 0 : Convert.ToInt32(r["LocationId"]),
                        name = r["Name"]?.ToString() ?? ""
                    })
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
                var dt = await _db.ExecuteDataTableAsync(
                    "spLU_LOCATION_ListPostcodesByCity",
                    new[] { new SqlParameter("@CityId", SqlDbType.Int) { Value = cityId } });

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        id = r["LocationId"] == DBNull.Value ? 0 : Convert.ToInt32(r["LocationId"]),
                        name = r["Name"]?.ToString() ?? ""
                    })
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
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientBasic_GetById",
                    new[] { new SqlParameter("@Patient_ID", patientId.Trim()) }
                );

                if (dt.Rows.Count == 0)
                {
                    return Ok(new { success = false, message = "Patient not found." });
                }

                var row = dt.Rows[0];

                var patient = new
                {
                    patientId = row["Patient_ID"]?.ToString() ?? "",
                    name = row["Patient_Name"]?.ToString() ?? "",
                    email = row["Patient_Email"]?.ToString() ?? "",
                    phone = row["Patient_Phone"]?.ToString() ?? "",
                    nric = row["Patient_NRIC"]?.ToString() ?? "",

                    birthDate = ToDateInputString(row["Patient_BirthDate"]),
                    age = row["Patient_Age"] == DBNull.Value ? 0 : Convert.ToInt32(row["Patient_Age"]),
                    gender = row["Patient_Gender"]?.ToString() ?? "",

                    raceId = row["Race_ID"]?.ToString() ?? "",
                    sourceId = row["Source_ID"]?.ToString() ?? "",
                    religionId = row["Religion_ID"]?.ToString() ?? "",
                    maritalStatusId = row["MaritalStatus_ID"]?.ToString() ?? "",
                    occupationId = row["Occupation_ID"]?.ToString() ?? "",

                    resState = row["Patient_ResState"]?.ToString() ?? "",
                    resCity = row["Patient_ResCity"]?.ToString() ?? "",
                    resPostcode = row["Patient_ResPostcode"]?.ToString() ?? "",
                    addLine1 = row["Patient_AddLine1"]?.ToString() ?? "",
                    addLine2 = row["Patient_AddLine2"] == DBNull.Value ? "" : row["Patient_AddLine2"]?.ToString() ?? "",

                    emergencyName = row["Patient_EmergencyName"]?.ToString() ?? "",
                    emergencyRelationship = row["Patient_EmergencyRelationship"]?.ToString() ?? "",
                    emergencyNumber = row["Patient_EmergencyNumber"]?.ToString() ?? "",

                    dischargeTypeId = row["DischargeType_ID"] == DBNull.Value ? null : row["DischargeType_ID"]?.ToString(),
                    dischargeTypeName = row["DischargeType_Name"] == DBNull.Value ? null : row["DischargeType_Name"]?.ToString(),
                    dischargeDate = ToDateInputString(row["Patient_DischargeDate"]),
                    dischargeRemarks = row["Patient_DischargeRemarks"] == DBNull.Value ? "" : row["Patient_DischargeRemarks"]?.ToString() ?? ""
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
                var dt = await _db.ExecuteDataTableAsync(
                    "spLU_DischargeType_List",
                    Array.Empty<SqlParameter>()
                );

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        dischargeTypeId = r["DischargeType_ID"]?.ToString(),
                        dischargeTypeName = r["DischargeType_Name"]?.ToString()
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

                    var dtMissing = await _db.ExecuteDataTableAsync(
                        "spPatient_Discharge_CheckMissingDocuments",
                        new[]
                        {
                            new SqlParameter("@Patient_ID", model.PatientId!.Trim()),
                            new SqlParameter("@DischargeType_ID", dischargeTypeId)
                        });

                    if (dtMissing.Rows.Count > 0)
                    {
                        var missingNames = dtMissing.Rows.Cast<DataRow>()
                            .Select(r => r["PatientDocumentType_Name"]?.ToString())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();

                        var msgMissing =
                            "Please upload the following mandatory documents before discharging this patient: " +
                            string.Join(", ", missingNames);

                        return Ok(new { success = false, message = msgMissing });
                    }
                }

                // ----------- INSERT NEW PATIENT -----------
                if (isNew)
                {
                    var parameters = new List<SqlParameter>
                    {
                        new SqlParameter("@Patient_Name",                  name.ToUpperInvariant()),
                        new SqlParameter("@Patient_Email",                 email),
                        new SqlParameter("@Patient_Phone",                 phone),
                        new SqlParameter("@Patient_NRIC",                  nricDigits),
                        new SqlParameter("@Patient_BirthDate",             birthDate),
                        new SqlParameter("@Patient_Age",                   age),
                        new SqlParameter("@Race_ID",                       raceId),
                        new SqlParameter("@Source_ID",                     sourceId),
                        new SqlParameter("@Patient_Gender",                gender),
                        new SqlParameter("@Religion_ID",                   religionId),
                        new SqlParameter("@MaritalStatus_ID",              maritalStatusId),
                        new SqlParameter("@Occupation_ID",                 occupationId),
                        new SqlParameter("@Patient_ResState",              resState),
                        new SqlParameter("@Patient_ResCity",               resCity),
                        new SqlParameter("@Patient_ResPostcode",           resPostcode),
                        new SqlParameter("@Patient_AddLine1",              addLine1),
                        new SqlParameter("@Patient_AddLine2",              string.IsNullOrWhiteSpace(addLine2) ? (object)DBNull.Value : addLine2),
                        new SqlParameter("@Patient_EmergencyName",         emergencyName.ToUpperInvariant()),
                        new SqlParameter("@Patient_EmergencyRelationship", emergencyRel),
                        new SqlParameter("@Patient_EmergencyNumber",       emergencyNum)
                        // Discharge columns for new records will default to NULL
                    };

                    var outParam = new SqlParameter("@NewPatient_ID", SqlDbType.VarChar, 100)
                    {
                        Direction = ParameterDirection.Output
                    };
                    parameters.Add(outParam);

                    await _db.ExecuteNonQueryAsync("spPatientBasic_Insert", parameters.ToArray());

                    var newId = outParam.Value?.ToString() ?? string.Empty;

                    return Ok(new { success = true, patientId = newId });
                }

                // ----------- UPDATE EXISTING PATIENT (including discharge info) -----------
                string patientId = model.PatientId!.Trim();

                var updateParams = new List<SqlParameter>
                {
                    new SqlParameter("@Patient_ID",                    patientId),
                    new SqlParameter("@Patient_Name",                  name.ToUpperInvariant()),
                    new SqlParameter("@Patient_Email",                 email),
                    new SqlParameter("@Patient_Phone",                 phone),
                    new SqlParameter("@Patient_NRIC",                  nricDigits),
                    new SqlParameter("@Patient_BirthDate",             birthDate),
                    new SqlParameter("@Patient_Age",                   age),
                    new SqlParameter("@Race_ID",                       raceId),
                    new SqlParameter("@Source_ID",                     sourceId),
                    new SqlParameter("@Patient_Gender",                gender),
                    new SqlParameter("@Religion_ID",                   religionId),
                    new SqlParameter("@MaritalStatus_ID",              maritalStatusId),
                    new SqlParameter("@Occupation_ID",                 occupationId),
                    new SqlParameter("@Patient_ResState",              resState),
                    new SqlParameter("@Patient_ResCity",               resCity),
                    new SqlParameter("@Patient_ResPostcode",           resPostcode),
                    new SqlParameter("@Patient_AddLine1",              addLine1),
                    new SqlParameter("@Patient_AddLine2",              string.IsNullOrWhiteSpace(addLine2) ? (object)DBNull.Value : addLine2),
                    new SqlParameter("@Patient_EmergencyName",         emergencyName.ToUpperInvariant()),
                    new SqlParameter("@Patient_EmergencyRelationship", emergencyRel),
                    new SqlParameter("@Patient_EmergencyNumber",       emergencyNum),

                    new SqlParameter("@DischargeType_ID", (object?)(isDischarged ? dischargeTypeId : null) ?? DBNull.Value),
                    new SqlParameter("@Patient_DischargeDate", isDischarged && dischargeDate.HasValue ? (object)dischargeDate.Value : DBNull.Value),
                    new SqlParameter("@Patient_DischargeRemarks", (object?)(isDischarged ? (string.IsNullOrWhiteSpace(dischargeRemarks) ? null : dischargeRemarks) : null) ?? DBNull.Value)
                };

                await _db.ExecuteNonQueryAsync("spPatientBasic_Update", updateParams.ToArray());

                return Ok(new { success = true, patientId });
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
                using (var cmdSlots = new SqlCommand("spStaffSlots_List", conn, tx)
                {
                    CommandType = CommandType.StoredProcedure
                })
                {
                    cmdSlots.Parameters.AddRange(new[]
                    {
                        new SqlParameter("@Staff_ID", staffId),
                        new SqlParameter("@FromDate", SqlDbType.Date) { Value = apptDate.Date },
                        new SqlParameter("@ToDate", SqlDbType.Date) { Value = apptDate.Date }
                    });

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

                if (appointmentId <= 0)
                {
                    // INSERT
                    using var cmd = new SqlCommand("spPatientAppointment_Insert", conn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    cmd.Parameters.AddRange(new[]
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
                    using var cmd = new SqlCommand("spPatientAppointment_Update", conn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    cmd.Parameters.AddRange(new[]
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

                    await cmd.ExecuteNonQueryAsync();

                    // Release previous slots (if any)
                    using (var cmdClear = new SqlCommand("spStaffSlots_ClearAppointment", conn, tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    })
                    {
                        cmdClear.Parameters.Add(new SqlParameter("@ApptId", SqlDbType.Int) { Value = appointmentId });
                        await cmdClear.ExecuteNonQueryAsync();
                    }
                }

                // Assign selected slots to the appointment
                using (var cmdAssign = new SqlCommand("spStaffSlots_AssignAppointment", conn, tx)
                {
                    CommandType = CommandType.StoredProcedure
                })
                {
                    cmdAssign.Parameters.Add(new SqlParameter("@ApptId", SqlDbType.Int) { Value = finalAppointmentId });
                    cmdAssign.Parameters.Add(new SqlParameter("@StaffSlotIds", SqlDbType.VarChar, -1)
                    {
                        Value = string.Join(",", slotIds)
                    });
                    await cmdAssign.ExecuteNonQueryAsync();
                }

                tx.Commit();

                return Ok(new { success = true, appointmentId = finalAppointmentId });
            }
            catch (SqlException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch
            {
                return Ok(new { success = false, message = "Error saving appointment." });
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

                return Ok(new { success = true });
            }
            catch (SqlException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch
            {
                return Ok(new { success = false, message = "Error deleting appointment." });
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