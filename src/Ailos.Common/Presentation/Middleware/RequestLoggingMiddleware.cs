using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Ailos.Common.Presentation.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;
        
        _logger.LogDebug("➡️ [REQUEST] {Method} {Path}{QueryString}", 
            request.Method, request.Path, request.QueryString);
        
        // Log body para POST/PUT (exceto para dados sensíveis)
        if (request.Method == HttpMethods.Post || request.Method == HttpMethods.Put)
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            
            // Não logar senhas ou tokens
            if (!body.Contains("senha", StringComparison.OrdinalIgnoreCase) && 
                !body.Contains("password", StringComparison.OrdinalIgnoreCase) &&
                !body.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("📝 Request Body: {Body}", body.Length > 500 ? body.Substring(0, 500) + "..." : body);
            }
        }

        try
        {
            await _next(context);
            stopwatch.Stop();
            
            _logger.LogDebug("⬅️ [RESPONSE] {Method} {Path} - {StatusCode} ({Duration}ms)", 
                request.Method, request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "💥 [ERROR] {Method} {Path} - Exception after {Duration}ms", 
                request.Method, request.Path, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
