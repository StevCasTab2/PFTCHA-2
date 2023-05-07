using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Google.Cloud.Firestore;
using PFTCHA.Services;
using PFTCHA.Views.Shared;
using Microsoft.AspNetCore.DataProtection;
using Google.Cloud.SecretManager.V1;
using Google.Api.Gax.ResourceNames;
using Google.Protobuf;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "theta-solution-377011-94bfb5b80ee9.json");
// Add services to the container.
builder.Services.AddControllersWithViews();



// Build the parent project name.
SecretManagerServiceClient client = SecretManagerServiceClient.Create();
AccessSecretVersionResponse result = client.AccessSecretVersion("projects/110578221303/secrets/Client-Secret/versions/1");
string secretKey = result.Payload.Data.ToStringUtf8();
string clientId = secretKey.Substring(21, 72);
string clientsecret = secretKey.Substring(329, 35);
//Added Authentication to builder
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
}).AddCookie().AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    options.ClientId = clientId;
    options.ClientSecret = clientsecret;
    //options.ClaimActions.MapJsonKey("urn:google:picture", "picture", "url");
});

builder.Services.AddScoped<FirestoreRepository>(provider => new FirestoreRepository(builder.Configuration));
builder.Services.AddScoped<GoogleCloudStorage>(provider => new GoogleCloudStorage(builder.Configuration));

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
//Added Authentication
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
