using Serilog.Context;

namespace CRC.Web.Infrastructure
{
    public class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-ID";
        public const string HttpContextItemKey = "CorrelationId";

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
                                && !string.IsNullOrWhiteSpace(existing)
                ? existing.ToString()
                : Guid.NewGuid().ToString("N");

            context.Items[HttpContextItemKey] = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("UserName", context.User?.Identity?.Name ?? "anonymous"))
            using (LogContext.PushProperty("RemoteIp", context.Connection.RemoteIpAddress?.ToString() ?? ""))
            {
                await _next(context);
            }
        }
    }

    public static class HttpContextCorrelationExtensions
    {
        public static string GetCorrelationId(this HttpContext context)
        {
            if (context.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out var value)
                && value is string s
                && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
            return context.TraceIdentifier;
        }
    }
}
