using CRC.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Security.Claims;

namespace CRC.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly DatabaseHelper _db;
        private static readonly PasswordHasher<string> _hasher = new PasswordHasher<string>();

        public AccountController(DatabaseHelper db)
        {
            _db = db;
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
                // includes RAISERROR from sproc
                return Ok(new { success = false, message = ex.Message });
            }
            catch
            {
                return Ok(new { success = false, message = "An unexpected error occurred." });
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
        public async Task<IActionResult> Login(LoginRequest model)
        {
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
                    ViewData["LoginError"] = "Invalid username or password.";
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

                var storedHash = row["PasswordHash"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(storedHash))
                {
                    ViewData["LoginError"] = "Invalid username or password.";
                    return View();
                }

                var verify = _hasher.VerifyHashedPassword(username, storedHash, model.Password);
                if (verify == PasswordVerificationResult.Failed)
                {
                    ViewData["LoginError"] = "Invalid username or password.";
                    return View();
                }

                if (int.TryParse(userId, out var uid))
                {
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

                return RedirectToLanding(principal);
            }
            catch (Exception ex)
            {
                ViewData["LoginError"] = ex.Message;
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
                return Convert.ToDateTime(v).ToString("o");
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

            var users = dt.Rows.Cast<System.Data.DataRow>()
                .Select(r => new UserListItemDto
                {
                    UserId = Convert.ToInt32(r["User_ID"]),
                    Name = r["User_Name"]?.ToString() ?? "",
                    Username = r["Username"]?.ToString() ?? "",
                    Email = r["User_Email"]?.ToString() ?? "",
                    UserType = Convert.ToInt32(r["User_Type"]),
                    UserTypeName = UserTypeName(r["User_Type"]),
                    StaffId = r["Staff_ID"] == DBNull.Value ? "" : (r["Staff_ID"]?.ToString() ?? ""),
                    CreatedAt = ToIso(r["Created_At"]),
                    LastLogin = ToIso(r["Last_Login"])
                })
                .ToList();

            return Ok(new { success = true, users });
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
            [MinLength(8, ErrorMessage = "New password must be at least 8 characters.")]
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
                TempData["ErrorMessage"] = ex.Message;
                return View(model);
            }
            catch
            {
                TempData["ErrorMessage"] = "An unexpected error occurred.";
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
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}