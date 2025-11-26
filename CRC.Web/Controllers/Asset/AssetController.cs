using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CRC.Web.Data;

namespace CRC.Web.Controllers
{
    [Authorize]
    public class AssetController : Controller
    {
        private readonly DatabaseHelper _db;

        public AssetController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /Asset
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Asset/GetActiveBranches
        [HttpGet]
        public async Task<IActionResult> GetActiveBranches()
        {
            var dt = await _db.ExecuteDataTableAsync("spBranch_ListActive");
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    branchId = row["Branch_ID"].ToString(),
                    branchName = row["Branch_Name"].ToString()
                });
            }

            return Ok(list);
        }

        // GET: /Asset/GetAssets?branchId=...
        [HttpGet]
        public async Task<IActionResult> GetAssets(string branchId)
        {
            if (string.IsNullOrWhiteSpace(branchId))
            {
                return BadRequest(new { success = false, message = "Branch ID is required." });
            }

            var parameters = new[]
            {
                new SqlParameter("@Branch_ID", branchId)
            };

            var dt = await _db.ExecuteDataTableAsync("spAsset_ListByBranch", parameters);
            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    assetId = row["Asset_ID"].ToString(),
                    name = row["Asset_Name"].ToString(),
                    branchId = row["Branch_ID"].ToString(),
                    branchName = row["Branch_Name"].ToString(),
                    quantity = row["Asset_Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(row["Asset_Quantity"]),
                    cost = row["Asset_Cost"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Asset_Cost"]),
                    totalCost = row["Asset_TotalCost"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Asset_TotalCost"])
                });
            }

            return Ok(new { success = true, data = list });
        }

        // GET: /Asset/GetAsset?assetId=...
        [HttpGet]
        public async Task<IActionResult> GetAsset(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId))
            {
                return BadRequest(new { success = false, message = "Asset ID is required." });
            }

            var parameters = new[]
            {
                new SqlParameter("@Asset_ID", assetId)
            };

            var dt = await _db.ExecuteDataTableAsync("spAsset_GetById", parameters);

            if (dt.Rows.Count == 0)
            {
                return Ok(new { success = false, message = "Asset not found." });
            }

            var row = dt.Rows[0];

            return Ok(new
            {
                success = true,
                data = new
                {
                    assetId = row["Asset_ID"].ToString(),
                    name = row["Asset_Name"].ToString(),
                    branchId = row["Branch_ID"].ToString(),
                    branchName = row["Branch_Name"].ToString(),
                    quantity = row["Asset_Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(row["Asset_Quantity"]),
                    cost = row["Asset_Cost"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Asset_Cost"])
                }
            });
        }

        public class SaveAssetRequest
        {
            public bool IsNew { get; set; }
            public string AssetId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string BranchId { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal Cost { get; set; }
        }

        public class DeleteAssetRequest
        {
            public string AssetId { get; set; } = string.Empty;
        }

        // POST: /Asset/SaveAsset
        [HttpPost]
        public async Task<IActionResult> SaveAsset([FromBody] SaveAssetRequest model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Invalid data." });
            }

            if (string.IsNullOrWhiteSpace(model.AssetId) ||
                string.IsNullOrWhiteSpace(model.Name) ||
                string.IsNullOrWhiteSpace(model.BranchId) ||
                string.IsNullOrWhiteSpace(model.BranchName) ||
                model.Quantity <= 0 ||
                model.Cost < 0)
            {
                return Ok(new { success = false, message = "Please fill in all required fields." });
            }

            var parameters = new[]
            {
                new SqlParameter("@Asset_ID", model.AssetId),
                new SqlParameter("@Asset_Name", model.Name),
                new SqlParameter("@Branch_ID", model.BranchId),
                new SqlParameter("@Branch_Name", model.BranchName),
                new SqlParameter("@Asset_Quantity", model.Quantity),
                new SqlParameter("@Asset_Cost", model.Cost)
            };

            try
            {
                if (model.IsNew)
                {
                    await _db.ExecuteNonQueryAsync("spAsset_Insert", parameters);
                }
                else
                {
                    await _db.ExecuteNonQueryAsync("spAsset_Update", parameters);
                }

                return Ok(new { success = true, message = "Asset saved successfully." });
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

        // POST: /Asset/DeleteAsset
        [HttpPost]
        public async Task<IActionResult> DeleteAsset([FromBody] DeleteAssetRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.AssetId))
            {
                return BadRequest(new { success = false, message = "Asset ID is required." });
            }

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@Asset_ID", model.AssetId)
                };

                await _db.ExecuteNonQueryAsync("spAsset_Delete", parameters);

                return Ok(new { success = true, message = "Asset deleted successfully." });
            }
            catch (Exception)
            {
                return Ok(new { success = false, message = "An unexpected error occurred." });
            }
        }
    }
}