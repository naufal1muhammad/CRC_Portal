using CRC.Data.Database;
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

        private static string FormatTime(object value)
        {
            if (value == DBNull.Value || value is null) return "";
            if (value is TimeSpan ts) return ts.ToString(@"hh\:mm");

            // Fallback (in case SQL returns string)
            return value.ToString() ?? "";
        }

        private static (string display, string sort) FormatDate(object value)
        {
            if (value == DBNull.Value || value is null) return ("", "");
            var dt = Convert.ToDateTime(value);
            return (dt.ToString("dd/MM/yyyy"), dt.ToString("yyyy-MM-dd"));
        }

        private static List<object> MapAppointmentRows(DataTable dt)
        {
            return dt.Rows
                .Cast<DataRow>()
                .Select(r =>
                {
                    var dateObj = r["PatientAppointment_Date"];
                    var (dateDisplay, dateSort) = FormatDate(dateObj);

                    var dateForSort = dateObj == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(dateObj);

                    TimeSpan? startTs = r["PatientAppointment_StartTime"] == DBNull.Value ? (TimeSpan?)null : (TimeSpan)r["PatientAppointment_StartTime"];
                    TimeSpan? endTs = r["PatientAppointment_EndTime"] == DBNull.Value ? (TimeSpan?)null : (TimeSpan)r["PatientAppointment_EndTime"];

                    return new
                    {
                        patientAppointmentId = Convert.ToInt32(r["PatientAppointment_ID"]),
                        patientId = r["Patient_ID"]?.ToString() ?? "",
                        patientName = r["Patient_Name"]?.ToString() ?? "",
                        appointmentType = r["PjAppType_Name"]?.ToString() ?? "",
                        status = r["PatientAppointment_Status"]?.ToString() ?? "",
                        branchName = r["Branch_Name"]?.ToString() ?? "",
                        appointmentDate = dateDisplay,
                        appointmentDateSort = dateSort,
                        fromTime = FormatTime(r["PatientAppointment_StartTime"]),
                        toTime = FormatTime(r["PatientAppointment_EndTime"]),
                        _sortDate = dateForSort ?? DateTime.MaxValue,
                        _sortStart = startTs ?? TimeSpan.MaxValue
                    };
                })
                .OrderBy(x => x._sortDate)
                .ThenBy(x => x._sortStart)
                .ThenBy(x => x.patientAppointmentId)
                .Select(x => (object)new
                {
                    x.patientAppointmentId,
                    x.patientId,
                    x.patientName,
                    x.appointmentType,
                    x.status,
                    x.branchName,
                    x.appointmentDate,
                    x.appointmentDateSort,
                    x.fromTime,
                    x.toTime
                })
                .ToList();
        }

        // ----------------------------
        // Card 1: Today's appointments (for logged-in staff)
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

                var rows = MapAppointmentRows(dt);
                return Ok(new { success = true, data = rows });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading today's staff appointments." });
            }
        }

        // ----------------------------
        // Card 2: This week's appointments (rolling next 7 days, for logged-in staff)
        // ----------------------------
        [HttpGet]
        public async Task<IActionResult> GetThisWeekAppointments()
        {
            try
            {
                var staffId = GetStaffId();
                if (string.IsNullOrWhiteSpace(staffId))
                    return Ok(new { success = false, message = "Your user is not linked to a Staff record (StaffId is missing)." });

                var fromDate = DateTime.Today;

                var dt = await _db.ExecuteDataTableAsync(
                    "spStaffDashboard_ThisWeekAppointments",
                    new[]
                    {
                        new SqlParameter("@Staff_ID", SqlDbType.VarChar, 100) { Value = staffId },
                        new SqlParameter("@FromDate", SqlDbType.Date) { Value = fromDate }
                    });

                var rows = MapAppointmentRows(dt);
                return Ok(new { success = true, data = rows });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading this week's staff appointments." });
            }
        }

        // ----------------------------
        // Card 3: This month's appointments (toggle month, for logged-in staff)
        // ----------------------------
        [HttpGet]
        public async Task<IActionResult> GetMonthAppointments(int? year, int? month)
        {
            try
            {
                var staffId = GetStaffId();
                if (string.IsNullOrWhiteSpace(staffId))
                    return Ok(new { success = false, message = "Your user is not linked to a Staff record (StaffId is missing)." });

                var y = year ?? DateTime.Today.Year;
                var m = month ?? DateTime.Today.Month;

                if (m < 1 || m > 12)
                    return Ok(new { success = false, message = "Invalid month value." });

                var dt = await _db.ExecuteDataTableAsync(
                    "spStaffDashboard_ThisMonthAppointments",
                    new[]
                    {
                        new SqlParameter("@Staff_ID", SqlDbType.VarChar, 100) { Value = staffId },
                        new SqlParameter("@Year", SqlDbType.Int) { Value = y },
                        new SqlParameter("@Month", SqlDbType.Int) { Value = m }
                    });

                var rows = MapAppointmentRows(dt);
                return Ok(new { success = true, data = rows, year = y, month = m });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading this month's staff appointments." });
            }
        }
    }
}