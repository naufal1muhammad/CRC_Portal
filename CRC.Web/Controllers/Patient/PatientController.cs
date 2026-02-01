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
            catch (Exception)
            {
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

            // Helpers for IN (...) parameterization
            static (string InClause, SqlParameter[] Parameters) BuildInClause(string paramPrefix, IReadOnlyList<int> ids)
            {
                var p = new List<SqlParameter>();
                var names = new List<string>();

                for (int i = 0; i < ids.Count; i++)
                {
                    var name = $"@{paramPrefix}{i}";
                    names.Add(name);
                    p.Add(new SqlParameter(name, SqlDbType.Int) { Value = ids[i] });
                }

                return (string.Join(",", names), p.ToArray());
            }

            try
            {
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                using var tx = conn.BeginTransaction();

                // Load selected slots (typed TIME values) inside the same TX
                var (inClause, inParams) = BuildInClause("sid", slotIds);

                var sqlSlots =
                    $"SELECT StaffSlot_ID, Staff_ID, SlotDate, SlotStartTime, SlotEndTime, PatientAppointment_ID " +
                    $"FROM dbo.StaffSlots WHERE StaffSlot_ID IN ({inClause})";

                var slots = new List<SlotInfo>();

                using (var cmdSlots = new SqlCommand(sqlSlots, conn, tx))
                {
                    cmdSlots.Parameters.AddRange(inParams);

                    using var reader = await cmdSlots.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        slots.Add(new SlotInfo
                        {
                            StaffSlotId = reader.GetInt32(0),
                            StaffId = reader.GetString(1),
                            SlotDate = reader.GetDateTime(2),
                            SlotStartTime = (TimeSpan)reader.GetValue(3),
                            SlotEndTime = (TimeSpan)reader.GetValue(4),
                            PatientAppointmentId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5)
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
                    using (var cmdClear = new SqlCommand(
                               "UPDATE dbo.StaffSlots SET PatientAppointment_ID = NULL WHERE PatientAppointment_ID = @ApptId",
                               conn, tx))
                    {
                        cmdClear.Parameters.Add(new SqlParameter("@ApptId", SqlDbType.Int) { Value = appointmentId });
                        await cmdClear.ExecuteNonQueryAsync();
                    }
                }

                // Assign selected slots to the appointment
                var (inClause2, inParams2) = BuildInClause("sid2", slotIds);
                var sqlAssign = $"UPDATE dbo.StaffSlots SET PatientAppointment_ID = @ApptId WHERE StaffSlot_ID IN ({inClause2})";

                using (var cmdAssign = new SqlCommand(sqlAssign, conn, tx))
                {
                    cmdAssign.Parameters.Add(new SqlParameter("@ApptId", SqlDbType.Int) { Value = finalAppointmentId });
                    cmdAssign.Parameters.AddRange(inParams2);
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

        public class DeletePatientDocumentRequest
        {
            public int DocumentId { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> DeletePatientDocument([FromBody] DeletePatientDocumentRequest model)
        {
            if (model == null || model.DocumentId <= 0)
            {
                return Ok(new { success = false, message = "Invalid document ID." });
            }

            try
            {
                var parameters = new[]
                {
            new SqlParameter("@PatientDocument_ID", model.DocumentId)
        };

                await _db.ExecuteNonQueryAsync("spPatientDocument_Delete", parameters);

                return Ok(new { success = true });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error deleting patient document." });
            }
        }
    }
}