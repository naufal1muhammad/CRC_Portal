using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CRC.Data.Data;
using CRC.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CRC.Web.Controllers.Staff
{
    [Authorize(Policy = "AdminOrSuperOrStaff")]
    public class StaffScheduleController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly ILogger<StaffScheduleController> _logger;

        public StaffScheduleController(IDatabaseData data, ILogger<StaffScheduleController> logger)
        {
            _data = data;
            _logger = logger;
        }

        public sealed class CreateRangeRequest
        {
            public string StaffId { get; set; } = string.Empty;
            public string FromDate { get; set; } = string.Empty; // yyyy-MM-dd
            public string ToDate { get; set; } = string.Empty;   // yyyy-MM-dd
            public string StartTime { get; set; } = string.Empty; // HH:mm
            public string EndTime { get; set; } = string.Empty;   // HH:mm
        }

        public sealed class DeleteSlotRequest
        {
            public int StaffSlotId { get; set; }
        }

        // GET: /StaffSchedule/List?staffId=...&fromDate=yyyy-MM-dd&toDate=yyyy-MM-dd
        [HttpGet]
        public async Task<IActionResult> List(string staffId, string? fromDate = null, string? toDate = null)
        {
            if (string.IsNullOrWhiteSpace(staffId))
                return Ok(new { success = true, data = Array.Empty<object>() });

            if (!User.CanAccessStaff(staffId))
                return Forbid();

            DateTime? from = null;
            DateTime? to = null;

            if (!string.IsNullOrWhiteSpace(fromDate))
            {
                if (!DateTime.TryParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
                    return Ok(new { success = false, message = "Invalid From Date." });

                from = d1.Date;
            }

            if (!string.IsNullOrWhiteSpace(toDate))
            {
                if (!DateTime.TryParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
                    return Ok(new { success = false, message = "Invalid To Date." });

                to = d2.Date;
            }

            try
            {
                var slots = await _data.GetStaffSlotsAsync(staffId, from, to);

                var rows = new List<object>();

                foreach (var slot in slots)
                {
                    rows.Add(new
                    {
                        staffSlotId = slot.StaffSlot_ID,
                        slotDate = slot.SlotDate.ToString("yyyy-MM-dd"),

                        // The two times are already "09:00" strings — spStaffSlots_List CONVERTs the TIME(0)
                        // columns to VARCHAR(5) — so they go out verbatim. Do not parse and re-format them.
                        slotStartTime = slot.SlotStartTime,
                        slotEndTime = slot.SlotEndTime,

                        // Null means the hour is still open; the grid renders it as available.
                        patientAppointmentId = slot.PatientAppointment_ID
                    });
                }

                return Ok(new { success = true, data = rows });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException listing staff schedule for StaffId={StaffId}", staffId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error listing staff schedule for StaffId={StaffId}", staffId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
        }

        // POST: /StaffSchedule/CreateRange
        [HttpPost]
        public async Task<IActionResult> CreateRange([FromBody] CreateRangeRequest model)
        {
            if (model == null)
                return BadRequest(new { success = false, message = "Invalid data." });

            if (string.IsNullOrWhiteSpace(model.StaffId) ||
                string.IsNullOrWhiteSpace(model.FromDate) ||
                string.IsNullOrWhiteSpace(model.ToDate) ||
                string.IsNullOrWhiteSpace(model.StartTime) ||
                string.IsNullOrWhiteSpace(model.EndTime))
            {
                return Ok(new { success = false, message = "Please fill in all required fields." });
            }

            if (!User.CanAccessStaff(model.StaffId))
                return Forbid();

            if (!DateTime.TryParseExact(model.FromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate) ||
                !DateTime.TryParseExact(model.ToDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            {
                return Ok(new { success = false, message = "Invalid date range." });
            }

            if (!TimeSpan.TryParseExact(model.StartTime, @"hh\:mm", CultureInfo.InvariantCulture, out var startTime) ||
                !TimeSpan.TryParseExact(model.EndTime, @"hh\:mm", CultureInfo.InvariantCulture, out var endTime))
            {
                return Ok(new { success = false, message = "Invalid time range." });
            }


            try
            {
                // The procedure's own rules — a range over 31 days, ToDate before FromDate, EndTime at or
                // before StartTime, a time that is not on the hour — are NOT checked above. They THROW, and
                // land in the SqlException catch below as the generic error. That split predates this
                // migration and is left as found.
                var created = await _data.CreateStaffSlotRangeAsync(
                    model.StaffId, fromDate.Date, toDate.Date, startTime, endTime);

                AuditLog.StaffSlotRangeCreated(HttpContext, model.StaffId, fromDate.Date, toDate.Date,
                    startTime, endTime, created.CreatedCount, created.SkippedExistingCount);

                return Ok(new
                {
                    success = true,
                    createdCount = created.CreatedCount,
                    skippedExistingCount = created.SkippedExistingCount
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException creating staff slot range for StaffId={StaffId}", model.StaffId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating staff slot range for StaffId={StaffId}", model.StaffId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
        }

        // POST: /StaffSchedule/Delete
        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] DeleteSlotRequest model)
        {
            if (model == null || model.StaffSlotId <= 0)
                return BadRequest(new { success = false, message = "Invalid slot." });

            try
            {
                // Ownership check: resolve the owning Staff_ID server-side rather than
                // trusting any client-supplied identifier. Without this, a STAFF user could
                // enumerate StaffSlot_ID (sequential PK) and delete other staff's slots.
                var ownerStaffId = await _data.GetStaffSlotOwnerAsync(model.StaffSlotId);
                if (ownerStaffId == null)
                    return Ok(new { success = false, message = "Slot not found." });

                if (!User.CanAccessStaff(ownerStaffId))
                    return Forbid();

                await _data.DeleteStaffSlotAsync(model.StaffSlotId);

                AuditLog.StaffSlotDeleted(HttpContext, model.StaffSlotId);

                return Ok(new { success = true });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException deleting staff slot StaffSlotId={StaffSlotId}", model.StaffSlotId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting staff slot StaffSlotId={StaffSlotId}", model.StaffSlotId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
        }
    }
}
