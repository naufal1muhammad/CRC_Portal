using CRC.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRC.Web.Controllers
{
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
                        name = r["Patient_Name"]?.ToString(),
                        email = r["Patient_Email"]?.ToString(),
                        phone = r["Patient_Phone"]?.ToString(),
                        branchName = r["Branch_Name"]?.ToString(),
                        admittedOn = r["Patient_AdmittedOn"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["Patient_AdmittedOn"]).ToString("dd/MM/yyyy"),
                        dischargeTypeName = r["DischargeType_Name"]?.ToString() ?? "",
                        dischargeDate = r["Patient_DischargeDate"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["Patient_DischargeDate"]).ToString("dd/MM/yyyy")
                    })
                    .ToList();

                return Ok(list);
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading active patients." });
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
                        email = r["Patient_Email"]?.ToString(),
                        phone = r["Patient_Phone"]?.ToString(),
                        branchName = r["Branch_Name"]?.ToString(),
                        admittedOn = r["Patient_AdmittedOn"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["Patient_AdmittedOn"]).ToString("dd/MM/yyyy"),
                        dischargeTypeName = r["DischargeType_Name"]?.ToString() ?? "",
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
                var dtBranches = await _db.ExecuteDataTableAsync("spBranch_ListActive", emptyParams); 
                var races = dtRace.Rows.Cast<DataRow>().Select(r => new { id = r["Race_ID"]?.ToString(), name = r["Race_Name"]?.ToString() }).ToList(); 
                var sources = dtSource.Rows.Cast<DataRow>().Select(r => new { id = r["Source_ID"]?.ToString(), name = r["Source_Name"]?.ToString() }).ToList(); 
                var religions = dtReligion.Rows.Cast<DataRow>().Select(r => new { id = r["Religion_ID"]?.ToString(), name = r["Religion_Name"]?.ToString() }).ToList(); 
                var maritalStatuses = dtMarital.Rows.Cast<DataRow>().Select(r => new { id = r["MaritalStatus_ID"]?.ToString(), name = r["MaritalStatus_Name"]?.ToString() }).ToList(); 
                var occupations = dtOccupation.Rows.Cast<DataRow>().Select(r => new { id = r["Occupation_ID"]?.ToString(), name = r["Occupation_Name"]?.ToString() }).ToList(); 
                var branches = dtBranches.Rows.Cast<DataRow>().Select(r => new { branchId = r["Branch_ID"]?.ToString(), branchName = r["Branch_Name"]?.ToString() }).ToList(); 
                return Ok(new 
                { success = true, races, sources, religions, maritalStatuses, occupations, branches }); 
            } 
            catch (Exception) { 
                return Ok(new 
                { success = false, message = "Error loading lookups." }); 
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
                    new[] { new SqlParameter("@Patient_ID", patientId) }
                );

                if (dt.Rows.Count == 0)
                {
                    return Ok(new { success = false, message = "Patient not found." });
                }

                var row = dt.Rows[0];

                var patient = new
                {
                    patientId = row["Patient_ID"]?.ToString(),
                    name = row["Patient_Name"]?.ToString(),
                    email = row["Patient_Email"]?.ToString(),
                    phone = row["Patient_Phone"]?.ToString(),
                    nric = row["Patient_NRIC"]?.ToString(),

                    admittedOn = ToDateInputString(row["Patient_AdmittedOn"]),
                    birthDate = ToDateInputString(row["Patient_BirthDate"]),

                    raceName = row["Race_Name"]?.ToString(),
                    branchName = row["Branch_Name"]?.ToString(),
                    sourceName = row["Source_Name"]?.ToString(),
                    gender = row["Patient_Gender"]?.ToString(),
                    religionName = row["Religion_Name"]?.ToString(),
                    maritalStatusName = row["MaritalStatus_Name"]?.ToString(),
                    address = row["Patient_Address"]?.ToString(),
                    emergencyName = row["Patient_EmergencyName"]?.ToString(),
                    emergencyRelationship = row["Patient_EmergencyRelationship"]?.ToString(),
                    emergencyNumber = row["Patient_EmergencyNumber"]?.ToString(),
                    occupationName = row["Occupation_Name"]?.ToString(),
                    dischargeTypeName = row["DischargeType_Name"] == DBNull.Value
        ? null
        : row["DischargeType_Name"]?.ToString(),
                    dischargeDate = ToDateInputString(row["Patient_DischargeDate"]),
                    dischargeRemarks = row["Patient_DischargeRemarks"] == DBNull.Value
        ? null
        : row["Patient_DischargeRemarks"]?.ToString()
                };

                return Ok(new { success = true, patient });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading patient details." });
            }
        }

        // DTO for saving basic details
        public class SaveBasicRequest
        {
            public string? PatientId { get; set; }

            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string NRIC { get; set; } = string.Empty;

            public string AdmittedOn { get; set; } = string.Empty; // yyyy-MM-dd
            public string BirthDate { get; set; } = string.Empty;  // yyyy-MM-dd

            public string RaceName { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
            public string SourceName { get; set; } = string.Empty;

            public string Gender { get; set; } = string.Empty;
            public string ReligionName { get; set; } = string.Empty;
            public string MaritalStatusName { get; set; } = string.Empty;

            public string Address { get; set; } = string.Empty;
            public string EmergencyName { get; set; } = string.Empty;
            public string EmergencyRelationship { get; set; } = string.Empty;
            public string EmergencyNumber { get; set; } = string.Empty;

            public string OccupationName { get; set; } = string.Empty;
            public bool IsDischarged { get; set; }
            public string? DischargeTypeId { get; set; }          // LU_DISCHARGETYPE.DischargeType_ID
            public string? DischargeTypeName { get; set; }        // DischargeType_Name to store in PatientBasic
            public string DischargeDate { get; set; } = string.Empty; // yyyy-MM-dd
            public string DischargeRemarks { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> SaveBasic([FromBody] SaveBasicRequest model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            // ----------- BASIC FIELDS (same as before) -----------
            string name = model.Name?.Trim() ?? string.Empty;
            string email = model.Email?.Trim() ?? string.Empty;
            string phone = model.Phone?.Trim() ?? string.Empty;
            string nric = model.NRIC?.Trim() ?? string.Empty;

            string admittedOnStr = model.AdmittedOn?.Trim() ?? string.Empty;
            string birthDateStr = model.BirthDate?.Trim() ?? string.Empty;

            string raceName = model.RaceName?.Trim() ?? string.Empty;
            string branchName = model.BranchName?.Trim() ?? string.Empty;
            string sourceName = model.SourceName?.Trim() ?? string.Empty;
            string gender = model.Gender?.Trim() ?? string.Empty;
            string religionName = model.ReligionName?.Trim() ?? string.Empty;
            string maritalStatusName = model.MaritalStatusName?.Trim() ?? string.Empty;
            string address = model.Address?.Trim() ?? string.Empty;
            string emergencyName = model.EmergencyName?.Trim() ?? string.Empty;
            string emergencyRel = model.EmergencyRelationship?.Trim() ?? string.Empty;
            string emergencyNum = model.EmergencyNumber?.Trim() ?? string.Empty;
            string occupationName = model.OccupationName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(nric) ||
                string.IsNullOrWhiteSpace(admittedOnStr) ||
                string.IsNullOrWhiteSpace(birthDateStr) ||
                string.IsNullOrWhiteSpace(raceName) ||
                string.IsNullOrWhiteSpace(branchName) ||
                string.IsNullOrWhiteSpace(sourceName) ||
                string.IsNullOrWhiteSpace(gender) ||
                string.IsNullOrWhiteSpace(religionName) ||
                string.IsNullOrWhiteSpace(maritalStatusName) ||
                string.IsNullOrWhiteSpace(address) ||
                string.IsNullOrWhiteSpace(emergencyName) ||
                string.IsNullOrWhiteSpace(emergencyRel) ||
                string.IsNullOrWhiteSpace(emergencyNum) ||
                string.IsNullOrWhiteSpace(occupationName))
            {
                return Ok(new { success = false, message = "Please fill in all mandatory fields." });
            }

            if (!DateTime.TryParse(admittedOnStr, out var admittedOn))
            {
                return Ok(new { success = false, message = "Invalid Admitted On date." });
            }

            if (!DateTime.TryParse(birthDateStr, out var birthDate))
            {
                return Ok(new { success = false, message = "Invalid Birth Date." });
            }

            int age = CalculateAge(birthDate);

            // ----------- DISCHARGE FIELDS -----------
            bool isDischarged = model.IsDischarged;
            string dischargeTypeId = model.DischargeTypeId?.Trim() ?? string.Empty;
            string dischargeTypeName = model.DischargeTypeName?.Trim() ?? string.Empty;
            string dischargeDateStr = model.DischargeDate?.Trim() ?? string.Empty;
            string dischargeRemarks = model.DischargeRemarks?.Trim() ?? string.Empty;

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
                new SqlParameter("@Patient_Name",                  name),
                new SqlParameter("@Patient_Email",                 email),
                new SqlParameter("@Patient_Phone",                 phone),
                new SqlParameter("@Patient_NRIC",                  nric),
                new SqlParameter("@Patient_AdmittedOn",            admittedOn),
                new SqlParameter("@Patient_BirthDate",             birthDate),
                new SqlParameter("@Patient_Age",                   age),
                new SqlParameter("@Race_Name",                     raceName),
                new SqlParameter("@Branch_Name",                   branchName),
                new SqlParameter("@Source_Name",                   sourceName),
                new SqlParameter("@Patient_Gender",                gender),
                new SqlParameter("@Religion_Name",                 religionName),
                new SqlParameter("@MaritalStatus_Name",            maritalStatusName),
                new SqlParameter("@Patient_Address",               address),
                new SqlParameter("@Patient_EmergencyName",         emergencyName),
                new SqlParameter("@Patient_EmergencyRelationship", emergencyRel),
                new SqlParameter("@Patient_EmergencyNumber",       emergencyNum),
                new SqlParameter("@Occupation_Name",               occupationName)
                // Discharge columns for new records will default to NULL in the table
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
                else
                {
                    // ----------- UPDATE EXISTING PATIENT (including discharge info) -----------
                    string patientId = model.PatientId!.Trim();

                    var parameters = new List<SqlParameter>
            {
                new SqlParameter("@Patient_ID",                    patientId),
                new SqlParameter("@Patient_Name",                  name),
                new SqlParameter("@Patient_Email",                 email),
                new SqlParameter("@Patient_Phone",                 phone),
                new SqlParameter("@Patient_NRIC",                  nric),
                new SqlParameter("@Patient_AdmittedOn",            admittedOn),
                new SqlParameter("@Patient_BirthDate",             birthDate),
                new SqlParameter("@Patient_Age",                   age),
                new SqlParameter("@Race_Name",                     raceName),
                new SqlParameter("@Branch_Name",                   branchName),
                new SqlParameter("@Source_Name",                   sourceName),
                new SqlParameter("@Patient_Gender",                gender),
                new SqlParameter("@Religion_Name",                 religionName),
                new SqlParameter("@MaritalStatus_Name",            maritalStatusName),
                new SqlParameter("@Patient_Address",               address),
                new SqlParameter("@Patient_EmergencyName",         emergencyName),
                new SqlParameter("@Patient_EmergencyRelationship", emergencyRel),
                new SqlParameter("@Patient_EmergencyNumber",       emergencyNum),
                new SqlParameter("@Occupation_Name",               occupationName),

                new SqlParameter("@DischargeType_Name",
                    (object?) (isDischarged ? dischargeTypeName : null) ?? DBNull.Value),
                new SqlParameter("@Patient_DischargeDate",
                    isDischarged && dischargeDate.HasValue
                        ? (object) dischargeDate.Value
                        : DBNull.Value),
                new SqlParameter("@Patient_DischargeRemarks",
                    (object?) (isDischarged ? dischargeRemarks : null) ?? DBNull.Value)
            };

                    await _db.ExecuteNonQueryAsync("spPatientBasic_Update", parameters.ToArray());

                    return Ok(new { success = true, patientId = patientId });
                }
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "An unexpected error occurred while saving patient details." });
            }
        }

        //------------------------------------------------------
        //APPOINTMENTS
        //------------------------------------------------------

        public class SaveAppointmentRequest
        {
            public int? AppointmentId { get; set; }  // for future use (currently we only insert)
            public string PatientId { get; set; } = string.Empty;
            public string PjAppTypeName { get; set; } = string.Empty;
            public string AppointmentDateTime { get; set; } = string.Empty; // from <input type="datetime-local">
            public string Status { get; set; } = string.Empty;
        }

        public class DeleteAppointmentRequest
        {
            public int AppointmentId { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointmentLookups()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spLU_PJ_AppType_List",
                    Array.Empty<SqlParameter>()
                );

                var types = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        id = r["PjAppType_ID"]?.ToString(),
                        name = r["PjAppType_Name"]?.ToString()
                    })
                    .ToList();

                return Ok(new { success = true, types });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading appointment types." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointments(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                // No patient yet (new) -> just return empty list
                return Ok(new { success = true, data = Array.Empty<object>() });
            }

            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientAppointment_ListByPatient",
                    new[] { new SqlParameter("@Patient_ID", patientId) }
                );

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        appointmentId = Convert.ToInt32(r["PatientAppointment_ID"]),
                        patientId = r["Patient_ID"]?.ToString(),
                        typeName = r["PjAppType_Name"]?.ToString(),
                        appointmentDateTime = r["PatientAppointment_Date"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["PatientAppointment_Date"]).ToString("dd/MM/yyyy HH:mm"),
                        appointmentDateTimeRaw = r["PatientAppointment_Date"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["PatientAppointment_Date"]).ToString("yyyy-MM-ddTHH:mm"),
                        status = r["PatientAppointment_Status"]?.ToString()
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading appointments." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveAppointment([FromBody] SaveAppointmentRequest model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            var patientId = model.PatientId?.Trim() ?? string.Empty;
            var typeName = model.PjAppTypeName?.Trim() ?? string.Empty;
            var status = model.Status?.Trim() ?? string.Empty;
            var dtStr = model.AppointmentDateTime?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(patientId) ||
                string.IsNullOrWhiteSpace(typeName) ||
                string.IsNullOrWhiteSpace(status) ||
                string.IsNullOrWhiteSpace(dtStr))
            {
                return Ok(new { success = false, message = "Please fill in all mandatory appointment fields." });
            }

            if (!DateTime.TryParse(dtStr, out var apptDateTime))
            {
                return Ok(new { success = false, message = "Invalid appointment date/time." });
            }

            try
            {
                var isUpdate = model.AppointmentId.HasValue && model.AppointmentId.Value > 0;

                if (isUpdate)
                {
                    // UPDATE existing appointment
                    var parameters = new[]
                    {
                new SqlParameter("@PatientAppointment_ID", model.AppointmentId.Value),
                new SqlParameter("@PjAppType_Name",        typeName),
                new SqlParameter("@PatientAppointment_Date", apptDateTime),
                new SqlParameter("@PatientAppointment_Status", status)
            };

                    await _db.ExecuteNonQueryAsync("spPatientAppointment_Update", parameters);
                }
                else
                {
                    // INSERT new appointment
                    var parameters = new[]
                    {
                new SqlParameter("@Patient_ID",              patientId),
                new SqlParameter("@PjAppType_Name",          typeName),
                new SqlParameter("@PatientAppointment_Date", apptDateTime),
                new SqlParameter("@PatientAppointment_Status", status)
            };

                    await _db.ExecuteNonQueryAsync("spPatientAppointment_Insert", parameters);
                }

                return Ok(new { success = true });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error saving appointment." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAppointment([FromBody] DeleteAppointmentRequest model)
        {
            if (model == null || model.AppointmentId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid appointment id." });
            }

            try
            {
                var parameters = new[]
                {
            new SqlParameter("@PatientAppointment_ID", model.AppointmentId)
        };

                await _db.ExecuteNonQueryAsync("spPatientAppointment_Delete", parameters);

                return Ok(new { success = true });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error deleting appointment." });
            }
        }

        //------------------------------------------------------
        //PATIENT JOURNEY
        //------------------------------------------------------
        public class SaveJourneyRequest
        {
            public int? JourneyId { get; set; }          // reserved for future edit support
            public string PatientId { get; set; } = string.Empty;
            public string PjAppTypeName { get; set; } = string.Empty;
            public string JourneyDate { get; set; } = string.Empty;  // from <input type="date">
            public string StaffId { get; set; } = string.Empty;
            public string Remarks { get; set; } = string.Empty;
        }

        public class DeleteJourneyRequest
        {
            public int JourneyId { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetJourneyStaffList()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spStaff_List",
                    Array.Empty<SqlParameter>()
                );

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        staffId = r["Staff_ID"]?.ToString(),
                        staffName = r["Staff_Name"]?.ToString()
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading staff list." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetJourneys(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                // New patient (not saved yet) => nothing to load
                return Ok(new { success = true, data = Array.Empty<object>() });
            }

            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientJourney_ListByPatient",
                    new[] { new SqlParameter("@Patient_ID", patientId) }
                );

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        journeyId = Convert.ToInt32(r["PatientJourney_ID"]),
                        patientId = r["Patient_ID"]?.ToString(),
                        typeName = r["PjAppType_Name"]?.ToString(),
                        journeyDate = r["PatientJourney_Date"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["PatientJourney_Date"]).ToString("dd/MM/yyyy"),
                        journeyDateRaw = r["PatientJourney_Date"] == DBNull.Value
                            ? ""
                            : Convert.ToDateTime(r["PatientJourney_Date"]).ToString("yyyy-MM-dd"),
                        staffId = r["Staff_ID"]?.ToString(),
                        staffName = r["Staff_Name"]?.ToString(),
                        remarks = r["PatientJourney_Remarks"]?.ToString() ?? ""
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading patient journeys." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveJourney([FromBody] SaveJourneyRequest model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            var patientId = model.PatientId?.Trim() ?? string.Empty;
            var typeName = model.PjAppTypeName?.Trim() ?? string.Empty;
            var dateStr = model.JourneyDate?.Trim() ?? string.Empty;
            var staffId = model.StaffId?.Trim() ?? string.Empty;
            var remarks = model.Remarks?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(patientId) ||
                string.IsNullOrWhiteSpace(typeName) ||
                string.IsNullOrWhiteSpace(dateStr) ||
                string.IsNullOrWhiteSpace(staffId))
            {
                return Ok(new { success = false, message = "Please fill in all mandatory journey fields." });
            }

            if (!DateTime.TryParse(dateStr, out var journeyDate))
            {
                return Ok(new { success = false, message = "Invalid journey date." });
            }

            try
            {
                var parameters = new[]
                {
            new SqlParameter("@Patient_ID",            patientId),
            new SqlParameter("@PjAppType_Name",        typeName),
            new SqlParameter("@PatientJourney_Date",   journeyDate),
            new SqlParameter("@Staff_ID",              staffId),
            new SqlParameter("@PatientJourney_Remarks", (object)remarks ?? DBNull.Value)
        };

                await _db.ExecuteNonQueryAsync("spPatientJourney_Insert", parameters);

                return Ok(new { success = true });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error saving journey." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteJourney([FromBody] DeleteJourneyRequest model)
        {
            if (model == null || model.JourneyId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid journey id." });
            }

            try
            {
                var parameters = new[]
                {
            new SqlParameter("@PatientJourney_ID", model.JourneyId)
        };

                await _db.ExecuteNonQueryAsync("spPatientJourney_Delete", parameters);

                return Ok(new { success = true });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error deleting journey." });
            }
        }

        //------------------------------------------------------
        //DOCUMENTS
        //------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> GetPatientDocumentTypes()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spLU_PatientDocumentType_List",
                    Array.Empty<SqlParameter>()
                );

                var types = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        documentTypeId = r["PatientDocumentType_ID"]?.ToString(),
                        documentTypeName = r["PatientDocumentType_Name"]?.ToString()
                    })
                    .ToList();

                return Ok(new { success = true, data = types });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading patient document types." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPatientDocuments(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return Ok(new { success = true, data = Array.Empty<object>() });
            }

            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientDocument_List",
                    new[] { new SqlParameter("@Patient_ID", patientId) }
                );

                var list = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        documentId = Convert.ToInt32(r["PatientDocument_ID"]),
                        patientId = r["Patient_ID"]?.ToString(),
                        patientName = r["Patient_Name"]?.ToString(),
                        docTypeId = r["PatientDocumentType_ID"]?.ToString(),
                        docTypeName = r["PatientDocumentType_Name"]?.ToString(),
                        fileName = r["FileName"]?.ToString(),
                        filePath = r["FilePath"]?.ToString(),
                        uploadedOn = r["UploadedOn"]?.ToString()
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error loading patient documents." });
            }
        }

        [HttpPost]
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

            try
            {
                var uploadRoot = Path.Combine(_env.WebRootPath, "uploads", "patient");
                if (!Directory.Exists(uploadRoot))
                {
                    Directory.CreateDirectory(uploadRoot);
                }

                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file == null || file.Length == 0) continue;

                    var docTypeId = (docTypeIds != null && i < docTypeIds.Count)
                        ? docTypeIds[i]
                        : string.Empty;

                    var docTypeName = (docTypeNames != null && i < docTypeNames.Count)
                        ? docTypeNames[i]
                        : string.Empty;

                    // sanitize & create unique filename
                    var safeFileName = Path.GetFileName(file.FileName);
                    var uniqueName = $"{Guid.NewGuid():N}_{safeFileName}";
                    var physicalPath = Path.Combine(uploadRoot, uniqueName);

                    await using (var stream = System.IO.File.Create(physicalPath))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var relativePath = $"/uploads/patient/{uniqueName}";
                    var contentType = file.ContentType ?? "application/octet-stream";

                    var parameters = new[]
                    {
                new SqlParameter("@Patient_ID",             patientId),
                new SqlParameter("@Patient_Name",           patientName ?? string.Empty),
                new SqlParameter("@PatientDocumentType_ID", (object)docTypeId ?? DBNull.Value),
                new SqlParameter("@PatientDocumentType_Name", (object)docTypeName ?? DBNull.Value),
                new SqlParameter("@FileName",               safeFileName),
                new SqlParameter("@FilePath",               relativePath),
                new SqlParameter("@ContentType",            contentType)
            };

                    await _db.ExecuteNonQueryAsync("spPatientDocument_Insert", parameters);
                }

                return Ok(new { success = true });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error uploading patient documents." });
            }
        }
    }
}