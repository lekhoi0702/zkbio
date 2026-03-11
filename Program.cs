using Microsoft.AspNetCore.Authentication.Cookies;
using QuestPDF.Infrastructure;
using ZkbioDashboard.Services;

// QuestPDF community license — required once at startup
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Force the app to listen on port 5000.
builder.WebHost.UseUrls("http://127.0.0.1:5000");

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizePage("/Index");
    options.Conventions.AuthorizePage("/AllTransaction");
    options.Conventions.AuthorizePage("/PersonalAttendance");
    options.Conventions.AuthorizePage("/AttendanceReport");
    options.Conventions.AuthorizePage("/ContractorAttendance");
    options.Conventions.AuthorizePage("/EarlyExitReport");
});

builder.Services.AddControllers();
builder.Services.AddMemoryCache();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
    });

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapControllers();

app.Run();
