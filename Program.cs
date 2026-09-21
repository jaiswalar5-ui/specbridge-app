using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// 1. Configure User Secrets for local development (LLM API key should be loaded via User Secrets)
builder.Configuration.AddUserSecrets<Program>();

// 2. Add services for Razor Pages and Web API Controllers
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddHttpClient<ISpecGeneratorService, LlmSpecGeneratorService>();
builder.Services.AddSingleton<IPdfExportService, PdfExportService>();

// 3. Configure ASP.NET Core built-in Rate Limiting middleware
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(policyName: "GenerateSpecPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

// 4. Configure Security Headers baseline middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self';");
    
    if (context.Request.IsHttps)
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    await next();
});

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// 5. Enforce HTTPS Redirection
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// 6. Rate Limiting Middleware
app.UseRateLimiter();

app.UseAuthorization();

// 7. Map Endpoints
app.MapRazorPages();
app.MapControllers();

app.Run();

// Make Program accessible for WebApplicationFactory / UserSecrets
public partial class Program { }
