using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CopilotCmdlets.Tests.EndToEnd;

internal sealed class OpenAiTestServer : IAsyncDisposable
{
    private readonly HttpListener listener;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task serverTask;
    private readonly ConcurrentQueue<CapturedHttpRequest> requests = new();

    private OpenAiTestServer(HttpListener listener, Uri baseUri)
    {
        this.listener = listener;
        BaseUri = baseUri;
        serverTask = RunAsync();
    }

    internal Uri BaseUri { get; }

    internal IReadOnlyList<CapturedHttpRequest> Requests => requests.ToArray();

    internal static OpenAiTestServer Start()
    {
        var baseUri = CreateLoopbackUri();
        var listener = new HttpListener();
        listener.Prefixes.Add(baseUri.ToString());
        listener.Start();
        return new OpenAiTestServer(listener, baseUri);
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        listener.Stop();
        try
        {
            await serverTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpListenerException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Close();
            cancellation.Dispose();
        }
    }

    private async Task RunAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellation.Token);
            await HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var body = await ReadBodyAsync(context.Request);
        requests.Enqueue(new CapturedHttpRequest(
            context.Request.HttpMethod,
            context.Request.Url!.AbsolutePath,
            context.Request.Headers["Authorization"],
            body));

        if (context.Request.HttpMethod != "POST" ||
            !context.Request.Url.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new
            {
                error = new { message = "fixture endpoint not found" }
            });
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.SendChunked = true;

        var chunks = new[]
        {
            JsonSerializer.Serialize(new
            {
                id = "chatcmpl-fixture",
                @object = "chat.completion.chunk",
                created = 1,
                model = "fixture-model",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new { role = "assistant", content = "byok-ok" },
                        finish_reason = (string?)null
                    }
                }
            }),
            JsonSerializer.Serialize(new
            {
                id = "chatcmpl-fixture",
                @object = "chat.completion.chunk",
                created = 1,
                model = "fixture-model",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new { },
                        finish_reason = "stop"
                    }
                }
            })
        };

        await using var writer = new StreamWriter(
            context.Response.OutputStream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            leaveOpen: false);
        foreach (var chunk in chunks)
        {
            await writer.WriteAsync($"data: {chunk}\n\n");
            await writer.FlushAsync();
        }
        await writer.WriteAsync("data: [DONE]\n\n");
        await writer.FlushAsync();
    }

    private static Uri CreateLoopbackUri()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        return new Uri($"http://127.0.0.1:{port}/");
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        return await reader.ReadToEndAsync();
    }

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        object body)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(body);
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json";
        response.ContentLength64 = content.Length;
        await response.OutputStream.WriteAsync(content);
        response.Close();
    }
}

internal sealed class OAuthMcpTestServer : IAsyncDisposable
{
    private const string ResourceMetadataPath = "/.well-known/oauth-protected-resource";
    private readonly HttpListener listener;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task serverTask;
    private readonly string expectedToken;
    private readonly ConcurrentQueue<CapturedHttpRequest> requests = new();

    private OAuthMcpTestServer(HttpListener listener, Uri baseUri, string expectedToken)
    {
        this.listener = listener;
        this.expectedToken = expectedToken;
        BaseUri = baseUri;
        serverTask = RunAsync();
    }

    internal Uri BaseUri { get; }

    internal Uri McpUri => new(BaseUri, "mcp");

    internal IReadOnlyList<CapturedHttpRequest> Requests => requests.ToArray();

    internal static OAuthMcpTestServer Start(string expectedToken)
    {
        var baseUri = CreateLoopbackUri();
        var listener = new HttpListener();
        listener.Prefixes.Add(baseUri.ToString());
        listener.Start();
        return new OAuthMcpTestServer(listener, baseUri, expectedToken);
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        listener.Stop();
        try
        {
            await serverTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpListenerException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Close();
            cancellation.Dispose();
        }
    }

