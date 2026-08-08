using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CRC.Data.Data;
using CRC.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CRC.Web.Controllers.Staff
{
    [Authorize(Policy = "AdminOrSuperOrStaff")]
    public class StaffPerformanceController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly ILogger<StaffPerformanceController> _logger;

        public StaffPerformanceController(IDatabaseData data, ILogger<StaffPerformanceController> logger)
        {
            _data = data;
            _logger = logger;
        }

        // GET: /StaffPerformance/Get?staffId=...
        [HttpGet]
        public async Task<IActionResult> Get(string staffId)
        {
            var trimmedStaffId = (staffId ?? string.Empty).Trim();

            // ADMIN/SUPERUSER always pass (even with empty staffId, where the empty-state
            // placeholder below is rendered). A pure STAFF user must match their own
            // StaffId claim; empty or mismatched staffIds are rejected.
            if (!User.CanAccessStaff(trimmedStaffId))
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(trimmedStaffId))
            {
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalColonoscopy = 0,
                        totalColonoscopyThisMonth = 0,
                        hoursByType = Array.Empty<object>(),
                        complications = Array.Empty<object>(),
                        anomalies = Array.Empty<object>()
                    }
                });
            }

            try
            {
                // ONE CALL, FOUR RESULT SETS, READ IN THE PROCEDURE'S ORDER inside SqlData — see
                // StaffPerformanceResult. The nulls coerced below are the same ones the DataTable code
                // coerced: grid 1's two SUMs really are NULL for a clinician with no journey rows.
                var performance = await _data.GetStaffPerformanceAsync(trimmedStaffId);

                int totalColonoscopy = performance.TotalColonoscopy ?? 0;
                int totalColonoscopyThisMonth = performance.TotalColonoscopyThisMonth ?? 0;

                var hoursByType = new List<object>();
                foreach (var row in performance.HoursByType)
                {
                    hoursByType.Add(new
                    {
                        pjAppTypeId = row.PjAppType_ID ?? string.Empty,
                        pjAppTypeName = row.PjAppType_Name ?? string.Empty,
                        totalHours = row.TotalHours == null
                            ? 0m
                            : Math.Round(row.TotalHours.Value, 2)
                    });
                }

                var complications = new List<object>();
                foreach (var row in performance.Complications)
                {
                    complications.Add(new
                    {
                        complication = row.Complication ?? string.Empty,
                        total = row.Total ?? 0
                    });
                }

                var anomalies = new List<object>();
                foreach (var row in performance.Anomalies)
                {
                    anomalies.Add(new
                    {
                        typeOfAnomaly = row.TypeOfAnomaly ?? string.Empty,
                        patientCount = row.PatientCount ?? 0
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalColonoscopy,
                        totalColonoscopyThisMonth,
                        hoursByType,
                        complications,
                        anomalies
                    }
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException loading staff performance for StaffId={StaffId}", trimmedStaffId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading staff performance for StaffId={StaffId}", trimmedStaffId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
        }
    }
}
