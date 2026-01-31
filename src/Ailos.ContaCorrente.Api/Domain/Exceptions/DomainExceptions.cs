// src/Ailos.ContaCorrente.Api/Domain/Exceptions/DomainExceptions.cs
namespace Ailos.ContaCorrente.Api.Domain.Exceptions;

public class DomainException : Exception
{
    public string ErrorType { get; }

    public DomainException(string message, string errorType) : base(message)
    {
        ErrorType = errorType;
    }
}

public class ContaInativaException : DomainException
{
    public ContaInativaException() : base("Conta corrente inativa", "INACTIVE_ACCOUNT") { }
}

public class ContaNaoEncontradaException : DomainException
{
    public ContaNaoEncontradaException() : base("Conta corrente não cadastrada", "INVALID_ACCOUNT") { }
}

public class ValorInvalidoException : DomainException
{
    public ValorInvalidoException() : base("Valor deve ser positivo", "INVALID_VALUE") { }
}

public class TipoMovimentoInvalidoException : DomainException
{
    public TipoMovimentoInvalidoException() : base("Tipo de movimento inválido", "INVALID_TYPE") { }
    public TipoMovimentoInvalidoException(string message) : base(message, "INVALID_TYPE") { }
}

public class SaldoInsuficienteException : DomainException
{
    public SaldoInsuficienteException() : base("Saldo insuficiente", "INSUFFICIENT_BALANCE") { }
}
