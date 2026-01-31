using Ailos.Transferencia.Api.Application.Services;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Ailos.Transferencia.Api.Infrastructure.Middleware;

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
        // Aplicar apenas em endpoints específicos (POST de transferência)
        return context.Request.Method == HttpMethods.Post 
            && context.Request.Path.StartsWithSegments("/api/transferencia");
    }

    private string? GetIdempotencyKey(HttpContext context)
    {
        // Tenta obter do header X-Idempotency-Key
        if (context.Request.Headers.TryGetValue("X-Idempotency-Key", out var key))
            return key.ToString();

        // Tenta obter do corpo da requisição (para compatibilidade)
        if (context.Request.HasJsonContentType())
        {
            // Nota: Em produção, precisaríamos ler o body de forma assíncrona
            // Por simplicidade, vamos confiar no que o serviço já faz
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
                TransferenciaId = resultadoObj?.TransferenciaId,
                Valor = resultadoObj?.Valor,
                TarifaAplicada = resultadoObj?.TarifaAplicada,
                DataProcessamento = resultadoObj?.DataProcessamento,
                Status = resultadoObj?.Status
            });
        }
    }

    private class IdempotenciaResultado
    {
        public string? TransferenciaId { get; set; }
        public decimal? Valor { get; set; }
        public decimal? TarifaAplicada { get; set; }
        public string? Status { get; set; }
        public string? Erro { get; set; }
        public string? TipoErro { get; set; }
        public DateTime? DataProcessamento { get; set; }
    }
}
