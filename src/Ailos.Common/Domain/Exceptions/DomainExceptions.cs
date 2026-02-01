namespace Ailos.Common.Domain.Exceptions;

#region Base

public abstract class DomainException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }

    protected DomainException(
        string message,
        string errorCode,
        int statusCode = 400
    ) : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}

#endregion

#region Genéricas (Common)

public sealed class ValidationException : DomainException
{
    public ValidationException(string message)
        : base(message, "VALIDATION_ERROR", 400) { }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entity, object id)
        : base($"{entity} com ID {id} não encontrado", "NOT_FOUND", 404) { }
}

public sealed class DuplicateCpfException : DomainException
{
    public DuplicateCpfException()
        : base("CPF já cadastrado", "DUPLICATE_DOCUMENT", 400) { }
}

#endregion

#region Conta Corrente

public sealed class ContaInativaException : DomainException
{
    public ContaInativaException()
        : base("Conta corrente inativa", "INACTIVE_ACCOUNT", 400) { }
}

public sealed class ContaNaoEncontradaException : DomainException
{
    public ContaNaoEncontradaException()
        : base("Conta corrente não cadastrada", "INVALID_ACCOUNT", 404) { }
}

public sealed class ValorInvalidoException : DomainException
{
    public ValorInvalidoException()
        : base("Valor deve ser positivo", "INVALID_VALUE", 400) { }
}

public sealed class TipoMovimentoInvalidoException : DomainException
{
    public TipoMovimentoInvalidoException(string? message = null)
        : base(
            message ?? "Tipo de movimento inválido",
            "INVALID_TYPE",
            400
        ) { }
}

public sealed class SaldoInsuficienteException : DomainException
{
    public SaldoInsuficienteException()
        : base("Saldo insuficiente", "INSUFFICIENT_BALANCE", 400) { }
}

#endregion