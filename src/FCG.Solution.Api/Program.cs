using System.Net.Http.Headers;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

var apimBaseUrl = builder.Configuration["Upstreams:ApimBaseUrl"];
if (string.IsNullOrWhiteSpace(apimBaseUrl))
    throw new InvalidOperationException("Missing config: Upstreams:ApimBaseUrl (ex: https://fcg-apim-fiap-klztt01.azure-api.net)");

builder.Services.AddHttpClient("apim", client =>
{
    client.BaseAddress = new Uri(apimBaseUrl.TrimEnd('/'));
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

MapProxy("/users");
MapProxy("/games");
MapProxy("/payments");
MapProxy("/search");

app.Run("http://*:80");

void MapProxy(string prefix)
{
    app.MapMethods(prefix, methods, ProxyToApim);
    app.MapMethods($"{prefix}/{{**rest}}", methods, ProxyToApim);
}

static async Task ProxyToApim(
    HttpContext ctx,
    IHttpClientFactory factory,
    IConfiguration cfg,
    ILogger<Program> logger)
{
    try
    {
        var client = factory.CreateClient("apim");
        var target = ctx.Request.Path + ctx.Request.QueryString;

        using var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);

        // Encaminha só os headers essenciais (evita header “estranho” quebrar o upstream)
        if (ctx.Request.Headers.TryGetValue("Authorization", out var auth))
            req.Headers.TryAddWithoutValidation("Authorization", auth.ToArray());

        if (ctx.Request.Headers.TryGetValue("Accept", out var accept))
            req.Headers.TryAddWithoutValidation("Accept", accept.ToArray());

        // Se seu APIM exigir subscription key, configure no App Service:
        // Upstreams__ApimSubscriptionKey = <key>
        var subKey = cfg["Upstreams:ApimSubscriptionKey"];
        if (!string.IsNullOrWhiteSpace(subKey))
            req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", subKey);

        // Body: copia para memória (evita problemas com stream não-seekable / já consumido)
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

        await resp.Content.CopyToAsync(ctx.Response.Body);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Proxy failure TraceId={TraceId} Path={Path}", ctx.TraceIdentifier, ctx.Request.Path);

        ctx.Response.StatusCode = 502;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(
            $"{{\"status\":502,\"message\":\"Proxy error calling APIM\",\"traceId\":\"{ctx.TraceIdentifier}\"}}");
    }
}
