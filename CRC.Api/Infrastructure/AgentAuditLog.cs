using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;

namespace CRC.Api.Infrastructure
{
    // =================================================================================================
    // The Agent API's security channel. The same mechanism as CRC.Web/Infrastructure/AuditLog.cs, and a
    // deliberate copy of it rather than a reference.
    //
    // WHY A COPY. The dependency runs CRC.Web → CRC.Api → CRC.Data. CRC.Web references this project so
    // that its controllers can be loaded as an MVC application part; a reference back to CRC.Web would
    // be a cycle and the build would say so. AuditLog.cs lives in CRC.Web, so it is unreachable from
    // here. (CoreFlow.md §13.2.)
    //
    // WHY THE COPY IS CHEAP. Nothing is duplicated except the class. Routing to the audit file is done
    // by ONE property — `Log.ForContext("AuditChannel", true)` against Serilog's STATIC logger — and
    // that logger is the process-wide pipeline configured once in CRC.Web/Program.cs. Both file sinks,
    // both retention policies and all three enrichers are shared. Lines written here land in exactly the
    // same Logs/audit-*.log as every line AuditLog writes, interleaved by timestamp (CoreFlow.md §9.2).
    // What is duplicated is 12 lines of class; what is shared is the whole pipeline.
    //
    // 🔴 EVERY MESSAGE NAMES THE ACTOR IN ITS OWN TEXT. The output template's [User:…] field comes from
    // CorrelationIdMiddleware, which pushes `context.User?.Identity?.Name` onto Serilog's LogContext
    // BEFORE the endpoint runs — that is, before AgentApiKeyFilter has set the principal. So every line
    // an agent request writes reads [User:anonymous], however correct dbo.AuditTrails is. The field
    // cannot be relied on here and is not to be "fixed"; instead the actor is written into the message
    // body, which is the part this class controls. CoreFlow.md §13.3.
    //
    // 🔴 THE KEY IS NEVER LOGGED. Not the configured value, not the supplied value, not a prefix of
    // either, not its length, and not on rejection — a rejected key is very often a correct key with a
    // typo, and the log file is read by more people than the app setting is. The reason for a rejection
    // is a short fixed string chosen in the filter, never caller-supplied text.
    // =================================================================================================
    public static class AgentAuditLog
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext("AuditChannel", true);

        /// <summary>
        /// A request presented a valid key and has been given the AGENT_SERVICE identity. Written after
        /// the service account has been resolved, so <paramref name="serviceUserId"/> is the real
        /// dbo.Users.User_ID that every write in this request will be audited as.
        /// </summary>
        public static void AgentRequestAuthenticated(HttpContext context, string endpoint, int serviceUserId)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Agent API request authenticated as AGENT_SERVICE. Endpoint={Endpoint} ServiceUserId={ServiceUserId} RemoteIp={RemoteIp}",
                endpoint, serviceUserId, RemoteIp(context));
        }

        /// <summary>
        /// A request was refused a key check and no action ran. <paramref name="reason"/> is one of the
        /// filter's own fixed strings — "not configured", "missing key", "invalid key" — and is never
        /// echoed to the caller: the wire sees an undifferentiated 401, the distinction lives here.
        /// </summary>
        public static void AgentRequestRejected(HttpContext context, string endpoint, string reason)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Agent API request rejected. Endpoint={Endpoint} Reason={Reason} RemoteIp={RemoteIp}",
                endpoint, reason, RemoteIp(context));
        }

        /// <summary>
        /// The key was correct and the AGENT_SERVICE row was not there. The request is answered 503 and
        /// no action runs — see AgentApiKeyFilter, and CoreFlow.md §13.3 for why continuing would be
        /// worse than failing.
        /// </summary>
        public static void AgentServiceAccountMissing(HttpContext context, string endpoint)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Agent API request failed: the AGENT_SERVICE account is missing from dbo.Users, so no actor could be resolved and the request was refused. Endpoint={Endpoint} RemoteIp={RemoteIp}",
                endpoint, RemoteIp(context));
        }

        private static string RemoteIp(HttpContext context) =>
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
