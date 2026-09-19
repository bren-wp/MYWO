using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MYWO.Desktop.Data;

namespace MYWO.Desktop.Services;

public sealed class LocalApiServer : IDisposable
{
    private const string ApiKeyItem = "MYWO.ApiKeyId";
    private WebApplication? _app;

    public bool IsRunning => _app is not null;
    public int Port { get; private set; } = 8787;
    public long CompanyId { get; private set; }

    public void Start(long companyId, int port = 8787)
    {
        if (IsRunning) return;
        if (port is < 1024 or > 65535)
            throw new InvalidOperationException("API port mora biti između 1024 i 65535.");

        WebApplication? app = null;
        try
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(LocalApiServer).Assembly.FullName,
                EnvironmentName = Environments.Production
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
            app = builder.Build();
            CompanyId = companyId;
            Port = port;

            app.Use(async (context, next) =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    context.Response.Headers["Cache-Control"] = "no-store";
                    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
                    await next();
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Local API request failed: {context.Request.Method} {context.Request.Path}", ex);
                    if (context.Response.HasStarted) throw;
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(new { error = "internal_error" });
                }
                finally
                {
                    sw.Stop();
                    try
                    {
                        var apiKeyId = context.Items.TryGetValue(ApiKeyItem, out var raw) && raw is long id ? id : (long?)null;
                        AppDb.LogApiRequest(CompanyId, apiKeyId, context.Request.Method,
                            context.Request.Path.ToString(), context.Response.StatusCode, sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error("API request logging failed.", ex);
                    }
                }
            });

            app.MapGet("/health", () => Results.Ok(new
            {
                status = "ok",
                apiVersion = "v1",
                app = "MYWO",
                companyId = CompanyId
            }));

            app.MapGet("/api/v1/company", (HttpContext context) =>
            {
                if (!Authorize(context, "company.read")) return Results.Unauthorized();
                return Results.Ok(new { data = AppDb.GetCompany(CompanyId) });
            });

            app.MapGet("/api/v1/categories", (HttpContext context) =>
            {
                if (!Authorize(context, "taxonomy.read")) return Results.Unauthorized();
                var includeInactive = ParseBool(context.Request.Query["includeInactive"].ToString());
                return Results.Ok(new { data = AppDb.Categories(CompanyId, activeOnly: !includeInactive) });
            });

            app.MapGet("/api/v1/brands", (HttpContext context) =>
            {
                if (!Authorize(context, "taxonomy.read")) return Results.Unauthorized();
                var includeInactive = ParseBool(context.Request.Query["includeInactive"].ToString());
                return Results.Ok(new { data = AppDb.Brands(CompanyId, activeOnly: !includeInactive) });
            });

            app.MapGet("/api/v1/products", (HttpContext context) =>
            {
                if (!Authorize(context, "products.read")) return Results.Unauthorized();
                var includeInactive = ParseBool(context.Request.Query["includeInactive"].ToString());
                var items = AppDb.Products(
                    CompanyId,
                    context.Request.Query["q"].ToString(),
                    includeInactive ? null : true,
                    context.Request.Query["availability"].ToString(),
                    ParseLong(context.Request.Query["categoryId"].ToString()),
                    ParseLong(context.Request.Query["brandId"].ToString()));
                var page = ParseInt(context.Request.Query["page"].ToString(), 1, 1, 100000);
                var pageSize = ParseInt(context.Request.Query["pageSize"].ToString(), 100, 1, 500);
                var data = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return Results.Ok(new { data, meta = new { total = items.Count, page, pageSize } });
            });

            app.MapGet("/api/v1/services", (HttpContext context) =>
            {
                if (!Authorize(context, "services.read")) return Results.Unauthorized();
                var includeInactive = ParseBool(context.Request.Query["includeInactive"].ToString());
                var items = AppDb.Services(
                    CompanyId,
                    context.Request.Query["q"].ToString(),
                    includeInactive ? null : true,
                    ParseLong(context.Request.Query["categoryId"].ToString()));
                var page = ParseInt(context.Request.Query["page"].ToString(), 1, 1, 100000);
                var pageSize = ParseInt(context.Request.Query["pageSize"].ToString(), 100, 1, 500);
                var data = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return Results.Ok(new { data, meta = new { total = items.Count, page, pageSize } });
            });

            app.MapGet("/api/v1/catalog", (HttpContext context) =>
            {
                if (!Authorize(context, "catalog.read")) return Results.Unauthorized();
                var includeInactive = ParseBool(context.Request.Query["includeInactive"].ToString());
                bool? active = includeInactive ? null : true;
                var products = AppDb.Products(CompanyId, active: active);
                var services = AppDb.Services(CompanyId, active: active);
                return Results.Ok(new
                {
                    company = AppDb.GetCompany(CompanyId),
                    products,
                    services,
                    categories = AppDb.Categories(CompanyId, activeOnly: !includeInactive),
                    brands = AppDb.Brands(CompanyId, activeOnly: !includeInactive),
                    meta = new
                    {
                        products = products.Count,
                        services = services.Count,
                        generatedAt = DateTimeOffset.Now
                    }
                });
            });

            app.StartAsync().GetAwaiter().GetResult();
            _app = app;
            AppLogger.Info($"Local API started on 127.0.0.1:{port} for company {companyId}.");
        }
        catch
        {
            if (app is not null)
            {
                try { app.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
            }
            throw;
        }
    }

    public void Stop()
    {
        var app = _app;
        _app = null;
        if (app is null) return;
        try
        {
            app.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            AppLogger.Info("Local API stopped.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Local API stop failed.", ex);
        }
    }

    private bool Authorize(HttpContext context, string permission)
    {
        var key = context.Request.Headers["X-API-Key"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            var authorization = context.Request.Headers.Authorization.ToString().Trim();
            const string bearer = "Bearer ";
            if (authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
                key = authorization[bearer.Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(key)) return false;
        if (!AppDb.AuthorizeApiKey(CompanyId, ApiKeyService.Hash(key), permission, out var keyId)) return false;
        context.Items[ApiKeyItem] = keyId;
        return true;
    }

    private static bool ParseBool(string? value)
        => value?.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "da";

    private static long? ParseLong(string? value)
        => long.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

    private static int ParseInt(string? value, int fallback, int min, int max)
        => int.TryParse(value, out var parsed) ? Math.Clamp(parsed, min, max) : fallback;


    public void Dispose() => Stop();
}
