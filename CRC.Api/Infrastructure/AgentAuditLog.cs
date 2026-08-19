using System.Globalization;
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

        /// <summary>
        /// An appointment was booked through POST /api/agent/appointments.
        ///
        /// 🔴 CALLED ONLY AFTER SaveAppointmentAsync HAS RETURNED Reason == Ok — that is, after its
        /// transaction has COMMITTED. Never from inside a flow that can still roll back (CoreFlow.md
        /// §11.3, §6.6): audit-*.log is kept for 365 days, and a line describing a booking that a
        /// rollback then erased is an actively misleading record. The dbo.AuditTrails row is written the
        /// other way round — by spPatientAppointment_Insert, INSIDE the transaction, so a rollback takes
        /// it with it. Both trails end up honest, by opposite means.
        ///
        /// The field list mirrors AuditLog.AppointmentCreated so that an agent booking and a portal
        /// booking read the same way side by side in one file, with two additions that only exist here:
        /// SlotIds, because the hours consumed are the thing a coordinator asks about when a booking is
        /// disputed and dbo.PatientAppointment does not record them (CoreFlow.md §3.9); and the
        /// AGENT_SERVICE wording, because the [User:…] field on this line reads `anonymous` — see the
        /// banner above.
        /// </summary>
        public static void AgentAppointmentBooked(HttpContext context, int appointmentId, string patientId,
            string staffId, string branchId, DateTime appointmentDate, IReadOnlyList<int> slotIds,
            TimeSpan startTime, TimeSpan endTime, string appointmentTypeId, string status)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Agent API appointment booked as AGENT_SERVICE. AppointmentId={AppointmentId} PatientId={PatientId} StaffId={StaffId} BranchId={BranchId} Date={Date} From={StartTime} To={EndTime} SlotIds={SlotIds} TypeId={TypeId} Status={Status} RemoteIp={RemoteIp}",
                appointmentId, patientId, staffId, branchId,
                appointmentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                startTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                endTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                string.Join(",", slotIds), appointmentTypeId, status, RemoteIp(context));
        }

        /// <summary>
        /// A booking was REFUSED and nothing was written. Two different kinds of refusal share this one
        /// line, and <paramref name="reason"/> is what tells them apart:
        ///
        ///   • an <c>AppointmentSaveFailure</c> name — "SlotTaken", "SlotNotFound",
        ///     "SlotsNotConsecutive" — meaning the request reached SaveAppointmentAsync and its
        ///     transaction rolled back. The same string is on the wire, because the caller branches on it.
        ///   • one of the controller's own short fixed labels — "pjAppTypeId not 01", "status not
        ///     Scheduled", … — meaning the request was refused BEFORE any data call and never touched the
        ///     database. Nothing goes on the wire but a message: a validation refusal is a client bug to
        ///     fix, not an outcome to branch on, and inventing pseudo-reason names for it would pollute
        ///     the caller's branch table with values that are not enum members.
        ///
        /// Either way the value is chosen from a fixed set by the controller and is NEVER caller-supplied
        /// text — the same rule AgentRequestRejected follows. The refused request's own values are logged
        /// because a refusal is worth reading later: a rejected "04" is the enforcement of
        /// AgentApiPlan.md decision 4 and is the most interesting thing this endpoint can do short of
        /// booking. <paramref name="appointmentDate"/> is the raw string as sent, since a refusal for an
        /// unparseable date has nothing else to report.
        /// </summary>
        public static void AgentAppointmentRefused(HttpContext context, string reason, string patientId,
            string staffId, string branchId, string appointmentDate, IReadOnlyList<int> slotIds)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Agent API appointment refused. Reason={Reason} PatientId={PatientId} StaffId={StaffId} BranchId={BranchId} Date={Date} SlotIds={SlotIds} RemoteIp={RemoteIp}",
                reason, patientId, staffId, branchId, appointmentDate, string.Join(",", slotIds),
                RemoteIp(context));
        }

        private static string RemoteIp(HttpContext context) =>
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
