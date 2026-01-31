// src/Ailos.ContaCorrente.Api/Presentation/Filters/ApiExceptionFilter.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ailos.ContaCorrente.Api.Presentation.Filters;

public class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        context.Result = context.Exception switch
        {
            UnauthorizedAccessException => new UnauthorizedObjectResult(new
            {
                Title = "Não autorizado",
                Detail = context.Exception.Message,
                Status = StatusCodes.Status401Unauthorized
            }),
            ArgumentException => new BadRequestObjectResult(new
            {
                Title = "Argumento inválido",
                Detail = context.Exception.Message,
                Status = StatusCodes.Status400BadRequest
            }),
            InvalidOperationException => new BadRequestObjectResult(new
            {
                Title = "Operação inválida",
                Detail = context.Exception.Message,
                Status = StatusCodes.Status400BadRequest
            }),
            _ => new ObjectResult(new
            {
                Title = "Erro interno",
                Detail = "Ocorreu um erro interno no servidor",
                Status = StatusCodes.Status500InternalServerError
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            }
        };
        
        context.ExceptionHandled = true;
    }
}
