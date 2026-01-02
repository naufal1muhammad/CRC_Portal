using CRC.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CRC.Web.Controllers.StaffDashboard
{
    [Authorize(Policy = "StaffOnly")]
    public class StaffDashboardController : Controller
    {
        private readonly DatabaseHelper _db;

        public StaffDashboardController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /StaffDashboard
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        private string? GetStaffId()
        {
            return User.FindFirst("StaffId")?.Value?.Trim();
        }

        // ----------------------------
        // Row 1: Today's appointments (for logged-in staff)
        // ----------------------------
        [HttpGet]
        public async Task<IActionResult> GetTodayAppointments()
        {
            try
            {
                var staffId = GetStaffId();
                if (string.IsNullOrWhiteSpace(staffId))
                    return Ok(new { success = false, message = "Your user is not linked to a Staff record (StaffId is missing)." });

                var today = DateTime.Today;

                var dt = await _db.ExecuteDataTableAsync(
                    "spStaffDashboard_TodayAppointments",
                    new[]
                    {
                        new SqlParameter("@Staff_ID", SqlDbType.VarChar, 100) { Value = staffId },
                        new SqlParameter("@ForDate", SqlDbType.Date) { Value = today }
                    });

                var rows = dt.Rows.Cast<DataRow>()
                    .Select(r =>
                    {
                        var apptDt = r["PatientAppointment_Date"] == DBNull.Value
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
                            appointmentDateTime = apptDt.HasValue ? apptDt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                            _sort = apptDt ?? DateTime.MaxValue
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
                return Ok(new { success = false, message = "Error loading today's staff appointments." });
            }
        }

        // ----------------------------
        // Row 2: Week appointments for calendar (for logged-in staff)
        // ----------------------------
        [HttpGet]
        public async Task<IActionResult> GetWeekAppointments()
        {
            try
            {
                var staffId = GetStaffId();
                if (string.IsNullOrWhiteSpace(staffId))
                    return Ok(new { success = false, message = "Your user is not linked to a Staff record (StaffId is missing)." });

                var today = DateTime.Today;

                // Monday-based week start
                var diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                var weekStart = today.AddDays(-diff);
                var weekEnd = weekStart.AddDays(7);

                var dt = await _db.ExecuteDataTableAsync(
                    "spStaffDashboard_AppointmentsByRange",
                    new[]
                    {
                        new SqlParameter("@Staff_ID", SqlDbType.VarChar, 100) { Value = staffId },
                        new SqlParameter("@StartDate", SqlDbType.DateTime) { Value = weekStart },
                        new SqlParameter("@EndDate", SqlDbType.DateTime) { Value = weekEnd }
                    });

                // Default duration = 30 mins.
                var events = dt.Rows.Cast<DataRow>()
                    .Select(r =>
                    {
                        var start = r["PatientAppointment_Date"] == DBNull.Value
                            ? (DateTime?)null
                            : Convert.ToDateTime(r["PatientAppointment_Date"]);

                        var apptType = r["PjAppType_Name"]?.ToString() ?? "";
                        var patient = r["Patient_Name"]?.ToString() ?? "";
                        var branch = r["Branch_Name"]?.ToString() ?? "";

                        return new
                        {
                            id = Convert.ToInt32(r["PatientAppointment_ID"]),
                            patientId = r["Patient_ID"]?.ToString() ?? "",
                            start = start?.ToString("yyyy-MM-ddTHH:mm:ss"),
                            end = start?.AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ss"),
                            appointmentType = apptType,
                            patientName = patient,
                            branchName = branch,
                            title = $"{patient} • {apptType} • {branch}"
                        };
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.start))
                    .ToList();

                return Ok(new { success = true, data = events });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading staff week appointments." });
            }
        }
    }
}