using CRC.Data.Data;
using CRC.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
// Microsoft.Data.SqlClient survives for SqlException alone — UpdateAppointmentStatus catches it separately
// from Exception, which matters here because spPatientAppointment_UpdateStatus RAISERRORs on an unknown id.
// `using System.Data;` is gone with the last DataTable, along with DatabaseHelper itself (Prompt 6).
using Microsoft.Data.SqlClient;

namespace CRC.Web.Controllers.Appointment
{
    [Authorize(Policy = "AdminOrSuper")]
    public class AppointmentController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AppointmentController> _logger;

        public AppointmentController(IDatabaseData data, IWebHostEnvironment env, ILogger<AppointmentController> logger)
        {
            _data = data;
            _env = env;
            _logger = logger;
        }

        // GET: /Appointment
        [HttpGet]
        public IActionResult Index()
        {
            // view will hold filters + empty table
            return View();
        }

        // GET: /Appointment/GetLookups
        [HttpGet]
        public async Task<IActionResult> GetLookups()
        {
            try
            {
                // 🔴 FOUR OF THE FIVE FILTERS ARE READS OVER dbo.PatientAppointment ITSELF, NOT OVER A
                // LOOKUP TABLE. They answer "what values are actually in use", so a portal with no
                // appointments offers four empty dropdowns, and a branch, patient or clinician that has
                // never been booked never appears. That is what a search filter wants — offering a value
                // that can only return nothing is worse than omitting it — and it is why the branch
                // filter here is spPatientAppointment_LookupBranches and NOT GetActiveBranchesAsync,
                // which is what the appointment FORM uses.
                //
                // The fifth, the appointment type, IS the plain lookup: filtering by type is filtering by
                // a fixed clinical vocabulary, not by what happens to be booked.
                var patientNames = await _data.GetAppointmentPatientNamesAsync();
                var staffNames = await _data.GetAppointmentStaffNamesAsync();
                var statusNames = await _data.GetAppointmentStatusesAsync();
                var appointmentTypes = await _data.GetJourneyAppointmentTypesAsync();
                var branchNames = await _data.GetAppointmentBranchNamesAsync();

                var patients = patientNames
                    .Select(n => new { name = n })
                    .ToList();

                var staff = staffNames
                    .Select(n => new { name = n })
                    .ToList();

                var statuses = statusNames
                    .Select(n => new { name = n })
                    .ToList();

                // 🔴 RE-SORTED BY NAME, AND THAT IS A DELIBERATE DIFFERENCE FROM EVERY OTHER CALLER.
                // spLU_PJ_AppType_List orders by ID because the ids are in clinical sequence
                // (01 PATIENT ASSESSMENT → 02 COLONOSCOPY → 03 FOLLOW UP → 04 SURVEILLANCE), and
                // /Patient/GetAppointmentLookups keeps that order because it fills a booking form. This
                // is a search filter, where alphabetical is what a user scans for, so it sorts. Removing
                // the OrderBy to "match the other endpoint" would change this dropdown's order.
                var types = appointmentTypes
                    .Select(t => new
                    {
                        id = t.Id,
                        name = t.Name
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.id))
                    .OrderBy(x => x.name)
                    .ToList();

                var branches = branchNames
                    .Select(n => new { name = n })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    patients,
                    staff,
                    statuses,
                    types,
                    branches
                });
            }
            catch (Exception)
            {
                return Ok(new
                {
                    success = false,
                    message = "Error loading appointment lookups."
                });
            }
        }

        // DTO for search
        public class AppointmentSearchRequest
        {
            public string? PatientName { get; set; }
            public string? StaffName { get; set; }
            public string? Status { get; set; }
            public string? FromDate { get; set; }   // yyyy-MM-dd
            public string? ToDate { get; set; }     // yyyy-MM-dd
            public string? PjAppTypeName { get; set; }
            public string? BranchName { get; set; }
        }

        // POST: /Appointment/Search
        [HttpPost]
        public async Task<IActionResult> Search([FromBody] AppointmentSearchRequest model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            string patientName = model.PatientName?.Trim() ?? string.Empty;
            string staffName = model.StaffName?.Trim() ?? string.Empty;
            string status = model.Status?.Trim() ?? string.Empty;
            string typeName = model.PjAppTypeName?.Trim() ?? string.Empty;
            string branchName = model.BranchName?.Trim() ?? string.Empty;

            DateTime? fromDate = null;
            DateTime? toDate = null;

            // TryParse, not TryParseExact — this endpoint has always accepted whatever the server's
            // culture will parse, and an unparseable date is silently treated as "no filter" rather than
            // rejected. Both are pre-existing behaviours and both are left as found.
            if (!string.IsNullOrWhiteSpace(model.FromDate) &&
                DateTime.TryParse(model.FromDate, out var fd))
            {
                fromDate = fd.Date;
            }

            if (!string.IsNullOrWhiteSpace(model.ToDate) &&
                DateTime.TryParse(model.ToDate, out var td))
            {
                toDate = td.Date;
            }

            try
            {
                // 🔴 BLANK MUST BECOME NULL, AND THAT CONVERSION IS THE WHOLE OF THE FILTERING.
                // Every predicate in spPatientAppointment_Search is `@X IS NULL OR column = @X`, so
                // sending "" for an unused filter would match only rows whose column is the empty string
                // — i.e. nothing at all. The DataTable code did this with DBNull.Value; a C# null through
                // Dapper is the same parameter value.
                var appointments = await _data.SearchAppointmentsAsync(
                    string.IsNullOrWhiteSpace(patientName) ? null : patientName,
                    string.IsNullOrWhiteSpace(staffName) ? null : staffName,
                    string.IsNullOrWhiteSpace(status) ? null : status,
                    fromDate,
                    toDate,
                    string.IsNullOrWhiteSpace(typeName) ? null : typeName,
                    string.IsNullOrWhiteSpace(branchName) ? null : branchName);

                var list = appointments
                    .Select(a => new
                    {
                        patientAppointmentId = a.PatientAppointment_ID,
                        patientId = a.Patient_ID,

                        // Five columns arrive through LEFT JOINs and each is coerced to "". The
                        // DataTable code got that for free — DBNull.Value.ToString() is "" — so leaving
                        // them null would newly render the word "null" in the results table.
                        patientName = a.Patient_Name ?? string.Empty,
                        patientPhone = a.Patient_Phone ?? string.Empty,
                        patientEmail = a.Patient_Email ?? string.Empty,
                        appointmentType = a.PjAppType_Name ?? string.Empty,

                        status = a.PatientAppointment_Status,

                        staffName = a.Staff_Name ?? string.Empty,
                        branchName = a.Branch_Name ?? string.Empty,

                        // The procedure folds the start time into the date column and keeps the date
                        // column's name, which is why one field renders as "01/09/2026 08:00". Culture-
                        // formatted — no CultureInfo — so the separator is the server's.
                        appointmentDateTime = a.PatientAppointment_Date.HasValue
                            ? a.PatientAppointment_Date.Value.ToString("dd/MM/yyyy HH:mm")
                            : ""
                    })
                    .ToList();

                return Ok(new { success = true, data = list });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error searching appointments." });
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

            // safety: only allow known statuses (match your dropdown values)
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Scheduled",
        "Attended",
        "Not Attended"
    };

            if (!allowed.Contains(status))
                return BadRequest(new { success = false, message = "Invalid status value." });

            try
            {
                // 🔴 THE RESULT IS THE ROW AS IT NOW STANDS, RE-READ BY THE PROCEDURE — not an echo of
                // what was posted. spPatientAppointment_UpdateStatus selects the eight columns back into
                // OUTPUT parameters with a comment saying why: "so callers can audit DB state, not
                // request payload". An audit line that reports the request can be wrong in exactly the
                // case somebody is reading it to investigate.
                //
                // An unknown id does not come back as an empty result — the procedure RAISERRORs
                // 'Appointment not found.', which lands in the SqlException catch below and reaches the
                // user as the generic message plus a correlation id.
                var result = await _data.UpdateAppointmentStatusAsync(model.PatientAppointmentId, status);

                AuditLog.AppointmentUpdated(HttpContext, model.PatientAppointmentId,
                    result.Patient_ID, result.Staff_ID, result.PatientAppointment_Date,
                    result.StartTime, result.EndTime, result.PjAppType_ID, result.Branch_ID,
                    result.PatientAppointment_Status);

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
