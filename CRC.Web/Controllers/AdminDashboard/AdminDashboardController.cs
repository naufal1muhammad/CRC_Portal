using CRC.Data.Data;
using CRC.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
// Microsoft.Data.SqlClient survives for SqlException alone — UpdateAppointmentStatus catches it separately
// from Exception, which matters here because spPatientAppointment_UpdateStatus RAISERRORs on an unknown id.
// `using System.Data;` is gone with the last DataTable, along with DatabaseHelper itself (Prompt 6).
using Microsoft.Data.SqlClient;

namespace CRC.Web.Controllers.AdminDashboard
{
    [Authorize(Policy = "AdminOrSuper")]
    public class AdminDashboardController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly ILogger<AdminDashboardController> _logger;

        public AdminDashboardController(IDatabaseData data, ILogger<AdminDashboardController> logger)
        {
            _data = data;
            _logger = logger;
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
                // The same branch filter /Appointment/GetLookups uses — branches that have at least one
                // appointment and are still active — not the branch dropdown from the booking form. A
                // branch nobody has ever been booked into would filter this dashboard down to nothing.
                var branchNames = await _data.GetAppointmentBranchNamesAsync();

                var items = branchNames
                    .Select(n => new { name = n })
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

                // spPatientAppointment_Search, shared with /Appointment/Search, with every filter left
                // null except the date range — pinned to today at both ends — and the optional branch.
                // Null means "do not filter" in that procedure, so the nulls are what make this "every
                // appointment today" rather than "no appointments at all".
                //
                // 🔴 "TODAY" IS THE WEB SERVER'S DateTime.Today, not the database's. On one machine the
                // two agree; split across an App Service and Azure SQL they agree only as well as the
                // two clocks and time zones do, and this panel is the place that would show it first.
                var appointments = await _data.SearchAppointmentsAsync(
                    null,
                    null,
                    null,
                    today,
                    today,
                    null,
                    string.IsNullOrWhiteSpace(branchName) ? null : branchName.Trim());

                var rows = appointments
                    .Select(a => new
                    {
                        patientAppointmentId = a.PatientAppointment_ID,
                        patientId = a.Patient_ID,

                        // Five LEFT JOIN columns, each coerced to "" — the DataTable code got that for
                        // free, since DBNull.Value.ToString() is "".
                        patientName = a.Patient_Name ?? "",
                        patientPhone = a.Patient_Phone ?? "",
                        patientEmail = a.Patient_Email ?? "",
                        appointmentType = a.PjAppType_Name ?? "",

                        status = a.PatientAppointment_Status,

                        staffName = a.Staff_Name ?? "",
                        branchName = a.Branch_Name ?? "",

                        appointmentDateTime = a.PatientAppointment_Date.HasValue
                            ? a.PatientAppointment_Date.Value.ToString("dd/MM/yyyy HH:mm")
                            : "",

                        // 🔴 THE RE-SORT IS DELIBERATE AND IT REVERSES THE PROCEDURE. The procedure
                        // orders date DESC (newest day first); this panel shows one day and wants it in
                        // clock order, so it sorts ASCENDING on the composed start datetime. A null date
                        // sorts LAST via DateTime.MaxValue rather than first — hence the sort key, which
                        // is then projected away so it never reaches the JSON.
                        _sort = a.PatientAppointment_Date ?? DateTime.MaxValue
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
                // The same method /Appointment/UpdateAppointmentStatus calls, and the result is
                // DISCARDED here on purpose: this endpoint has never written an AuditLog line, only the
                // dbo.AuditTrails row the procedure writes itself. Adding one would be a new audit event,
                // not a migration. Requesting the OUTPUT parameters costs nothing — they all declare
                // defaults and the procedure fills them either way — which is why the two controllers
                // share one method rather than growing a second that differs only in what it throws away.
                await _data.UpdateAppointmentStatusAsync(model.PatientAppointmentId, status);

                return Ok(new { success = true });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException updating appointment status PatientAppointmentId={Id} Status={Status}", model.PatientAppointmentId, status);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error updating appointment status."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating appointment status PatientAppointmentId={Id} Status={Status}", model.PatientAppointmentId, status);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error updating appointment status."));
            }
        }
    }
}