    private async Task RunAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellation.Token);
            await HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var path = request.Url!.AbsolutePath;

        if (request.HttpMethod == "GET" && path == ResourceMetadataPath)
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
            {
                resource = McpUri.ToString(),
                authorization_servers = new[] { BaseUri.ToString().TrimEnd('/') },
                scopes_supported = new[] { "mcp.read" },
                bearer_methods_supported = new[] { "header" }
            });
            return;
        }

        if (request.HttpMethod == "GET" &&
            path == "/.well-known/oauth-authorization-server")
        {
            var authority = BaseUri.ToString().TrimEnd('/');
            await WriteJsonAsync(context.Response, HttpStatusCode.OK, new
            {
                issuer = authority,
                authorization_endpoint = $"{authority}/authorize",
                token_endpoint = $"{authority}/token",
                response_types_supported = new[] { "code" },
                grant_types_supported = new[] { "authorization_code" }
            });
            return;
        }

        if (path != "/mcp")
        {
            await WriteJsonAsync(
                context.Response,
                HttpStatusCode.NotFound,
                new { error = "not_found" });
            return;
        }

        var body = await ReadBodyAsync(request);
        var authorization = request.Headers["Authorization"];
        requests.Enqueue(new CapturedHttpRequest(
            request.HttpMethod,
            path,
            authorization,
            body));

        if (!string.Equals(authorization, $"Bearer {expectedToken}", StringComparison.Ordinal))
        {
            var metadata = new Uri(BaseUri, ResourceMetadataPath.TrimStart('/'));
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.Headers["WWW-Authenticate"] =
                $"Bearer resource_metadata=\"{metadata}\", scope=\"mcp.read\", error=\"invalid_token\"";
            await WriteJsonBodyAsync(context.Response, new { error = "missing_or_invalid_token" });
            return;
        }

        if (request.HttpMethod != "POST")
        {
            await WriteJsonAsync(
                context.Response,
                HttpStatusCode.MethodNotAllowed,
                new { error = "method_not_allowed" });
            return;
        }

        var message = JsonNode.Parse(body);
        var response = message is JsonArray batch
            ? new JsonArray(batch.Select(HandleMessage).Where(item => item is not null).ToArray())
            : HandleMessage(message);

        if (response is null || response is JsonArray { Count: 0 })
        {
            context.Response.StatusCode = (int)HttpStatusCode.Accepted;
            context.Response.Headers["mcp-session-id"] = "oauth-test-session";
            context.Response.Close();
            return;
        }

        context.Response.Headers["mcp-session-id"] = "oauth-test-session";
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response);
    }

    private static JsonNode? HandleMessage(JsonNode? message)
    {
        if (message is not JsonObject request ||
            request["id"] is null)
        {
            return null;
        }

        var id = request["id"]!.DeepClone();
        var method = request["method"]?.GetValue<string>();
        return method switch
        {
            "initialize" => new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["protocolVersion"] =
                        request["params"]?["protocolVersion"]?.DeepClone() ?? "2025-03-26",
                    ["capabilities"] = new JsonObject
                    {
                        ["tools"] = new JsonObject()
                    },
                    ["serverInfo"] = new JsonObject
                    {
                        ["name"] = "oauth-test-server",
                        ["version"] = "1.0.0"
                    }
                }
            },
            "tools/list" => new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = new JsonObject
                {
                    ["tools"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "whoami",
                            ["description"] = "Returns the authenticated test principal.",
                            ["inputSchema"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject(),
                                ["additionalProperties"] = false
                            }
                        }
                    }
                }
            },
            _ => new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JsonObject
                {
                    ["code"] = -32601,
                    ["message"] = $"Method not found: {method}"
                }
            }
        };
    }

    private static Uri CreateLoopbackUri()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        return new Uri($"http://127.0.0.1:{port}/");
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        return await reader.ReadToEndAsync();
    }

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        object body)
    {
        response.StatusCode = (int)statusCode;
        await WriteJsonBodyAsync(response, body);
    }

    private static async Task WriteJsonBodyAsync(HttpListenerResponse response, object body)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(body);
        response.ContentType = "application/json";
        response.ContentLength64 = content.Length;
        await response.OutputStream.WriteAsync(content);
        response.Close();
    }
}

internal sealed record CapturedHttpRequest(
    string Method,
    string Path,
    string? Authorization,
    string Body);
