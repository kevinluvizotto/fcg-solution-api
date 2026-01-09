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

// Portal estático
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/", context =>
{
    context.Response.Redirect("/login.html");
    return Task.CompletedTask;
});

// Proxy (BFF)
string[] methods = ["GET", "POST", "PUT", "DELETE", "PATCH"];

MapProxy("/users");
MapProxy("/games");
MapProxy("/payments");
MapProxy("/search");

app.Run("http://*:80");

// ---------------- helpers ----------------

void MapProxy(string prefix)
{
    app.MapMethods(prefix, methods, ProxyToApim);
    app.MapMethods($"{prefix}/{{**rest}}", methods, ProxyToApim);
}

static async Task ProxyToApim(HttpContext ctx, IHttpClientFactory factory, IConfiguration cfg)
{
    var client = factory.CreateClient("apim");

    // Mantém path + query
    var target = ctx.Request.Path + ctx.Request.QueryString;

    using var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);

    // Se tiver body, encaminha como stream + Content-Type correto
    var hasBody =
        ctx.Request.ContentLength.HasValue && ctx.Request.ContentLength.Value > 0;

    if (hasBody && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method))
    {
        // garante que o Body pode ser lido/encaminhado
        ctx.Request.EnableBuffering();
        ctx.Request.Body.Position = 0;

        req.Content = new StreamContent(ctx.Request.Body);

        // Content-Type tem que ir no Content.Headers (não no req.Headers)
        var ct = ctx.Request.ContentType;
        if (!string.IsNullOrWhiteSpace(ct))
            req.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ct);
        else
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    }

    // Copia headers (exceto hop-by-hop e headers de content)
    foreach (var h in ctx.Request.Headers)
    {
        var key = h.Key;

        if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
        if (key.Equals("Connection", StringComparison.OrdinalIgnoreCase)) continue;
        if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
        if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue; // IMPORTANT
        if (key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) continue;

        if (!req.Headers.TryAddWithoutValidation(key, h.Value.ToArray()))
            req.Content?.Headers.TryAddWithoutValidation(key, h.Value.ToArray());
    }

    // (Opcional) subscription key se necessário
    var subKey = cfg["Upstreams:ApimSubscriptionKey"];
    if (!string.IsNullOrWhiteSpace(subKey) && !req.Headers.Contains("Ocp-Apim-Subscription-Key"))
        req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", subKey);

    using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);

    ctx.Response.StatusCode = (int)resp.StatusCode;

    foreach (var h in resp.Headers)
        ctx.Response.Headers[h.Key] = h.Value.ToArray();

    foreach (var h in resp.Content.Headers)
        ctx.Response.Headers[h.Key] = h.Value.ToArray();

    ctx.Response.Headers.Remove("transfer-encoding");

    await resp.Content.CopyToAsync(ctx.Response.Body);
}
