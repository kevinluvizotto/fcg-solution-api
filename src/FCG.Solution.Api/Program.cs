using System.Net.Http.Headers;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ======================
// Upstreams
// ======================
var apimBaseUrl = builder.Configuration["Upstreams:ApimBaseUrl"];
if (string.IsNullOrWhiteSpace(apimBaseUrl))
    throw new InvalidOperationException("Missing config: Upstreams:ApimBaseUrl (ex: https://fcg-apim-fiap-klztt01.azure-api.net)");

var searchBaseUrl = builder.Configuration["Upstreams:SearchBaseUrl"]; // ✅ novo
if (string.IsNullOrWhiteSpace(searchBaseUrl))
    throw new InvalidOperationException("Missing config: Upstreams:SearchBaseUrl (ex: https://fcg-search-api-klztt01.azurewebsites.net)");

builder.Services.AddHttpClient("apim", client =>
{
    client.BaseAddress = new Uri(apimBaseUrl.TrimEnd('/'));
    client.Timeout = TimeSpan.FromSeconds(100);
});

builder.Services.AddHttpClient("search", client => // ✅ novo
{
    client.BaseAddress = new Uri(searchBaseUrl.TrimEnd('/'));
    client.Timeout = TimeSpan.FromSeconds(100);
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/", ctx =>
{
    ctx.Response.Redirect("/login.html");
    return Task.CompletedTask;
});

// ⚠️ Inclui OPTIONS por segurança (mesmo com same-origin, não atrapalha)
string[] methods = ["GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS"];

// ======================
// APIM routes
// ======================
MapProxyTo("apim", "/users");
MapProxyTo("apim", "/games");
MapProxyTo("apim", "/payments");

// ======================
// Search routes (direto no Search API, NÃO APIM)
// ======================
MapProxyTo("search", "/search");
MapProxyTo("search", "/analytics");
MapProxyTo("search", "/recommendations");
MapProxyTo("search", "/reindex");

app.Run("http://*:80");

// ----------------------

void MapProxyTo(string upstreamClientName, string prefix)
{
    app.MapMethods(prefix, methods, (HttpContext ctx, IHttpClientFactory factory, IConfiguration cfg, ILogger<Program> logger)
        => ProxyToUpstream(ctx, factory, cfg, logger, upstreamClientName));

    app.MapMethods($"{prefix}/{{**rest}}", methods, (HttpContext ctx, IHttpClientFactory factory, IConfiguration cfg, ILogger<Program> logger)
        => ProxyToUpstream(ctx, factory, cfg, logger, upstreamClientName));
}

static async Task ProxyToUpstream(
    HttpContext ctx,
    IHttpClientFactory factory,
    IConfiguration cfg,
    ILogger<Program> logger,
    string upstreamClientName)
{
    try
    {
        var client = factory.CreateClient(upstreamClientName);

        // Path + Query
        var target = ctx.Request.Path + ctx.Request.QueryString;

        using var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);

        // Encaminha só os headers essenciais
        if (ctx.Request.Headers.TryGetValue("Authorization", out var auth))
            req.Headers.TryAddWithoutValidation("Authorization", auth.ToArray());

        if (ctx.Request.Headers.TryGetValue("Accept", out var accept))
            req.Headers.TryAddWithoutValidation("Accept", accept.ToArray());

        // Subscription key só faz sentido pro APIM (mas não atrapalha se existir)
        var subKey = cfg["Upstreams:ApimSubscriptionKey"];
        if (!string.IsNullOrWhiteSpace(subKey) && upstreamClientName.Equals("apim", StringComparison.OrdinalIgnoreCase))
            req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", subKey);

        // Body: copia para memória (evita stream não-seekable)
        var hasBody =
            !HttpMethods.IsGet(ctx.Request.Method) &&
            !HttpMethods.IsHead(ctx.Request.Method) &&
            !HttpMethods.IsDelete(ctx.Request.Method) &&
            (ctx.Request.ContentLength.GetValueOrDefault() > 0);

        if (hasBody)
        {
            using var ms = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(ms, ctx.RequestAborted);
            var bytes = ms.ToArray();

            req.Content = new ByteArrayContent(bytes);

            var ct = ctx.Request.ContentType ?? "application/json";
            req.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ct);
        }

        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);

        ctx.Response.StatusCode = (int)resp.StatusCode;

        foreach (var h in resp.Headers)
            ctx.Response.Headers[h.Key] = h.Value.ToArray();

        foreach (var h in resp.Content.Headers)
            ctx.Response.Headers[h.Key] = h.Value.ToArray();

        ctx.Response.Headers.Remove("transfer-encoding");

        // Debug útil
        ctx.Response.Headers["X-Upstream"] = client.BaseAddress + target;
        ctx.Response.Headers["X-TraceId"] = ctx.TraceIdentifier;
        ctx.Response.Headers["X-Upstream-Client"] = upstreamClientName;

        await resp.Content.CopyToAsync(ctx.Response.Body);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Proxy failure TraceId={TraceId} Path={Path}", ctx.TraceIdentifier, ctx.Request.Path);

        ctx.Response.StatusCode = 502;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(
            $"{{\"status\":502,\"message\":\"Proxy error calling upstream\",\"upstream\":\"{upstreamClientName}\",\"traceId\":\"{ctx.TraceIdentifier}\"}}");
    }
}
