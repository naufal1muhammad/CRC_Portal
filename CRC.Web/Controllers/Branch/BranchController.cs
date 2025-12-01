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
                        : Convert.ToBoolean(row["Branch_Status"]),
                    organizationId = row["Organization_ID"].ToString(),
                    organizationName = row["Organization_Name"].ToString()
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
                        : Convert.ToBoolean(row["Branch_Status"]),
                    organizationId = row["Organization_ID"].ToString(),
                    organizationName = row["Organization_Name"].ToString()
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
            public string OrganizationId { get; set; } = string.Empty;
            public string OrganizationName { get; set; } = string.Empty;
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
                return Ok(new { success = false, message = "Invalid request." });
            }

            // Basic validation
            if (string.IsNullOrWhiteSpace(model.Name) ||
                string.IsNullOrWhiteSpace(model.State) ||
                string.IsNullOrWhiteSpace(model.OrganizationId))
            {
                return Ok(new { success = false, message = "Please fill in branch name, state and organization." });
            }

            try
            {
                if (model.IsNew)
                {
                    // INSERT: Branch_ID is auto-generated in spBranch_Insert
                    var insertParams = new[]
                    {
                new SqlParameter("@Branch_Name", model.Name),
                new SqlParameter("@Branch_Location", (object?)model.Location ?? DBNull.Value),
                new SqlParameter("@Branch_State", (object?)model.State ?? DBNull.Value),
                new SqlParameter("@Branch_Status", (object?)model.Status ?? DBNull.Value),
                new SqlParameter("@Organization_ID", (object?)model.OrganizationId ?? DBNull.Value),
                new SqlParameter("@Organization_Name", (object?)model.OrganizationName ?? DBNull.Value)
            };

                    // spBranch_Insert ends with: SELECT @Branch_ID AS NewBranch_ID;
                    var dt = await _db.ExecuteDataTableAsync("spBranch_Insert", insertParams);

                    string newBranchId = string.Empty;
                    if (dt.Rows.Count > 0 && dt.Columns.Contains("NewBranch_ID"))
                    {
                        newBranchId = dt.Rows[0]["NewBranch_ID"]?.ToString() ?? "";
                    }

                    return Ok(new
                    {
                        success = true,
                        message = "Branch created successfully.",
                        branchId = newBranchId
                    });
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(model.BranchId))
                    {
                        return Ok(new { success = false, message = "Branch ID is required for update." });
                    }

                    var updateParams = new[]
                    {
                new SqlParameter("@Branch_ID", model.BranchId),
                new SqlParameter("@Branch_Name", model.Name),
                new SqlParameter("@Branch_Location", (object?)model.Location ?? DBNull.Value),
                new SqlParameter("@Branch_State", (object?)model.State ?? DBNull.Value),
                new SqlParameter("@Branch_Status", (object?)model.Status ?? DBNull.Value),
                new SqlParameter("@Organization_ID", (object?)model.OrganizationId ?? DBNull.Value),
                new SqlParameter("@Organization_Name", (object?)model.OrganizationName ?? DBNull.Value)
            };

                    await _db.ExecuteNonQueryAsync("spBranch_Update", updateParams);

                    return Ok(new
                    {
                        success = true,
                        message = "Branch updated successfully.",
                        branchId = model.BranchId
                    });
                }
            }
            catch (SqlException ex)
            {
                // You can log ex here
                return Ok(new { success = false, message = ex.Message });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "Error saving branch." });
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

        [HttpGet]
        public async Task<IActionResult> GetStates()
        {
            var dt = await _db.ExecuteDataTableAsync("spLU_STATES_List");
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    stateId = row["State_ID"] == DBNull.Value ? 0 : Convert.ToInt32(row["State_ID"]),
                    stateName = row["State_Name"]?.ToString() ?? string.Empty
                });
            }

            return Ok(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrganizations()
        {
            var dt = await _db.ExecuteDataTableAsync("spLU_ORGANIZATION_List");
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    organizationId = row["Organization_ID"]?.ToString() ?? "",
                    organizationName = row["Organization_Name"]?.ToString() ?? ""
                });
            }
            return Ok(list);
        }

    }
}