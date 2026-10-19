using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.WebEncoders;
using Rollcall.Application;
using Rollcall.Data;
using Rollcall.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Every POST form is checked for an antiforgery token.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

    var messages = options.ModelBindingMessageProvider;
    messages.SetValueMustBeANumberAccessor(field => $"{field} bir sayı olmalı.");
    messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"\"{value}\" {field} için geçerli değil.");
    messages.SetValueIsInvalidAccessor(value => $"\"{value}\" geçerli bir değer değil.");
    messages.SetValueMustNotBeNullAccessor(_ => "Bu alan zorunlu.");
    messages.SetMissingBindRequiredValueAccessor(field => $"{field} zorunlu.");
    messages.SetUnknownValueIsInvalidAccessor(field => $"{field} için girilen değer geçerli değil.");
});

// Write Turkish letters as they are instead of HTML character references (İ, not &#x130;).
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddRollcallData(builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Services.AddRollcallServices();
builder.Services.AddScoped<Lookups>();

// Keys that protect antiforgery tokens survive restarts (and container rebuilds when the folder is a volume).
var keysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("Rollcall");

var app = builder.Build();

await app.Services.PrepareDatabaseAsync(seedDemoData: app.Configuration.GetValue<bool>("Seed:DemoContent"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

var turkish = new CultureInfo("tr-TR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(turkish),
    SupportedCultures = [turkish],
    SupportedUICultures = [turkish],
});

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
