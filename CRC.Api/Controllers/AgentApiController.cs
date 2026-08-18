using CRC.Api.Infrastructure;
using CRC.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace CRC.Api.Controllers
{
    // =================================================================================================
    // THE AGENT API — nucentra's one machine-callable surface. Eight endpoints are planned under
    // /api/agent for a single external caller (an n8n WhatsApp workflow); this class currently carries
    // one of them. CoreFlow.md §13.
    //
    // Four attributes on this class do things that no other controller in nucentra does, and every one of
    // them is a deliberate hole in a global default. Read all four before adding an action.
    //
    // 🔴 1. [AllowAnonymous] DISABLES THE GLOBAL AuthorizeFilter FOR THIS CONTROLLER.
    //    Program.cs installs `options.Filters.Add(new AuthorizeFilter())`, so every action in the portal
    //    requires a signed-in user unless it says otherwise. CoreFlow.md §2.2 states that a grep for
    //    AllowAnonymous is a complete audit of the portal's public surface and that it returns TWO lines,
    //    both on AccountController.Login. AFTER THIS CONTROLLER IT RETURNS THREE, AND THIS IS THE THIRD.
    //    That is a deliberate, documented widening of the portal's public surface — not an oversight, and
    //    not something to copy onto the next controller.
    //
    // 🔴 2. AgentApiKeyFilter IS THE ONLY THING CLOSING THAT GAP.
    //    Authentication here is the X-Agent-Key header and nothing else. If the [ServiceFilter] below is
    //    removed, if the AddScoped<AgentApiKeyFilter>() registration in Program.cs is dropped (which
    //    would throw at request time rather than fail open — the one failure here that is loud), or if
    //    the filter is ever edited to continue past a bad key, every endpoint on this controller becomes
    //    an unauthenticated read. The endpoints Prompt 2 adds return patient names, phone numbers,
    //    screening results and clinician schedules, so the cost of that is a patient-data leak. The two
    //    tests that prove the guard — a call with no header and a call with a wrong key, both expecting
    //    401 — belong in every verification pass, not just the one that built the filter.
    //
    // 🔴 3. [IgnoreAntiforgeryToken] DISABLES THE GLOBAL AutoValidateAntiforgeryTokenAttribute.
    //    Program.cs validates every non-GET action in the portal (§2.4). An external caller has no
    //    __Host-CSRF cookie, cannot be issued one, and therefore cannot produce the X-CSRF-TOKEN header
    //    that pairs with it — so without this attribute every POST to this controller returns 400 with no
    //    useful message. This is safe here for the reason antiforgery exists: CSRF is an attack on
    //    AMBIENT credentials, and this controller has none. It authenticates on a header a browser will
    //    never attach by itself.
    //
    // 🔴 4. EVERY WRITE MADE THROUGH THIS CONTROLLER IS AUDITED AS AGENT_SERVICE.
    //    Not as the patient, not as the coordinator who approved the message, and above all not as user
    //    0. AgentApiKeyFilter resolves the seeded AGENT_SERVICE row from dbo.Users PER REQUEST and builds
    //    the ClaimsPrincipal that DatabaseHelper.CurrentUserId reads, so the actor arrives at the 19
    //    audit-actor procedures the same way a logged-in user's would. A missing row is a 503 and no
    //    action runs. See CoreFlow.md §13.3.
    //
    // House style is unchanged from the rest of the portal (§0, §11.3): hand-built camelCase anonymous
    // objects, never a serialized CRC.Data model (§12 #4); catch SqlException then Exception, log both,
    // return AgentErrorResponse.ForUser; no SQL and no procedure name anywhere in this project — every
    // read goes through IDatabaseData (§12 #2).
    // =================================================================================================
    [ApiController]
    [Route("api/agent")]
    [AllowAnonymous]
    [ServiceFilter(typeof(AgentApiKeyFilter))]
    [IgnoreAntiforgeryToken]
    public class AgentApiController : ControllerBase
    {
        private readonly IDatabaseData _data;
        private readonly ILogger<AgentApiController> _logger;

        public AgentApiController(IDatabaseData data, ILogger<AgentApiController> logger)
        {
            _data = data;
            _logger = logger;
        }

        // GET: /api/agent/branches
        //
        // Endpoint 5 of the eight. The agent asks "which hospitals can I offer?" before it asks anything
        // about staff, hours or a patient, so this is the cheapest of the eight and the only one whose
        // worst-case leak is a list of hospital names — which is why the guard was built and proven
        // against it, before any endpoint existed that returns a patient.
        //
        // Calls the EXISTING GetActiveBranchesAsync (spBranch_ListActive), the same method the staff and
        // appointment branch dropdowns use. Active branches only, ordered by branch name; an empty list
        // is a legitimate answer, not an error.
        //
        // 🔴 THE THREE PROPERTY NAMES BELOW ARE A PUBLISHED CONTRACT. branchId / name / state, camelCase,
        // mapped by hand out of BranchOption (Branch_ID / Branch_Name / Branch_State) so that renaming a
        // data-layer property is a compile-time change with no effect on the wire, and a procedure
        // gaining a column does not silently gain a JSON field. `branchId` is the value every other agent
        // endpoint takes back as its branchId argument; `name` and `state` are what the agent says out
        // loud to a patient. CoreFlow.md §13.4 publishes them, and the six endpoints in Prompt 2 follow
        // the same style: the model's noun without its table prefix, lower-camel.
        [HttpGet("branches")]
        public async Task<IActionResult> GetBranches()
        {
            try
            {
                var branches = await _data.GetActiveBranchesAsync();

                return Ok(new
                {
                    success = true,
                    data = branches.Select(b => new
                    {
                        branchId = b.Branch_ID,
                        name = b.Branch_Name,
                        state = b.Branch_State
                    })
                });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException listing active branches for the Agent API.");
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving branches."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error listing active branches for the Agent API.");
                return Ok(AgentErrorResponse.ForUser(HttpContext, "Error retrieving branches."));
            }
        }
    }
}
