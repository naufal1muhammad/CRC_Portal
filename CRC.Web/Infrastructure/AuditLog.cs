using Serilog;
using Serilog.Events;

namespace CRC.Web.Infrastructure
{
    /// Dedicated audit log channel. Writes to a separate "audit-*.log" file via a
    /// Serilog sub-logger configured in Program.cs. Use for security-relevant
    /// events: login attempts, viewing/downloading sensitive resources, etc.
    public static class AuditLog
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext("AuditChannel", true);

        public static void LoginSucceeded(HttpContext context, string username, int? userId, string userType)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Login succeeded. Username={Username} UserId={UserId} UserType={UserType}",
                username, userId, userType);
        }

        public static void LoginFailed(HttpContext context, string username, string reason)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Login failed. Username={Username} Reason={Reason}",
                username, reason);
        }

        public static void Logout(HttpContext context, string username)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Logout. Username={Username}", username);
        }

        public static void DocumentSearched(HttpContext context, string mode, string? individualName, string? documentType, int resultCount)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Documents searched. Mode={Mode} Individual={Individual} DocType={DocType} Results={ResultCount}",
                mode, individualName ?? "", documentType ?? "", resultCount);
        }
    }
}
