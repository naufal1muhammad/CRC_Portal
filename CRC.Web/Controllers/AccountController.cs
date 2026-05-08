using CRC.Data.Database;
using CRC.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using CRC.Web.Models;

namespace CRC.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly PasswordPolicyOptions _passwordPolicy;
        private readonly SessionTimeoutOptions _sessionTimeout;
        private readonly LoginLockoutOptions _lockoutOptions;
        private readonly ILogger<AccountController> _logger;
        private static readonly PasswordHasher<string> _hasher = new PasswordHasher<string>();

        public AccountController(
            DatabaseHelper db,
            IOptions<PasswordPolicyOptions> passwordPolicyOptions,
            IOptions<SessionTimeoutOptions> sessionTimeoutOptions,
            IOptions<LoginLockoutOptions> lockoutOptions,
            ILogger<AccountController> logger)
        {
            _db = db;
            _passwordPolicy = passwordPolicyOptions.Value;
            _sessionTimeout = sessionTimeoutOptions.Value;
            _lockoutOptions = lockoutOptions.Value;
            _logger = logger;
        }

        private async Task RegisterFailedLoginAsync(string username, HttpContext httpContext)
        {
            try
            {
                using var conn = _db.CreateConnection();
                await conn.OpenAsync();

                using var cmd = new SqlCommand("dbo.spUsers_RegisterFailedLogin", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.Add(new SqlParameter("@Username", SqlDbType.VarChar, 100) { Value = username });
                cmd.Parameters.Add(new SqlParameter("@MaxFailedAttempts", SqlDbType.Int) { Value = _lockoutOptions.MaxFailedAttempts });
                cmd.Parameters.Add(new SqlParameter("@LockoutMinutes", SqlDbType.Int) { Value = _lockoutOptions.LockoutMinutes });
                cmd.Parameters.Add(new SqlParameter("@AttemptWindowMinutes", SqlDbType.Int) { Value = _lockoutOptions.AttemptWindowMinutes });

                var triggeredParam = new SqlParameter("@LockoutTriggered", SqlDbType.Bit) { Direction = ParameterDirection.Output };
                var lockoutEndParam = new SqlParameter("@LockoutEndUtc", SqlDbType.DateTime) { Direction = ParameterDirection.Output };
                var failedCountParam = new SqlParameter("@FailedLoginCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(triggeredParam);
                cmd.Parameters.Add(lockoutEndParam);
                cmd.Parameters.Add(failedCountParam);

                await cmd.ExecuteNonQueryAsync();

                var triggered = triggeredParam.Value is bool b && b;
                if (triggered)
                {
                    DateTime? lockoutEnd = lockoutEndParam.Value is DateTime dtEnd ? dtEnd : null;
                    int failedCount = failedCountParam.Value is int fc ? fc : 0;
                    AuditLog.LoginLockoutTriggered(httpContext, username, failedCount, lockoutEnd);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register failed-login attempt for Username={Username}", username);
            }
        }

        private async Task ResetFailedLoginsAsync(int userId)
        {
            try
            {
                await _db.ExecuteNonQueryAsync(
                    "spUsers_ResetFailedLogins",
                    new[] { new SqlParameter("@User_ID", userId) }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset failed-login counters for UserId={UserId}", userId);
            }
        }

        private List<string> ValidatePasswordPolicy(string password)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(password))
            {
                errors.Add("Password is required.");
                return errors;
            }

            if (password.Length < _passwordPolicy.RequiredLength)
                errors.Add($"Password must be at least {_passwordPolicy.RequiredLength} characters long.");

            if (_passwordPolicy.RequireUppercase && !password.Any(char.IsUpper))
                errors.Add("Password must contain at least one uppercase letter (A-Z).");

            if (_passwordPolicy.RequireLowercase && !password.Any(char.IsLower))
                errors.Add("Password must contain at least one lowercase letter (a-z).");

            if (_passwordPolicy.RequireDigit && !password.Any(char.IsDigit))
                errors.Add("Password must contain at least one digit (0-9).");

            if (_passwordPolicy.RequireNonAlphanumeric && !password.Any(c => !char.IsLetterOrDigit(c)))
                errors.Add("Password must contain at least one special character (e.g., !@#$%^&*).");

            if (_passwordPolicy.RequiredUniqueChars > 0 && password.Distinct().Count() < _passwordPolicy.RequiredUniqueChars)
                errors.Add($"Password must contain at least {_passwordPolicy.RequiredUniqueChars} unique characters.");

            return errors;
        }

        // -------------------------
        // SUPERUSER ONLY: Register
        // -------------------------

        // GET: /Account/Register
        [Authorize(Policy = "SuperUserOnly")]
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // GET: /Account/GetPasswordPolicy (for client-side validation)
        [Authorize]
        [HttpGet]
        public IActionResult GetPasswordPolicy()
        {
            return Ok(new
            {
                requireDigit = _passwordPolicy.RequireDigit,
                requireLowercase = _passwordPolicy.RequireLowercase,
                requireNonAlphanumeric = _passwordPolicy.RequireNonAlphanumeric,
                requireUppercase = _passwordPolicy.RequireUppercase,
                requiredLength = _passwordPolicy.RequiredLength,
                requiredUniqueChars = _passwordPolicy.RequiredUniqueChars
            });
        }

        // GET: /Account/GetSessionTimeout (for client-side inactivity tracker)
        [Authorize]
        [HttpGet]
        public IActionResult GetSessionTimeout()
        {
            return Ok(new
            {
                inactivityTimeoutSeconds = _sessionTimeout.InactivityTimeoutSeconds
            });
        }

        // DTO (request model) for registration
        public class RegisterUserRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;

            // 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF
            public int UserType { get; set; } = 3;
            public string? StaffId { get; set; }
        }

        public class UserListItemDto
        {
            [JsonPropertyName("userId")]
            public int UserId { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("username")]
            public string Username { get; set; } = "";

            [JsonPropertyName("email")]
            public string Email { get; set; } = "";

            [JsonPropertyName("userType")]
            public int UserType { get; set; }

            [JsonPropertyName("userTypeName")]
            public string UserTypeName { get; set; } = "";

            [JsonPropertyName("staffId")]
            public string StaffId { get; set; } = "";

            [JsonPropertyName("createdAt")]
            public string CreatedAt { get; set; } = "";

            [JsonPropertyName("lastLogin")]
            public string LastLogin { get; set; } = "";

            [JsonPropertyName("failedLoginCount")]
            public int FailedLoginCount { get; set; }

            [JsonPropertyName("lastFailedLoginAt")]
            public string LastFailedLoginAt { get; set; } = "";

            [JsonPropertyName("lockoutEndUtc")]
            public string LockoutEndUtc { get; set; } = "";

            [JsonPropertyName("isLocked")]
            public bool IsLocked { get; set; }
        }

        // POST: /Account/RegisterUser (called via JS)
        [Authorize(Policy = "SuperUserOnly")]
        [HttpPost]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequest model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.Name) ||
                string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.Password))
            {
                return BadRequest(new { success = false, message = "Please fill in all required fields." });
            }

            var passwordErrors = ValidatePasswordPolicy(model.Password);
            if (passwordErrors.Count > 0)
            {
                return BadRequest(new { success = false, message = string.Join(" ", passwordErrors) });
            }

            try
            {
                var username = model.Username.Trim();
                var passwordHash = _hasher.HashPassword(username, model.Password);

                // StaffId required only for STAFF
                string? staffId = null;
                if (model.UserType == 3)
                {
                    staffId = (model.StaffId ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(staffId))
                        return BadRequest(new { success = false, message = "Staff is required for STAFF users." });
                }

                var parameters = new[]
                {
    new SqlParameter("@User_Name", model.Name.Trim()),
    new SqlParameter("@Username", username),
    new SqlParameter("@User_Email", model.Email.Trim()),
    new SqlParameter("@PasswordHash", passwordHash),
    new SqlParameter("@User_Type", model.UserType),
    new SqlParameter("@Staff_ID", (object?)staffId ?? DBNull.Value)
};

                await _db.ExecuteNonQueryAsync("spUsers_Register", parameters);

                return Ok(new { success = true, message = "User registered successfully." });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException during user registration Username={Username}", model.Username);
                return Ok(ErrorResponse.ForUser(HttpContext, "Unable to register user. Please verify the inputs and try again."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during user registration Username={Username}", model.Username);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
        }

        // -------------------------
        // ANONYMOUS: Login
        // -------------------------

        // GET: /Account/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        private IActionResult RedirectToLanding(ClaimsPrincipal principal)
        {
            if (principal.IsInRole("SUPERUSER"))
                return RedirectToAction("Index", "Dashboard");

            if (principal.IsInRole("ADMIN"))
                return RedirectToAction("Index", "AdminDashboard");

            if (principal.IsInRole("STAFF"))
                return RedirectToAction("Index", "StaffDashboard");

            return RedirectToAction("AccessDenied", "Account");
        }

        // DTO for login
        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        // POST: /Account/Login
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("login-ip")]
        public async Task<IActionResult> Login(LoginRequest model)
        {
            // Generic message used for every failure path so we don't leak whether the
            // username exists, whether the account is locked, or the specific reason.
            const string genericLoginError = "Invalid username or password.";

            if (model == null ||
                string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Password))
            {
                ViewData["LoginError"] = "Please enter username and password.";
                return View();
            }

            try
            {
                var usernameInput = model.Username.Trim();

                var dt = await _db.ExecuteDataTableAsync(
                    "spUsers_ValidateLogin",
                    new[] { new SqlParameter("@Username", usernameInput) }
                );

                if (dt.Rows.Count == 0)
                {
                    AuditLog.LoginFailed(HttpContext, usernameInput, "UserNotFound");
                    ViewData["LoginError"] = genericLoginError;
                    return View();
                }

                var row = dt.Rows[0];

                var userId = row["User_ID"]?.ToString() ?? string.Empty;
                var userName = row["User_Name"]?.ToString() ?? string.Empty;
                var username = row["Username"]?.ToString() ?? string.Empty;
                var userEmail = row["User_Email"]?.ToString() ?? string.Empty;
                var userType = row["User_Type"]?.ToString() ?? "3";
                var staffId = row.Table.Columns.Contains("StaffId")
    ? (row["StaffId"]?.ToString() ?? "")
    : "";

                // Lockout check: if the account is currently locked, refuse before
                // verifying the password (avoids password-oracle on locked accounts)
                // and do not increment the failure counter.
                DateTime? lockoutEndUtc = null;
                if (row.Table.Columns.Contains("LockoutEndUtc") && row["LockoutEndUtc"] != DBNull.Value)
                {
                    var rawEnd = Convert.ToDateTime(row["LockoutEndUtc"]);
                    if (rawEnd.Kind == DateTimeKind.Unspecified)
                        rawEnd = DateTime.SpecifyKind(rawEnd, DateTimeKind.Utc);
                    lockoutEndUtc = rawEnd;
                }

                if (lockoutEndUtc.HasValue && lockoutEndUtc.Value > DateTime.UtcNow)
                {
                    AuditLog.LoginAttemptWhileLocked(HttpContext, usernameInput, lockoutEndUtc.Value);
                    ViewData["LoginError"] = genericLoginError;
                    return View();
                }

                var storedHash = row["PasswordHash"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(storedHash))
                {
                    AuditLog.LoginFailed(HttpContext, usernameInput, "MissingPasswordHash");
                    await RegisterFailedLoginAsync(usernameInput, HttpContext);
                    ViewData["LoginError"] = genericLoginError;
                    return View();
                }

                var verify = _hasher.VerifyHashedPassword(username, storedHash, model.Password);
                if (verify == PasswordVerificationResult.Failed)
                {
                    AuditLog.LoginFailed(HttpContext, usernameInput, "PasswordMismatch");
                    await RegisterFailedLoginAsync(usernameInput, HttpContext);
                    ViewData["LoginError"] = genericLoginError;
                    return View();
                }

                if (int.TryParse(userId, out var uid))
                {
                    await ResetFailedLoginsAsync(uid);
                    await _db.ExecuteNonQueryAsync(
                        "spUsers_UpdateLastLogin",
                        new[] { new SqlParameter("@User_ID", uid) }
                    );
                }

                // -----------------------------
                // Claims + Sign-in (same as yours)
                // -----------------------------
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim("FullName", userName),
            new Claim("UserEmail", userEmail),
            new Claim("UserType", userType)
        };

                if (userType.Trim() == "3" && !string.IsNullOrWhiteSpace(staffId))
                {
                    claims.Add(new Claim("StaffId", staffId));
                }

                var ut = userType.Trim();

                if (ut == "1") // SUPERUSER
                {
                    claims.Add(new Claim(ClaimTypes.Role, "SUPERUSER"));
                }
                else if (ut == "2") // ADMIN
                {
                    claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
                }
                else
                {
                    claims.Add(new Claim(ClaimTypes.Role, "STAFF"));
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties { IsPersistent = false }
                );

                int? auditUserId = int.TryParse(userId, out var parsedUserId) ? parsedUserId : (int?)null;
                AuditLog.LoginSucceeded(HttpContext, username, auditUserId, ut);

                return RedirectToLanding(principal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login attempt Username={Username}", model?.Username);
                ViewData["LoginError"] = ErrorResponse.ForView(HttpContext, "We couldn't sign you in right now.");
                return View();
            }
        }

        [Authorize(Policy = "SuperUserOnly")]
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var dt = await _db.ExecuteDataTableAsync("spUsers_GetAll", Array.Empty<SqlParameter>());

            string ToIso(object v)
            {
                if (v == null || v == DBNull.Value) return "";

                var dt = Convert.ToDateTime(v);
                if (dt.Kind == DateTimeKind.Unspecified)
                    dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                return dt.ToString("o");
            }

            string UserTypeName(object v)
            {
                if (v == null || v == DBNull.Value) return "";
                var t = Convert.ToInt32(v);
                return t switch
                {
                    1 => "SUPERUSER",
                    2 => "ADMIN",
                    3 => "STAFF",
                    _ => t.ToString()
                };
            }

            bool HasCol(string name) => dt.Columns.Contains(name);
            DateTime? ReadDateTime(System.Data.DataRow r, string col)
            {
                if (!HasCol(col) || r[col] == DBNull.Value) return null;
                var v = Convert.ToDateTime(r[col]);
                if (v.Kind == DateTimeKind.Unspecified)
                    v = DateTime.SpecifyKind(v, DateTimeKind.Utc);
                return v;
            }

            var nowUtc = DateTime.UtcNow;

            var users = dt.Rows.Cast<System.Data.DataRow>()
                .Select(r =>
                {
                    var lockoutEnd = ReadDateTime(r, "LockoutEndUtc");
                    var lastFailed = ReadDateTime(r, "LastFailedLoginAt");
                    var failedCount = HasCol("FailedLoginCount") && r["FailedLoginCount"] != DBNull.Value
                        ? Convert.ToInt32(r["FailedLoginCount"])
                        : 0;

                    return new UserListItemDto
                    {
                        UserId = Convert.ToInt32(r["User_ID"]),
                        Name = r["User_Name"]?.ToString() ?? "",
                        Username = r["Username"]?.ToString() ?? "",
                        Email = r["User_Email"]?.ToString() ?? "",
                        UserType = Convert.ToInt32(r["User_Type"]),
                        UserTypeName = UserTypeName(r["User_Type"]),
                        StaffId = r["Staff_ID"] == DBNull.Value ? "" : (r["Staff_ID"]?.ToString() ?? ""),
                        CreatedAt = ToIso(r["Created_At"]),
                        LastLogin = ToIso(r["Last_Login"]),
                        FailedLoginCount = failedCount,
                        LastFailedLoginAt = lastFailed.HasValue ? lastFailed.Value.ToString("o") : "",
                        LockoutEndUtc = lockoutEnd.HasValue ? lockoutEnd.Value.ToString("o") : "",
                        IsLocked = lockoutEnd.HasValue && lockoutEnd.Value > nowUtc
                    };
                })
                .ToList();

            return Ok(new { success = true, users });
        }

        public class UnlockUserRequest
        {
            public int UserId { get; set; }
        }

        // POST: /Account/UnlockUser (SuperUser only). Clears the lockout window and
        // failed-attempt counter for the target account.
        [Authorize(Policy = "SuperUserOnly")]
        [HttpPost]
        public async Task<IActionResult> UnlockUser([FromBody] UnlockUserRequest model)
        {
            if (model == null || model.UserId <= 0)
                return BadRequest(new { success = false, message = "A valid user is required." });

            try
            {
                var dt = await _db.ExecuteDataTableAsync(
                    "spUsers_GetById",
                    new[] { new SqlParameter("@User_ID", model.UserId) }
                );

                if (dt.Rows.Count == 0)
                    return BadRequest(new { success = false, message = "User not found." });

                var targetUsername = dt.Rows[0]["Username"]?.ToString() ?? "";

                await _db.ExecuteNonQueryAsync(
                    "spUsers_Unlock",
                    new[] { new SqlParameter("@User_ID", model.UserId) }
                );

                var actor = User?.Identity?.Name ?? "unknown";
                AuditLog.AccountUnlocked(HttpContext, model.UserId, targetUsername, actor);

                return Ok(new { success = true, message = "Account unlocked." });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException unlocking UserId={UserId}", model.UserId);
                return Ok(ErrorResponse.ForUser(HttpContext, "Unable to unlock the account."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error unlocking UserId={UserId}", model.UserId);
                return Ok(ErrorResponse.ForUser(HttpContext));
            }
        }

        // DTO for Change Password page
        public class ChangePasswordViewModel
        {
            // Read-only user profile fields (loaded from dbo.Users)
            public string UserName { get; set; } = "";
            public string UserType { get; set; } = "";
            public int UserTypeId { get; set; }
            public string StaffId { get; set; } = "";
            public string Email { get; set; } = "";

            // Inputs
            [Required(ErrorMessage = "Current password is required.")]
            [DataType(DataType.Password)]
            public string CurrentPassword { get; set; } = "";

            [Required(ErrorMessage = "New password is required.")]
            [DataType(DataType.Password)]
            public string NewPassword { get; set; } = "";

            [Required(ErrorMessage = "Confirm password is required.")]
            [Compare(nameof(NewPassword), ErrorMessage = "New password and confirm password do not match.")]
            [DataType(DataType.Password)]
            public string ConfirmPassword { get; set; } = "";
        }

        // -------------------------
        // Change Password (all authenticated users)
        // -------------------------

        private static string UserTypeDisplay(int userType)
        {
            return userType switch
            {
                1 => "SUPERUSER",
                2 => "ADMIN",
                3 => "STAFF",
                _ => userType.ToString()
            };
        }

        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                TempData["ErrorMessage"] = "Unable to identify user.";
                return RedirectToAction(nameof(Logout));
            }

            var dt = await _db.ExecuteDataTableAsync(
                "spUsers_GetById",
                new[] { new SqlParameter("@User_ID", userId) }
            );

            if (dt.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Logout));
            }

            var row = dt.Rows[0];
            var userType = Convert.ToInt32(row["User_Type"]);

            var vm = new ChangePasswordViewModel
            {
                UserName = row["User_Name"]?.ToString() ?? "",
                UserTypeId = userType,
                UserType = UserTypeDisplay(userType),
                StaffId = row["StaffId"] == DBNull.Value ? "" : (row["StaffId"]?.ToString() ?? ""),
                Email = row["User_Email"]?.ToString() ?? ""
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                TempData["ErrorMessage"] = "Unable to identify user.";
                return RedirectToAction(nameof(Logout));
            }

            // Always reload user fields from DB (do not trust form values)
            var dt = await _db.ExecuteDataTableAsync(
                "spUsers_GetById",
                new[] { new SqlParameter("@User_ID", userId) }
            );

            if (dt.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Logout));
            }

            var row = dt.Rows[0];
            var username = row["Username"]?.ToString() ?? "";
            var storedHash = row["PasswordHash"]?.ToString() ?? "";
            var userType = Convert.ToInt32(row["User_Type"]);

            // Overwrite read-only fields (so UI always shows truth from dbo.Users)
            model.UserName = row["User_Name"]?.ToString() ?? "";
            model.UserTypeId = userType;
            model.UserType = UserTypeDisplay(userType);
            model.StaffId = row["StaffId"] == DBNull.Value ? "" : (row["StaffId"]?.ToString() ?? "");
            model.Email = row["User_Email"]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(storedHash))
            {
                TempData["ErrorMessage"] = "User record is invalid.";
                return View(model);
            }

            // Validate current password (against dbo.Users.Password_Hash)
            var verify = _hasher.VerifyHashedPassword(username, storedHash, model.CurrentPassword ?? "");
            if (verify == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
            }

            // Basic extra rule: new password must be different
            if (!string.IsNullOrWhiteSpace(model.NewPassword) &&
                !string.IsNullOrWhiteSpace(model.CurrentPassword) &&
                model.NewPassword == model.CurrentPassword)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "New password must be different from current password.");
            }

            // Validate new password against policy
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var passwordErrors = ValidatePasswordPolicy(model.NewPassword);
                foreach (var error in passwordErrors)
                {
                    ModelState.AddModelError(nameof(model.NewPassword), error);
                }
            }

            if (!ModelState.IsValid)
            {
                // Clear sensitive inputs on invalid submit
                model.CurrentPassword = "";
                model.NewPassword = "";
                model.ConfirmPassword = "";
                return View(model);
            }

            var newHash = _hasher.HashPassword(username, model.NewPassword);

            try
            {
                await _db.ExecuteNonQueryAsync(
                    "spUsers_UpdatePassword",
                    new[]
                    {
                        new SqlParameter("@User_ID", userId),
                        new SqlParameter("@PasswordHash", newHash)
                    }
                );

                TempData["SuccessMessage"] = "Password updated successfully.";
                return RedirectToAction(nameof(ChangePassword));
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SqlException updating password for UserId={UserId}", userId);
                TempData["ErrorMessage"] = ErrorResponse.ForView(HttpContext, "We couldn't update your password.");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating password for UserId={UserId}", userId);
                TempData["ErrorMessage"] = ErrorResponse.ForView(HttpContext);
                return View(model);
            }
        }


        // -------------------------
        // Access Denied page
        // -------------------------
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // -------------------------
        // Logout
        // -------------------------
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            var username = User?.Identity?.Name ?? "anonymous";
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            AuditLog.Logout(HttpContext, username);
            return RedirectToAction("Login");
        }
    }
}