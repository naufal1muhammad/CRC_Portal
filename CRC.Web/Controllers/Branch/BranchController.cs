using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CRC.Web.Data;

namespace CRC.Web.Controllers.Branch
{
    [Authorize]
    public class BranchController : Controller
    {
        private readonly DatabaseHelper _db;

        public BranchController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /Branch
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Branch/GetBranches
        [HttpGet]
        public async Task<IActionResult> GetBranches()
        {
            var dt = await _db.ExecuteDataTableAsync("spBranch_ListAll");
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    branchId = row["Branch_ID"].ToString(),
                    name = row["Branch_Name"].ToString(),
                    location = row["Branch_Location"].ToString(),
                    state = row["Branch_State"].ToString(),
                    status = row["Branch_Status"] == DBNull.Value
                        ? false
                        : Convert.ToBoolean(row["Branch_Status"])
                });
            }

            return Ok(list);
        }

        // GET: /Branch/GetBranch?branchId=...
        [HttpGet]
        public async Task<IActionResult> GetBranch(string branchId)
        {
            if (string.IsNullOrWhiteSpace(branchId))
            {
                return BadRequest(new { success = false, message = "Branch ID is required." });
            }

            var parameters = new[]
            {
                new SqlParameter("@Branch_ID", branchId)
            };

            var dt = await _db.ExecuteDataTableAsync("spBranch_GetById", parameters);

            if (dt.Rows.Count == 0)
            {
                return Ok(new { success = false, message = "Branch not found." });
            }

            var row = dt.Rows[0];

            return Ok(new
            {
                success = true,
                data = new
                {
                    branchId = row["Branch_ID"].ToString(),
                    name = row["Branch_Name"].ToString(),
                    location = row["Branch_Location"].ToString(),
                    state = row["Branch_State"].ToString(),
                    status = row["Branch_Status"] == DBNull.Value
                        ? false
                        : Convert.ToBoolean(row["Branch_Status"])
                }
            });
        }

        public class SaveBranchRequest
        {
            public bool IsNew { get; set; }
            public string BranchId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
            public bool Status { get; set; }
        }

        public class DeleteBranchRequest
        {
            public string BranchId { get; set; } = string.Empty;
        }

        // POST: /Branch/SaveBranch
        [HttpPost]
        public async Task<IActionResult> SaveBranch([FromBody] SaveBranchRequest model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid data." });
            }

            if (string.IsNullOrWhiteSpace(model.BranchId) ||
                string.IsNullOrWhiteSpace(model.Name) ||
                string.IsNullOrWhiteSpace(model.Location) ||
                string.IsNullOrWhiteSpace(model.State))
            {
                return Ok(new { success = false, message = "Please fill in all required fields." });
            }

            var parameters = new[]
            {
                new SqlParameter("@Branch_ID", model.BranchId),
                new SqlParameter("@Branch_Name", model.Name),
                new SqlParameter("@Branch_Location", model.Location),
                new SqlParameter("@Branch_State", model.State),
                new SqlParameter("@Branch_Status", model.Status)
            };

            try
            {
                if (model.IsNew)
                {
                    await _db.ExecuteNonQueryAsync("spBranch_Insert", parameters);
                }
                else
                {
                    await _db.ExecuteNonQueryAsync("spBranch_Update", parameters);
                }

                return Ok(new { success = true, message = "Branch saved successfully." });
            }
            catch (SqlException ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "An unexpected error occurred." });
            }
        }

        // POST: /Branch/DeleteBranch
        [HttpPost]
        public async Task<IActionResult> DeleteBranch([FromBody] DeleteBranchRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.BranchId))
            {
                return BadRequest(new { success = false, message = "Branch ID is required." });
            }

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@Branch_ID", model.BranchId)
                };

                await _db.ExecuteNonQueryAsync("spBranch_Delete", parameters);

                return Ok(new { success = true, message = "Branch deleted successfully." });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "An unexpected error occurred." });
            }
        }
    }
}