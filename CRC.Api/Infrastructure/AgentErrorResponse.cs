using Microsoft.AspNetCore.Http;

namespace CRC.Api.Infrastructure
{
    // =================================================================================================
    // The Agent API's caught-exception response. The same three-property shape
    // CRC.Web/Infrastructure/ErrorResponse.ForUser returns — { success = false, message, correlationId }
    // — with the same generic default message, so a caller cannot tell from the wire which project
    // handled the failure.
    //
    // It is a copy for the reason AgentAuditLog is a copy: CRC.Web references CRC.Api, so CRC.Api cannot
    // reference CRC.Web without making a cycle (CoreFlow.md §13.2). ErrorResponse lives in CRC.Web.
    //
    // Only ForUser is here. ErrorResponse.ForView renders a sentence into a Razor page, and this project
    // has no views: every answer it gives is JSON.
    // =================================================================================================
    public static class AgentErrorResponse
    {
        /// <summary>
        /// 🔴 Duplicated from CRC.Web/Infrastructure/CorrelationIdMiddleware.HttpContextItemKey ON
        /// PURPOSE — the constant is in CRC.Web and CRC.Api cannot reference it. The middleware still
        /// runs for these requests (it is a host-wide pipeline stage, not an MVC filter), so the id it
        /// stored is there to be read; only the name of the key had to be repeated.
        /// <para>
        /// 🔴 THE TWO MUST BE CHANGED TOGETHER. Nothing checks that they agree. If the middleware's key
        /// is renamed and this literal is not, this file silently falls back to
        /// HttpContext.TraceIdentifier and the correlation id the agent is handed stops matching the one
        /// in Logs/app-*.log — a broken diagnostic that breaks no build and throws no exception.
        /// </para>
        /// </summary>
        private const string CorrelationIdItemKey = "CorrelationId";

        public const string GenericUserMessage =
            "An unexpected error occurred. Please try again or contact support if the problem persists.";

        public static object ForUser(HttpContext context, string? userMessage = null)
        {
            return new
            {
                success = false,
                message = string.IsNullOrWhiteSpace(userMessage) ? GenericUserMessage : userMessage,
                correlationId = GetCorrelationId(context)
            };
        }

        // The same fallback chain as HttpContextCorrelationExtensions.GetCorrelationId: the id
        // CorrelationIdMiddleware stamped, or TraceIdentifier when it is absent, blank or not a string.
        private static string GetCorrelationId(HttpContext context)
        {
            if (context.Items.TryGetValue(CorrelationIdItemKey, out var value)
                && value is string s
                && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }

            return context.TraceIdentifier;
        }
    }
}
