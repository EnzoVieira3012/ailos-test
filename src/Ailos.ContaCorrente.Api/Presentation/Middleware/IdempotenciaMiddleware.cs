// src/Ailos.ContaCorrente.Api/Presentation/Middleware/IdempotenciaMiddleware.cs
using Ailos.ContaCorrente.Api.Application.Services;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Ailos.ContaCorrente.Api.Presentation.Middleware;

public class IdempotenciaMiddleware
{
    private readonly RequestDelegate _next;

    public IdempotenciaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotenciaService idempotenciaService)
    {
        // Verificar se é uma requisição que precisa de idempotência
        if (ShouldCheckIdempotency(context))
        {
            var identificacaoRequisicao = GetIdempotencyKey(context);
            
            if (!string.IsNullOrEmpty(identificacaoRequisicao))
            {
                var jaProcessada = await idempotenciaService
                    .RequisicaoJaProcessadaAsync(identificacaoRequisicao, context.RequestAborted);
                
                if (jaProcessada)
                {
                    var resultado = await idempotenciaService
                        .ObterResultadoAsync(identificacaoRequisicao, context.RequestAborted);
                    
                    if (!string.IsNullOrEmpty(resultado))
                    {
                        // Retornar resultado anterior
                        await ReturnCachedResult(context, resultado);
                        return;
                    }
                }
            }
        }

        await _next(context);
    }

    private bool ShouldCheckIdempotency(HttpContext context)
    {
        // Aplicar apenas em endpoints específicos (POST de movimentação)
        return context.Request.Method == HttpMethods.Post 
            && context.Request.Path.StartsWithSegments("/api/movimentacao");
    }

    private string? GetIdempotencyKey(HttpContext context)
    {
        // Tenta obter do header X-Idempotency-Key
        if (context.Request.Headers.TryGetValue("X-Idempotency-Key", out var key))
            return key.ToString();

        // Tenta obter do corpo da requisição (para compatibilidade)
        if (context.Request.HasJsonContentType())
        {
            // Nota: Em produção, isso seria mais sofisticado
            // com leitura assíncrona do body
            return null;
        }

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
