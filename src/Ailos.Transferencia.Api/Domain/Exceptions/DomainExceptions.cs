namespace Ailos.Transferencia.Api.Domain.Exceptions;

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

public class SaldoInsuficienteException : DomainException
{
    public SaldoInsuficienteException() : base("Saldo insuficiente", "INSUFFICIENT_BALANCE") { }
}

public class TransferenciaMesmaContaException : DomainException
{
    public TransferenciaMesmaContaException() : base("Conta de origem e destino não podem ser iguais", "SAME_ACCOUNT") { }
}
