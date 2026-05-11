using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using CRC.Data.Database;
using CRC.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CRC.Web.Controllers.Staff
{
    [Authorize(Policy = "AdminOrSuperOrStaff")]
    public class StaffPerformanceController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly ILogger<StaffPerformanceController> _logger;

        public StaffPerformanceController(DatabaseHelper db, ILogger<StaffPerformanceController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // GET: /StaffPerformance/Get?staffId=...
        [HttpGet]
        public async Task<IActionResult> Get(string staffId)
        {
            var trimmedStaffId = (staffId ?? string.Empty).Trim();

            if (User.IsInRole("STAFF") && !User.IsInRole("ADMIN") && !User.IsInRole("SUPERUSER"))
            {
                var ownStaffId = User.FindFirst("StaffId")?.Value?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(ownStaffId) ||
                    !string.Equals(ownStaffId, trimmedStaffId, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
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
                var parameters = new[]
                {
                    new SqlParameter("@Staff_ID", SqlDbType.VarChar, 100) { Value = trimmedStaffId }
                };

                var ds = await _db.ExecuteDataSetAsync("dbo.spStaff_GetPerformance", parameters);

                int totalColonoscopy = 0;
                int totalColonoscopyThisMonth = 0;

                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    var r = ds.Tables[0].Rows[0];
                    if (ds.Tables[0].Columns.Contains("TotalColonoscopy") && r["TotalColonoscopy"] != DBNull.Value)
                        totalColonoscopy = Convert.ToInt32(r["TotalColonoscopy"]);
                    if (ds.Tables[0].Columns.Contains("TotalColonoscopyThisMonth") && r["TotalColonoscopyThisMonth"] != DBNull.Value)
                        totalColonoscopyThisMonth = Convert.ToInt32(r["TotalColonoscopyThisMonth"]);
                }

                var hoursByType = new List<object>();
                if (ds.Tables.Count > 1)
                {
                    foreach (DataRow r in ds.Tables[1].Rows)
                    {
                        hoursByType.Add(new
                        {
                            pjAppTypeId = r["PjAppType_ID"] == DBNull.Value ? "" : Convert.ToString(r["PjAppType_ID"]),
                            pjAppTypeName = r["PjAppType_Name"] == DBNull.Value ? "" : Convert.ToString(r["PjAppType_Name"]),
                            totalHours = r["TotalHours"] == DBNull.Value
                                ? 0m
                                : Math.Round(Convert.ToDecimal(r["TotalHours"]), 2)
                        });
                    }
                }

                var complications = new List<object>();
                if (ds.Tables.Count > 2)
                {
                    foreach (DataRow r in ds.Tables[2].Rows)
                    {
                        complications.Add(new
                        {
                            complication = r["Complication"] == DBNull.Value ? "" : Convert.ToString(r["Complication"]),
                            total = r["Total"] == DBNull.Value ? 0 : Convert.ToInt32(r["Total"])
                        });
                    }
                }

                var anomalies = new List<object>();
                if (ds.Tables.Count > 3)
                {
                    foreach (DataRow r in ds.Tables[3].Rows)
                    {
                        anomalies.Add(new
                        {
                            typeOfAnomaly = r["TypeOfAnomaly"] == DBNull.Value ? "" : Convert.ToString(r["TypeOfAnomaly"]),
                            patientCount = r["PatientCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["PatientCount"])
                        });
                    }
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
