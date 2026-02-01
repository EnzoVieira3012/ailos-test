using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ailos.Common.Domain.Exceptions;


namespace Ailos.Common.Presentation.Filters;

public class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        context.ExceptionHandled = true;

        var problemDetails = context.Exception switch
        {
            DomainException domainEx => new ProblemDetails
            {
                Title = "Erro de Domínio",
                Detail = domainEx.Message,
                Status = domainEx.StatusCode,
                Extensions = { ["errorCode"] = domainEx.ErrorCode }
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Title = "Não autorizado",
                Detail = context.Exception.Message,
                Status = 401
            },
            _ => new ProblemDetails
            {
                Title = "Erro interno",
                Detail = "Ocorreu um erro inesperado",
                Status = 500
            }
        };

        context.Result = new ObjectResult(problemDetails) 
        { 
            StatusCode = problemDetails.Status 
        };
    }
}
