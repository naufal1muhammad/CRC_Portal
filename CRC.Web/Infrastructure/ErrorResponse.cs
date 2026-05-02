namespace CRC.Web.Infrastructure
{
    public static class ErrorResponse
    {
        public const string GenericUserMessage =
            "An unexpected error occurred. Please try again or contact support if the problem persists.";

        public static object ForUser(HttpContext context, string? userMessage = null)
        {
            return new
            {
                success = false,
                message = string.IsNullOrWhiteSpace(userMessage) ? GenericUserMessage : userMessage,
                correlationId = context.GetCorrelationId()
            };
        }

        public static string ForView(HttpContext context, string? userMessage = null)
        {
            var msg = string.IsNullOrWhiteSpace(userMessage) ? GenericUserMessage : userMessage;
            return $"{msg} (Reference: {context.GetCorrelationId()})";
        }
    }
}
