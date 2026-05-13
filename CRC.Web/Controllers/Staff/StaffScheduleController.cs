using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using CRC.Data.Database;
using CRC.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CRC.Web.Controllers.Staff
{
    [Authorize(Policy = "AdminOrSuperOrStaff")]
    public class StaffScheduleController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly ILogger<StaffScheduleController> _logger;

        public StaffScheduleController(DatabaseHelper db, ILogger<StaffScheduleController> logger)
        {
            _db = db;
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

        // Resolves the owning Staff_ID for a slot. Returns null when the slot doesn't exist.
        // Used by Delete to enforce ownership without trusting a client-supplied staffId.
        private async Task<string?> GetSlotOwnerAsync(int staffSlotId)
        {
            var parameters = new[]
            {
                new SqlParameter("@StaffSlot_ID", SqlDbType.Int) { Value = staffSlotId }
            };

            var dt = await _db.ExecuteDataTableAsync("dbo.spStaffSlots_GetOwner", parameters);
            if (dt.Rows.Count == 0)
                return null;

            var ownerCol = dt.Columns.Contains("Staff_ID") ? "Staff_ID" : dt.Columns[0].ColumnName;
            var value = dt.Rows[0][ownerCol];
            return value == DBNull.Value ? null : value?.ToString();
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
                var parameters = new[]
                {
            new SqlParameter("@Staff_ID", SqlDbType.VarChar, 100) { Value = staffId },
            new SqlParameter("@FromDate", SqlDbType.Date) { Value = (object?)from ?? DBNull.Value },
            new SqlParameter("@ToDate", SqlDbType.Date) { Value = (object?)to ?? DBNull.Value }
        };

                var dt = await _db.ExecuteDataTableAsync("dbo.spStaffSlots_List", parameters);

                var rows = new List<object>();

                foreach (DataRow r in dt.Rows)
                {
                    rows.Add(new
                    {
                        staffSlotId = r["StaffSlot_ID"] == DBNull.Value ? 0 : Convert.ToInt32(r["StaffSlot_ID"]),
                        slotDate = r["SlotDate"] == DBNull.Value ? "" : Convert.ToDateTime(r["SlotDate"]).ToString("yyyy-MM-dd"),
                        slotStartTime = r["SlotStartTime"]?.ToString() ?? "",
                        slotEndTime = r["SlotEndTime"]?.ToString() ?? "",
                        patientAppointmentId = r["PatientAppointment_ID"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PatientAppointment_ID"])
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
                var parameters = new[]
                {
                    new SqlParameter("@Staff_ID", SqlDbType.VarChar, 100) { Value = model.StaffId },
                    new SqlParameter("@FromDate", SqlDbType.Date) { Value = fromDate.Date },
                    new SqlParameter("@ToDate", SqlDbType.Date) { Value = toDate.Date },
                    new SqlParameter("@StartTime", SqlDbType.Time) { Value = startTime },
                    new SqlParameter("@EndTime", SqlDbType.Time) { Value = endTime }
                };

                var dt = await _db.ExecuteDataTableAsync("dbo.spStaffSlots_CreateRange", parameters);

                int created = 0;
                int skipped = 0;

                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    if (dt.Columns.Contains("CreatedCount") && row["CreatedCount"] != DBNull.Value)
                        created = Convert.ToInt32(row["CreatedCount"]);
                    if (dt.Columns.Contains("SkippedExistingCount") && row["SkippedExistingCount"] != DBNull.Value)
                        skipped = Convert.ToInt32(row["SkippedExistingCount"]);
                }

                AuditLog.StaffSlotRangeCreated(HttpContext, model.StaffId, fromDate.Date, toDate.Date,
                    startTime, endTime, created, skipped);

                return Ok(new
                {
                    success = true,
                    createdCount = created,
                    skippedExistingCount = skipped
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
                var ownerStaffId = await GetSlotOwnerAsync(model.StaffSlotId);
                if (ownerStaffId == null)
                    return Ok(new { success = false, message = "Slot not found." });

                if (!User.CanAccessStaff(ownerStaffId))
                    return Forbid();

                var parameters = new[]
                {
                    new SqlParameter("@StaffSlot_ID", SqlDbType.Int) { Value = model.StaffSlotId }
                };

                await _db.ExecuteNonQueryAsync("dbo.spStaffSlots_Delete", parameters);

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