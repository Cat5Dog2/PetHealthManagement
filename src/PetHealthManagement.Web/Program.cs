using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Infrastructure;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = UploadRequestLimits.MaxMultipartRequestBodySizeBytes;
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = UploadRequestLimits.MaxMultipartRequestBodySizeBytes;
});

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddScoped<IImageStorageService, FileSystemImageStorageService>();
builder.Services.AddScoped<IPetPhotoService, PetPhotoService>();
builder.Services.AddScoped<IPetDeletionService, PetDeletionService>();
builder.Services.AddScoped<IHealthLogImageService, HealthLogImageService>();
builder.Services.AddScoped<IVisitImageService, VisitImageService>();
builder.Services.AddScoped<IHealthLogDeletionService, HealthLogDeletionService>();
builder.Services.AddScoped<IVisitDeletionService, VisitDeletionService>();
builder.Services.AddScoped<IUserAvatarService, UserAvatarService>();
builder.Services.AddScoped<IUserDataDeletionService, UserDataDeletionService>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("UploadRateLimiting");

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        logger.LogWarning(
            "Rejected upload request due to rate limiting. path={Path}, partitionKey={PartitionKey}",
            context.HttpContext.Request.Path,
            UploadRateLimiting.ResolvePartitionKey(context.HttpContext));

        return ValueTask.CompletedTask;
    };
    options.AddPolicy(UploadRateLimiting.ImageUploadPolicyName, UploadRateLimiting.BuildImageUploadPolicy());
});

var app = builder.Build();
var uploadRequestLimitLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("UploadRequestLimits");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error/500");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (InvalidDataException ex) when (IsMultipartRequestParsingFailure(context, ex))
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        uploadRequestLimitLogger.LogWarning(
            ex,
            "Rejected multipart request that exceeded the configured upload limit. path={Path}",
            context.Request.Path);

        await WriteBadRequestPageAsync(context);
    }
    catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        uploadRequestLimitLogger.LogWarning(
            ex,
            "Rejected request that exceeded the configured upload limit. path={Path}",
            context.Request.Path);

        await WriteBadRequestPageAsync(context);
    }
});
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static bool IsMultipartRequestParsingFailure(HttpContext context, InvalidDataException exception)
{
    return context.Request.HasFormContentType
           && exception.Message.Contains("multipart", StringComparison.OrdinalIgnoreCase);
}

static Task WriteBadRequestPageAsync(HttpContext context)
{
    context.Response.Clear();
    context.Response.StatusCode = StatusCodes.Status400BadRequest;
    context.Response.ContentType = "text/html; charset=utf-8";

    return context.Response.WriteAsync(
        """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <title>400 Bad Request</title>
        </head>
        <body>
            <h1 class="text-danger">400 Bad Request</h1>
            <p>Please review your request and try again.</p>
        </body>
        </html>
        """);
}

public partial class Program;
