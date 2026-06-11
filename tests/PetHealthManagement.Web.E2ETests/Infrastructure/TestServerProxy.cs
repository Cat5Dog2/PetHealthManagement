using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PetHealthManagement.Web.E2ETests.Infrastructure;

internal sealed class TestServerProxy<TProgram>(WebApplicationFactory<TProgram> factory) : IAsyncDisposable
    where TProgram : class
{
    private readonly HttpClient _appClient = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false,
        BaseAddress = new Uri("http://127.0.0.1")
    });

    private WebApplication? _proxyApp;

    public Uri BaseAddress { get; private set; } = null!;

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.Run(ForwardAsync);

        _proxyApp = app;
        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?
            .Select(NormalizeLoopbackAddress)
            .FirstOrDefault(x => x.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("Could not determine the E2E proxy address.");
        }

        BaseAddress = new Uri(address);
    }

    public async ValueTask DisposeAsync()
    {
        _appClient.Dispose();

        if (_proxyApp is not null)
        {
            await _proxyApp.DisposeAsync();
        }
    }

    private async Task ForwardAsync(HttpContext context)
    {
        using var requestMessage = CreateRequestMessage(context);
        using var responseMessage = await _appClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        context.Response.StatusCode = (int)responseMessage.StatusCode;

        foreach (var header in responseMessage.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in responseMessage.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        RemoveHopByHopHeaders(context.Response.Headers);
        await responseMessage.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    private HttpRequestMessage CreateRequestMessage(HttpContext context)
    {
        var requestPath = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        var requestMessage = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            new Uri(BaseAddress, requestPath));

        if (RequestMayHaveBody(context.Request))
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
        }

        foreach (var header in context.Request.Headers)
        {
            if (ShouldSkipRequestHeader(header.Key))
            {
                continue;
            }

            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                requestMessage.Content ??= new StreamContent(context.Request.Body);
                requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        return requestMessage;
    }

    private static bool RequestMayHaveBody(HttpRequest request)
    {
        return request.ContentLength.GetValueOrDefault() > 0
            || request.Headers.ContainsKey("Transfer-Encoding");
    }

    private static bool ShouldSkipRequestHeader(string headerName)
    {
        return string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Upgrade", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Proxy-Connection", StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveHopByHopHeaders(IHeaderDictionary headers)
    {
        headers.Remove("connection");
        headers.Remove("keep-alive");
        headers.Remove("proxy-authenticate");
        headers.Remove("proxy-authorization");
        headers.Remove("te");
        headers.Remove("trailer");
        headers.Remove("transfer-encoding");
        headers.Remove("upgrade");
    }

    private static string NormalizeLoopbackAddress(string address)
    {
        return address
            .Replace("://0.0.0.0:", "://127.0.0.1:", StringComparison.Ordinal)
            .Replace("://[::]:", "://127.0.0.1:", StringComparison.Ordinal);
    }
}
