using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CRC.Data.Data;
using CRC.Web.Infrastructure;

namespace CRC.Web.Controllers.Branch
{
    [Authorize(Policy = "SuperUserOnly")]
    public class BranchController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly ILogger<BranchController> _logger;

        public BranchController(IDatabaseData data, ILogger<BranchController> logger)
        {
            _data = data;
            _logger = logger;
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
            var branches = await _data.GetAllBranchesAsync();
            var list = new List<object>();

            // The anonymous object below is a public contract: wwwroot/js/branch/index.js reads every one
            // of these camelCase names. The model is mapped INTO it and never returned directly.
            foreach (var branch in branches)
            {
                list.Add(new
                {
                    branchId = branch.Branch_ID,
                    name = branch.Branch_Name,
                    location = branch.Branch_Location,
                    state = branch.Branch_State,
                    // Branch_Status is BIT NOT NULL, but this endpoint has always coerced a null to false
                    // rather than failing. Keep the coercion: a null must still serialize as false.
                    status = branch.Branch_Status ?? false,
                    // Organization_ID / Organization_Name ARE nullable on dbo.Branch. The DataTable code
                    // this replaced returned "" for them, because DBNull.Value.ToString() is "". Without
                    // these two coalesces a legacy row with a null organization serializes as null here
                    // and the branch table renders "null".
                    organizationId = branch.Organization_ID ?? string.Empty,
                    organizationName = branch.Organization_Name ?? string.Empty
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

            var branch = await _data.GetBranchByIdAsync(branchId);

            if (branch == null)
            {
                return Ok(new { success = false, message = "Branch not found." });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    branchId = branch.Branch_ID,
                    name = branch.Branch_Name,
                    location = branch.Branch_Location,
                    state = branch.Branch_State,
                    status = branch.Branch_Status ?? false,
                    organizationId = branch.Organization_ID ?? string.Empty,
                    organizationName = branch.Organization_Name ?? string.Empty
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
                    // INSERT: Branch_ID is auto-generated in spBranch_Insert, which returns it.
                    var newBranchId = await _data.CreateBranchAsync(
                        model.Name,
                        model.Location,
                        model.State,
                        model.Status,
                        model.OrganizationId,
                        model.OrganizationName);

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

                    await _data.UpdateBranchAsync(
                        model.BranchId,
                        model.Name,
                        model.Location,
                        model.State,
                        model.Status,
                        model.OrganizationId,
                        model.OrganizationName);

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
                _logger.LogError(ex, "SqlException saving branch BranchId={BranchId}", model?.BranchId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving branch."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving branch BranchId={BranchId}", model?.BranchId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Error saving branch."));
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
                await _data.DeleteBranchAsync(model.BranchId);

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
            var states = await _data.GetStatesAsync();
            var list = new List<object>();

            foreach (var state in states)
            {
                list.Add(new
                {
                    // LU_LOCATION.LocationId is INT IDENTITY NOT NULL, so the DBNull-to-0 guard the
                    // DataTable version carried here could never fire. stateId stays a JSON number.
                    stateId = state.LocationId,
                    stateName = state.Name
                });
            }

            return Ok(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrganizations()
        {
            var organizations = await _data.GetOrganizationsAsync();
            var list = new List<object>();

            foreach (var organization in organizations)
            {
                list.Add(new
                {
                    organizationId = organization.Id,
                    organizationName = organization.Name
                });
            }
            return Ok(list);
        }

    }
}
