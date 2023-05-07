
using Google.Cloud.Diagnostics.AspNetCore3;
using Google.Cloud.Diagnostics.Common;
using Serilog;
var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "theta-solution-377011-94bfb5b80ee9.json");
builder.Logging.AddGoogle(new LoggingServiceOptions { ProjectId = "theta-solution-377011" });
builder.Services.AddControllersWithViews();
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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Subscribe}/{action=Index}/{id?}");

app.Run();
