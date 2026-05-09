namespace CRC.Web.Models
{
    public class LoginLockoutOptions
    {
        // Number of failed attempts (within AttemptWindowMinutes) that triggers a lockout.
        public int MaxFailedAttempts { get; set; } = 5;

        // How long an account stays locked after triggering, in minutes.
        public int LockoutMinutes { get; set; } = 15;

        // Sliding window over which consecutive failures are counted.
        // A failure older than this window resets the counter.
        public int AttemptWindowMinutes { get; set; } = 15;

        // Per-IP rate limit on the login endpoint.
        public int IpRequestsPerMinute { get; set; } = 10;

        // Window length (in seconds) for the per-IP rate limiter.
        // Drives both the FixedWindowRateLimiter window and the Retry-After
        // response header, so clients receive an accurate retry hint.
        public int IpRateLimitWindowSeconds { get; set; } = 60;
    }
}
