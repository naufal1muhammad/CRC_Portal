using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace CRC.Data.Data
{
    // =================================================================================================
    // Two things, and deliberately only two: a connection factory for SqlData, and the current user's id.
    //
    // 🔴 WHAT USED TO BE HERE, AND WHY IT IS NOT ANY MORE.
    //
    // Until the Dapper migration this class was the whole data-access layer, and it carried an ADO
    // surface — ExecuteNonQueryAsync, ExecuteDataTableAsync, ExecuteDataSetAsync and
    // CreateStoredProcedureCommandAsync — that all sixteen controllers called with hand-built
    // SqlParameter[] arrays. Alongside it sat a piece of machinery worth naming, because it was clever
    // and it was invisible:
    //
    //     Before executing ANY command, the helper queried sys.parameters (behind a static
    //     ConcurrentDictionary cache) to ask "does this stored procedure declare a parameter called
    //     @User_ID?" — and if it did, it silently appended the caller's ClaimTypes.NameIdentifier value.
    //     That is how dbo.AuditTrails learned who performed a write. No controller ever passed an actor;
    //     it arrived by magic.
    //
    // All of it — the four ADO methods, TryInjectUserIdAsync, SupportsUserIdParameterAsync,
    // NormalizeStoredProcedure, HasParameter and the _userIdParamSupportCache dictionary — WAS DELETED in
    // this migration, once every controller had moved to IDatabaseData. Dapper sends exactly the
    // properties of the anonymous parameter object and nothing else, so there was no hook left to hang
    // the injection on.
    //
    // 🔴 @User_ID IS NOW ALWAYS PASSED EXPLICITLY, in SqlData, per call, with a comment on each of the 19
    // actor procedures saying so. DO NOT RE-INTRODUCE THE AUTO-INJECTION. It cannot see the difference
    // between the two meanings @User_ID carries in this codebase — the ACTOR (19 procedures, declared
    // `@User_ID INT = NULL`, the audit trail's "who") and a TARGET USER ROW (5 spUsers_* procedures,
    // declared `@User_ID INT` with no default, an ordinary argument). Auto-filling spUsers_Unlock's
    // @User_ID from the caller's claim would unlock the administrator's own account and leave the locked
    // user locked, with a success message. See CoreFlow.md §0.1 and §12.
    //
    // If you are reading old git history and wondering where a controller's stored-procedure call went:
    // it is in CRC.Data/Data/SqlData.cs, which is now the only place in the solution that names one.
    // =================================================================================================
    public class DatabaseHelper
    {
        private readonly string _connectionString;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DatabaseHelper(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _connectionString = configuration.GetConnectionString("CRC_DB")
                ?? throw new InvalidOperationException("Connection string 'CRC_DB' not found.");

            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// A closed <see cref="SqlConnection"/> for <see cref="SqlData"/> to run a stored procedure on.
        /// </summary>
        /// <remarks>
        /// The return type is <see cref="SqlConnection"/> rather than <c>IDbConnection</c> on purpose: the
        /// two transactional units of work in <see cref="SqlData"/> need <c>OpenAsync()</c> and
        /// <c>BeginTransaction()</c>, and they need the <c>SqlTransaction</c> the latter returns. Every
        /// other method lets Dapper open and close the connection around the call.
        /// </remarks>
        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        /// <summary>
        /// The logged-in user's id, from the <see cref="ClaimTypes.NameIdentifier"/> claim; null when there is
        /// no authenticated caller (background work, a request that failed authentication).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is public because <see cref="SqlData"/> needs it, and it needs it because Dapper cannot do what
        /// this class used to do for itself. The deleted <c>TryInjectUserIdAsync</c> asked sys.parameters
        /// whether the procedure it was about to run declared <c>@User_ID</c> and, if so, silently appended the
        /// claim value — which is how dbo.AuditTrails learned who performed a write without any controller
        /// passing an actor. Dapper sends exactly the properties of the anonymous parameter object and nothing
        /// else, so <see cref="SqlData"/> passes this value as an ordinary parameter, per call, to the 19
        /// procedures that record an audit actor.
        /// </para>
        /// <para>
        /// Read CoreFlow.md §0.1 before using this. The failure mode is silent: every one of those 19
        /// procedures declares <c>@User_ID INT = NULL</c>, so omitting it does not throw — it writes
        /// <c>AuditTrails.User_Id = 0</c> and the audit trail stops naming anyone. Note also that
        /// <c>@User_ID</c> means the ACTOR in those 19 and a TARGET USER ROW in five spUsers_* procedures;
        /// this property answers only the first question.
        /// </para>
        /// </remarks>
        public int? CurrentUserId => GetCurrentUserId();

        private int? GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            var raw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(raw, out var uid))
                return uid;

            return null;
        }
    }
}
