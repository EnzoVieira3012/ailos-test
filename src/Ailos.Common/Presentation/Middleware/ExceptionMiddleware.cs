using System.Net;
using System.Text.Json;
using Ailos.Common.Domain.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Ailos.Common.Application.Middleware;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";

            var response = new
            {
                title = ex.ErrorCode,
                status = ex.StatusCode,
                detail = ex.Message
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                title = "INTERNAL_ERROR",
                status = 500,
                detail = ex.Message // ⚠️ deixa assim para o teste
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
