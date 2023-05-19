
using Google.Cloud.Diagnostics.AspNetCore3;
using Google.Cloud.Diagnostics.Common;
using Serilog;
var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "projectforpftc-a2c8e69e6062.json");
builder.Logging.AddGoogle(new LoggingServiceOptions { ProjectId = "projectforpftc" });
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
