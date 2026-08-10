using CRC.Data.Data;
using CRC.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRC.Web.Controllers.StaffDashboard
{
    [Authorize(Policy = "StaffOnly")]
    public class StaffDashboardController : Controller
    {
        private readonly IDatabaseData _data;

        public StaffDashboardController(IDatabaseData data)
        {
            _data = data;
        }

        // GET: /StaffDashboard
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // 🔴 THE WHOLE SCOPING OF THIS PAGE. Every one of the three reads below is filtered by this staff
        // id inside its stored procedure, and this is where it comes from: the caller's own StaffId claim,
        // stamped at login from dbo.Users.Staff_ID. It is resolved HERE, in the controller, and passed as
        // an ordinary argument — the data layer must never fill it from a claim of its own. A STAFF account
        // whose row has no Staff_ID gets an error message rather than an unscoped query.
        private string? GetStaffId()
        {
            return User.FindFirst("StaffId")?.Value?.Trim();
        }

        private static string FormatTime(TimeSpan? value)
        {
            if (value is null) return "";
            return value.Value.ToString(@"hh\:mm");
        }

        private static (string display, string sort) FormatDate(DateTime? value)
        {
            if (value is null) return ("", "");
            var dt = value.Value;
            return (dt.ToString("dd/MM/yyyy"), dt.ToString("yyyy-MM-dd"));
        }

        private static List<object> MapAppointmentRows(List<StaffDashboardAppointmentItem> rows)
        {
            return rows
                .Select(r =>
                {
                    var (dateDisplay, dateSort) = FormatDate(r.PatientAppointment_Date);

                    return new
                    {
                        patientAppointmentId = r.PatientAppointment_ID,
                        patientId = r.Patient_ID,
                        patientName = r.Patient_Name ?? "",
                        appointmentType = r.PjAppType_Name ?? "",
                        status = r.PatientAppointment_Status,
                        branchName = r.Branch_Name ?? "",
                        appointmentDate = dateDisplay,
                        appointmentDateSort = dateSort,
                        fromTime = FormatTime(r.PatientAppointment_StartTime),
                        toTime = FormatTime(r.PatientAppointment_EndTime),
                        _sortDate = r.PatientAppointment_Date ?? DateTime.MaxValue,
                        _sortStart = r.PatientAppointment_StartTime ?? TimeSpan.MaxValue
                    };
                })
                // The three procedures already order by these same three keys. This re-sort is redundant
                // and is kept because it is what the page has always done: it also decides where a row with
                // a null date or time lands (last, via MaxValue), which SQL's ORDER BY would put first.
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

                var rows = MapAppointmentRows(await _data.GetStaffTodayAppointmentsAsync(staffId, today));
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

                var rows = MapAppointmentRows(await _data.GetStaffWeekAppointmentsAsync(staffId, fromDate));
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

                // The procedure builds its window with DATEFROMPARTS, which THROWS on a month outside
                // 1-12 rather than returning nothing — so the range check has to happen before the call.
                if (m < 1 || m > 12)
                    return Ok(new { success = false, message = "Invalid month value." });

                var rows = MapAppointmentRows(await _data.GetStaffMonthAppointmentsAsync(staffId, y, m));
                return Ok(new { success = true, data = rows, year = y, month = m });
            }
            catch
            {
                return Ok(new { success = false, message = "Error loading this month's staff appointments." });
            }
        }
    }
}
