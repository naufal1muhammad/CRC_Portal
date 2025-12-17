using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
});
builder.Services.AddScoped<CRC.Web.Data.DatabaseHelper>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";      // where to go if not logged in
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });
builder.Services.AddAuthorization(options =>
{
    // UserType claim values:
    // 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF
    options.AddPolicy("SuperUserOnly", policy => policy.RequireClaim("UserType", "1"));
    options.AddPolicy("AdminOrSuper", policy => policy.RequireClaim("UserType", "1", "2"));
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("UserType", "2"));
    options.AddPolicy("StaffOnly", policy => policy.RequireClaim("UserType", "3"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
