using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRouting();

var app = builder.Build();

// Serve arquivos estáticos
app.UseDefaultFiles();
app.UseStaticFiles();

// Redireciona raiz -> login.html
app.MapGet("/", context =>
{
    context.Response.Redirect("/login.html");
    return Task.CompletedTask;
});

app.UseHttpsRedirection();
app.Run("http://*:80");
