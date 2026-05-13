using System;
using System.Security.Claims;

namespace CRC.Web.Infrastructure
{
    /// <summary>
    /// Centralized ownership checks for Staff-scoped endpoints. Use these helpers
    /// at the top of any action that takes a staffId (or that resolves a staffId
    /// from a slot/appointment) instead of re-inlining the role + claim comparison.
    ///
    /// Rules:
    ///   - ADMIN and SUPERUSER can access any staff record.
    ///   - A pure STAFF user can only access their own staff record, identified
    ///     by the "StaffId" claim.
    ///   - Anyone without ADMIN/SUPERUSER/STAFF roles is rejected by the surrounding
    ///     [Authorize(...)] attribute, not by these helpers.
    /// </summary>
    public static class StaffAccessExtensions
    {
        public const string AdminRole = "ADMIN";
        public const string SuperuserRole = "SUPERUSER";
        public const string StaffRole = "STAFF";
        public const string StaffIdClaimType = "StaffId";

        /// <summary>
        /// Returns true when <paramref name="user"/> is allowed to read/write data
        /// belonging to <paramref name="staffId"/>. ADMIN/SUPERUSER are always allowed.
        /// A pure STAFF user is allowed only when the provided <paramref name="staffId"/>
        /// matches their own "StaffId" claim (case-insensitive, trimmed).
        /// </summary>
        public static bool CanAccessStaff(this ClaimsPrincipal? user, string? staffId)
        {
            if (user == null) return false;

            if (user.IsInRole(AdminRole) || user.IsInRole(SuperuserRole))
                return true;

            if (!user.IsInRole(StaffRole))
                return false;

            var ownStaffId = user.FindFirst(StaffIdClaimType)?.Value?.Trim() ?? string.Empty;
            if (ownStaffId.Length == 0)
                return false;

            var requested = (staffId ?? string.Empty).Trim();
            if (requested.Length == 0)
                return false;

            return string.Equals(ownStaffId, requested, StringComparison.OrdinalIgnoreCase);
        }
    }
}
