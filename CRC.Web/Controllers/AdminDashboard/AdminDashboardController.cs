using CRC.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRC.Web.Controllers.AdminDashboard
{
    [Authorize(Policy = "AdminOrSuper")]
    public class AdminDashboardController : Controller
    {
        private readonly DatabaseHelper _db;

        public AdminDashboardController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /AdminDashboard
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetBranches()
        {
            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spPatientAppointment_LookupBranches",
                    Array.Empty<SqlParameter>());

                var items = dt.Rows.Cast<DataRow>()
                    .Select(r => new
                    {
                        name = r["Branch_Name"]?.ToString() ?? ""
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.name))
                    .OrderBy(x => x.name)
                    .ToList();

                return Ok(new { success = true, data = items });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading branches." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTodayAppointments(string? branchName)
        {
            try
            {
                var today = DateTime.Today;

                var parameters = new[]
                {
                    new SqlParameter("@PatientName", SqlDbType.VarChar, 100) { Value = DBNull.Value },
                    new SqlParameter("@StaffName", SqlDbType.VarChar, 100)   { Value = DBNull.Value },
                    new SqlParameter("@Status", SqlDbType.VarChar, 100)      { Value = DBNull.Value },
                    new SqlParameter("@FromDate", SqlDbType.Date)            { Value = today },
                    new SqlParameter("@ToDate", SqlDbType.Date)              { Value = today },
                    new SqlParameter("@PjAppTypeName", SqlDbType.VarChar, 100){ Value = DBNull.Value },
                    new SqlParameter("@BranchName", SqlDbType.VarChar, 100)
                    {
                        Value = string.IsNullOrWhiteSpace(branchName) ? (object)DBNull.Value : branchName.Trim()
                    }
                };

                var dt = await _db.ExecuteDataTableAsync("spPatientAppointment_Search", parameters);

                var rows = dt.Rows.Cast<DataRow>()
                    .Select(r =>
                    {
                        var dtVal = r["PatientAppointment_Date"] == DBNull.Value
                            ? (DateTime?)null
                            : Convert.ToDateTime(r["PatientAppointment_Date"]);

                        return new
                        {
                            patientAppointmentId = Convert.ToInt32(r["PatientAppointment_ID"]),
                            patientId = r["Patient_ID"]?.ToString() ?? "",
                            patientName = r["Patient_Name"]?.ToString() ?? "",
                            patientPhone = r["Patient_Phone"]?.ToString() ?? "",
                            patientEmail = r["Patient_Email"]?.ToString() ?? "",
                            appointmentType = r["PjAppType_Name"]?.ToString() ?? "",
                            status = r["PatientAppointment_Status"]?.ToString() ?? "",
                            staffName = r["Staff_Name"]?.ToString() ?? "",
                            branchName = r["Branch_Name"]?.ToString() ?? "",
                            appointmentDateTime = dtVal.HasValue ? dtVal.Value.ToString("dd/MM/yyyy HH:mm") : "",
                            _sort = dtVal ?? DateTime.MaxValue
                        };
                    })
                    .OrderBy(x => x._sort)
                    .Select(x => new
                    {
                        x.patientAppointmentId,
                        x.patientId,
                        x.patientName,
                        x.patientPhone,
                        x.patientEmail,
                        x.appointmentType,
                        x.status,
                        x.staffName,
                        x.branchName,
                        x.appointmentDateTime
                    })
                    .ToList();

                return Ok(new { success = true, data = rows });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading today's appointments." });
            }
        }

        public class UpdateAppointmentStatusRequest
        {
            public int PatientAppointmentId { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAppointmentStatus([FromBody] UpdateAppointmentStatusRequest model)
        {
            if (model == null || model.PatientAppointmentId <= 0 || string.IsNullOrWhiteSpace(model.Status))
                return BadRequest(new { success = false, message = "Invalid request." });

            var status = model.Status.Trim();

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Scheduled", "Attended", "Not Attended"
            };

            if (!allowed.Contains(status))
                return BadRequest(new { success = false, message = "Invalid status value." });

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@PatientAppointment_ID", model.PatientAppointmentId),
                    new SqlParameter("@PatientAppointment_Status", status)
                };

                await _db.ExecuteNonQueryAsync("spPatientAppointment_UpdateStatus", parameters);

                return Ok(new { success = true });
            }
            catch (SqlException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch
            {
                return Ok(new { success = false, message = "Error updating appointment status." });
            }
        }
    }
}
