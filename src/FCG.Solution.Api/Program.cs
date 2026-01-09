using System.Net.Http.Headers;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// === Config do APIM ===
var apimBaseUrl = builder.Configuration["Upstreams:ApimBaseUrl"];
if (string.IsNullOrWhiteSpace(apimBaseUrl))
{
    throw new InvalidOperationException("Missing config: Upstreams:ApimBaseUrl (ex: https://fcg-apim-fiap-klztt01.azure-api.net)");
}

builder.Services.AddHttpClient("apim", client =>
{
    client.BaseAddress = new Uri(apimBaseUrl.TrimEnd('/'));
    client.Timeout = TimeSpan.FromSeconds(100);
});

var app = builder.Build();

// Se estiver no Azure, isso ajuda a respeitar https do front-end
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});

// Serve portal (wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

// Redireciona raiz -> login.html
app.MapGet("/", context =>
{
    context.Response.Redirect("/login.html");
    return Task.CompletedTask;
});

// ===== Reverse proxy para APIM =====
// Portal vai chamar (same-origin): /users/login, /users/me, /users/users, /games/games, etc.
// E o solution-api vai encaminhar para o APIM mantendo path + query.
string[] methods = ["GET", "POST", "PUT", "DELETE", "PATCH"];

MapProxy("/users");
MapProxy("/games");
MapProxy("/payments");
MapProxy("/search");

app.Run("http://*:80");

// ----------------- helpers -----------------

void MapProxy(string prefix)
{
    app.MapMethods(prefix, methods, ProxyToApim);
    app.MapMethods($"{prefix}/{{**rest}}", methods, ProxyToApim);
}

static async Task ProxyToApim(HttpContext ctx, IHttpClientFactory factory, IConfiguration cfg)
{
    var client = factory.CreateClient("apim");

    // Mantém path + query exatamente como o portal pediu
    var target = ctx.Request.Path + ctx.Request.QueryString;

    using var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);

    // Copia body quando existir
    if (ctx.Request.ContentLength > 0 && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method))
    {
        req.Content = new StreamContent(ctx.Request.Body);

        if (!string.IsNullOrWhiteSpace(ctx.Request.ContentType))
            req.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ctx.Request.ContentType);
    }

    // Copia headers (inclui Authorization)
    foreach (var h in ctx.Request.Headers)
    {
        if (h.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
        if (h.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)) continue;
        if (h.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;

        if (!req.Headers.TryAddWithoutValidation(h.Key, h.Value.ToArray()))
            req.Content?.Headers.TryAddWithoutValidation(h.Key, h.Value.ToArray());
    }

    // (Opcional) Se seu APIM exigir subscription key, configure:
    // Upstreams__ApimSubscriptionKey = <key>
    var subKey = cfg["Upstreams:ApimSubscriptionKey"];
    if (!string.IsNullOrWhiteSpace(subKey) && !req.Headers.Contains("Ocp-Apim-Subscription-Key"))
        req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", subKey);

    using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);

    ctx.Response.StatusCode = (int)resp.StatusCode;

    // Copia headers da resposta
    foreach (var h in resp.Headers)
        ctx.Response.Headers[h.Key] = h.Value.ToArray();

    foreach (var h in resp.Content.Headers)
        ctx.Response.Headers[h.Key] = h.Value.ToArray();

    // Evita problemas com header hop-by-hop
    ctx.Response.Headers.Remove("transfer-encoding");

    await resp.Content.CopyToAsync(ctx.Response.Body);
}
