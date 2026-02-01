using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Ailos.Common.Presentation.Middleware;

public class IdempotenciaMiddleware
{
    private readonly RequestDelegate _next;

    public IdempotenciaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Este middleware agora é apenas uma estrutura base
        // As implementações específicas devem ser feitas em cada API
        
        await _next(context);
    }

    private bool ShouldCheckIdempotency(HttpContext context)
    {
        // Aplicar apenas em endpoints específicos (POST de movimentação)
        return context.Request.Method == HttpMethods.Post;
    }

    private string? GetIdempotencyKey(HttpContext context)
    {
        // Tenta obter do header X-Idempotency-Key
        if (context.Request.Headers.TryGetValue("X-Idempotency-Key", out var key))
            return key.ToString();

        return null;
    }

    private async Task ReturnCachedResult(HttpContext context, string resultado)
    {
        var resultadoObj = JsonSerializer.Deserialize<IdempotenciaResultado>(resultado);
        
        if (resultadoObj?.Erro != null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                Title = "Erro em requisição anterior",
                Detail = resultadoObj.Erro,
                Status = StatusCodes.Status400BadRequest,
                Extensions = new { errorType = resultadoObj.TipoErro }
            });
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new
            {
                MovimentoId = resultadoObj?.MovimentoId,
                SaldoAtual = resultadoObj?.SaldoAtual,
                DataProcessamento = resultadoObj?.DataProcessamento
            });
        }
    }

    private class IdempotenciaResultado
    {
        public string? MovimentoId { get; set; }
        public decimal? SaldoAtual { get; set; }
        public string? Erro { get; set; }
        public string? TipoErro { get; set; }
        public DateTime? DataProcessamento { get; set; }
    }
}
