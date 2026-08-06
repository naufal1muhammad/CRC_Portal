using CRC.Data.Data;
using CRC.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using CRC.Web.Models;

namespace CRC.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IDatabaseData _data;
        private readonly PasswordPolicyOptions _passwordPolicy;
        private readonly SessionTimeoutOptions _sessionTimeout;
        private readonly LoginLockoutOptions _lockoutOptions;
        private readonly ILogger<AccountController> _logger;
        private static readonly PasswordHasher<string> _hasher = new PasswordHasher<string>();

        public AccountController(
            IDatabaseData data,
            IOptions<PasswordPolicyOptions> passwordPolicyOptions,
            IOptions<SessionTimeoutOptions> sessionTimeoutOptions,
            IOptions<LoginLockoutOptions> lockoutOptions,
            ILogger<AccountController> logger)
        {
            _data = data;
            _passwordPolicy = passwordPolicyOptions.Value;
            _sessionTimeout = sessionTimeoutOptions.Value;
            _lockoutOptions = lockoutOptions.Value;
            _logger = logger;
        }

        // Records one failed login attempt and, if this attempt locked the account, writes the audit line.
        //
        // 🔴 THE try/catch IS LOAD-BEARING AND SWALLOWS ON PURPOSE. Failing to RECORD a failed login must
        // never change what the caller is told about that login — the user still gets the same generic
        // "Invalid username or password.", and the operational detail goes to the app log. Rethrowing here
        // would turn a database hiccup into a 500 on the login page and hand an attacker a way to
        // distinguish one failure from another. Do not "improve" this by letting the exception out.
        //
        // The thresholds come from IOptions<LoginLockoutOptions> ("Account:LoginLockout" in
        // appsettings.json) and are passed per call: the procedure holds no policy of its own.
        private async Task RegisterFailedLoginAsync(string username, HttpContext httpContext)
        {
            try
            {
                var result = await _data.RegisterFailedLoginAsync(
                    username,
                    _lockoutOptions.MaxFailedAttempts,
                    _lockoutOptions.LockoutMinutes,
                    _lockoutOptions.AttemptWindowMinutes);

                // Null means the procedure returned no result set — an unknown username, or a lockout
                // window that was already open. Neither is reachable from Login below, and neither is a
                // new lockout, so there is nothing to audit either way. See IDatabaseData.
                if (result != null && result.LockoutTriggered)
                {
                    AuditLog.LoginLockoutTriggered(httpContext, username, result.FailedLoginCount, result.LockoutEndUtc);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register failed-login attempt for Username={Username}", username);
            }
        }

        // Clears the lockout counters after a successful login. Swallows for the same reason as above: a
        // successful authentication must not be turned into an error because the bookkeeping failed.
        private async Task ResetFailedLoginsAsync(int userId)
        {
            try
            {
                // spUsers_ResetFailedLogins' @User_ID is a TARGET — the user who just logged in — not an
                // audit actor. See CoreFlow.md §0.1.
                await _data.ResetFailedLoginsAsync(userId);
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

                // staffId is null for everyone who is not a STAFF user; the procedure only looks at it when
                // @User_Type = 3. Its four RAISERROR paths (duplicate username, missing Staff_ID, unknown
                // Staff_ID, Staff_ID already linked to another account) all surface as the SqlException
                // caught below, which is why none of them is re-checked here.
                await _data.RegisterUserAsync(
                    model.Name.Trim(),
                    username,
                    model.Email.Trim(),
                    passwordHash,
                    model.UserType,
                    staffId);

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

                // spUsers_ValidateLogin VALIDATES NOTHING — it is a plain read by username. A non-null
                // result means the account exists; every decision below is made here, in C#.
                var user = await _data.GetUserForLoginAsync(usernameInput);

                if (user == null)
                {
                    AuditLog.LoginFailed(HttpContext, usernameInput, "UserNotFound");
                    ViewData["LoginError"] = genericLoginError;
                    return View();
                }

                var userId = user.User_ID.ToString(CultureInfo.InvariantCulture);
                var userName = user.User_Name;
                var username = user.Username;
                var userEmail = user.User_Email;
                var userType = user.User_Type.ToString(CultureInfo.InvariantCulture);
                var staffId = user.StaffId ?? "";

                // Lockout check: if the account is currently locked, refuse before
                // verifying the password (avoids password-oracle on locked accounts)
                // and do not increment the failure counter.
                DateTime? lockoutEndUtc = null;
                if (user.LockoutEndUtc.HasValue)
                {
                    var rawEnd = user.LockoutEndUtc.Value;
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

                var storedHash = user.PasswordHash;
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

                // Both @User_ID parameters below are TARGETS — the account that just authenticated. They
                // could not be audit actors even in principle: SignInAsync has not run yet, so there is no
                // NameIdentifier claim for DatabaseHelper.CurrentUserId to read. See CoreFlow.md §0.1.
                await ResetFailedLoginsAsync(user.User_ID);
                await _data.UpdateLastLoginAsync(user.User_ID);

                // -----------------------------
                // Claims + Sign-in (same as yours)
                // -----------------------------
                //
                // 🔴 THIS BLOCK IS THE PRODUCT'S AUTHORIZATION AND AUDIT SURFACE, AND IT IS BUILT NOWHERE
                // ELSE. Renaming a claim, or changing the FORMAT of one's value, breaks things that will
                // not fail a build:
                //   • ClaimTypes.NameIdentifier — a plain integer string. DatabaseHelper.CurrentUserId
                //     int.TryParses it back, and that value becomes dbo.AuditTrails.User_Id for all 19
                //     audit-actor procedures. A non-numeric value here writes User_Id = 0 for the entire
                //     product, silently (CoreFlow.md §0.1).
                //   • "UserType" — "1"/"2"/"3" as STRINGS. All five authorization policies are
                //     RequireClaim("UserType", …) string comparisons (CoreFlow.md §2).
                //   • ClaimTypes.Role — the role names RedirectToLanding switches on.
                //   • "StaffId" — added ONLY for User_Type = 3, and read by every staff-scoped page.
                // Do not touch the construction below.
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

                int? auditUserId = user.User_ID;
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
            // spUsers_GetAll is the only Users read that omits Password_Hash — so the model it maps onto
            // does not carry one, and this endpoint's JSON cannot leak one by accident.
            var rows = await _data.GetAllUsersAsync();

            // Every date leaves as ISO-8601 round-trip ("o"), and "never" is the EMPTY STRING, not null —
            // wwwroot/js reads these by name and renders them straight. SQL Server hands back DATETIME with
            // Kind = Unspecified; the values are UTC, so they are stamped Utc before formatting or the "Z"
            // the format appends would be a lie.
            static string ToIso(DateTime? v)
            {
                if (!v.HasValue) return "";

                var value = v.Value;
                if (value.Kind == DateTimeKind.Unspecified)
                    value = DateTime.SpecifyKind(value, DateTimeKind.Utc);

                return value.ToString("o");
            }

            static string UserTypeName(int t)
            {
                return t switch
                {
                    1 => "SUPERUSER",
                    2 => "ADMIN",
                    3 => "STAFF",
                    _ => t.ToString()
                };
            }

            var nowUtc = DateTime.UtcNow;

            // The DTO's [JsonPropertyName] attributes are the public contract that wwwroot/js reads; the
            // Dapper model is mapped INTO it and never returned directly.
            var users = rows
                .Select(r =>
                {
                    var lockoutEnd = r.LockoutEndUtc;
                    if (lockoutEnd.HasValue && lockoutEnd.Value.Kind == DateTimeKind.Unspecified)
                        lockoutEnd = DateTime.SpecifyKind(lockoutEnd.Value, DateTimeKind.Utc);

                    return new UserListItemDto
                    {
                        UserId = r.User_ID,
                        Name = r.User_Name,
                        Username = r.Username,
                        Email = r.User_Email,
                        UserType = r.User_Type,
                        UserTypeName = UserTypeName(r.User_Type),
                        // Staff_ID is NULL for every non-STAFF account, and the DataTable code this
                        // replaced returned "" for a DBNull. Without the coalesce the table renders "null".
                        StaffId = r.Staff_ID ?? "",
                        CreatedAt = ToIso(r.Created_At),
                        LastLogin = ToIso(r.Last_Login),
                        // Failed_Login_Count is INT NOT NULL DEFAULT 0, so the coalesce cannot fire — it is
                        // here because the model types it int? to keep a NULL from becoming a 500 (Dapper
                        // throws mapping NULL onto a non-nullable int), exactly as BranchDetail does.
                        FailedLoginCount = r.FailedLoginCount ?? 0,
                        LastFailedLoginAt = ToIso(r.LastFailedLoginAt),
                        LockoutEndUtc = ToIso(lockoutEnd),
                        // "Locked" is not a column: an expired window leaves Lockout_End_Utc set until the
                        // next successful login clears it, so the answer is always a comparison against now.
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
                // 🔴 BOTH CALLS BELOW TAKE model.UserId — THE LOCKED-OUT ACCOUNT — as their @User_ID, and
                // that parameter is a TARGET, not an audit actor. The SUPERUSER performing the unlock is
                // named separately, in the audit line, from their own identity. Passing the caller's id to
                // spUsers_Unlock would clear the SUPERUSER's counters, leave the locked user locked, and
                // still answer "Account unlocked." See CoreFlow.md §0.1.
                var target = await _data.GetUserByIdAsync(model.UserId);

                if (target == null)
                    return BadRequest(new { success = false, message = "User not found." });

                var targetUsername = target.Username;

                await _data.UnlockUserAsync(model.UserId);

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

            // @User_ID is a TARGET — here it happens to be the caller's own id, read from their claim,
            // which is precisely what makes it easy to mistake for an actor parameter. It is not.
            var user = await _data.GetUserByIdAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Logout));
            }

            var userType = user.User_Type;

            var vm = new ChangePasswordViewModel
            {
                UserName = user.User_Name,
                UserTypeId = userType,
                UserType = UserTypeDisplay(userType),
                StaffId = user.StaffId ?? "",
                Email = user.User_Email
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
            var user = await _data.GetUserByIdAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Logout));
            }

            var username = user.Username;
            var storedHash = user.PasswordHash;
            var userType = user.User_Type;

            // Overwrite read-only fields (so UI always shows truth from dbo.Users)
            model.UserName = user.User_Name;
            model.UserTypeId = userType;
            model.UserType = UserTypeDisplay(userType);
            model.StaffId = user.StaffId ?? "";
            model.Email = user.User_Email;

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
                // A TARGET again: the row whose Password_Hash is replaced. nucentra has no admin password
                // reset, so this is always the caller's own account — but the parameter does not know that.
                await _data.UpdateUserPasswordAsync(userId, newHash);

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