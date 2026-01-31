namespace Ailos.Transferencia.Api.Domain.Entities;

public sealed class TransferenciaEntity
{
    public long Id { get; set; } // MUDADO: private set -> set
    public long ContaCorrenteOrigemId { get; private set; }
    public long ContaCorrenteDestinoId { get; private set; }
    public DateTime DataMovimento { get; set; } // MUDADO: private set -> set
    public decimal Valor { get; private set; }
    public decimal? TarifaAplicada { get; set; } // MUDADO: private set -> set
    public TransferenciaStatus Status { get; set; } // MUDADO: private set -> set
    public string? MensagemErro { get; set; } // MUDADO: private set -> set
    public string? IdentificacaoRequisicao { get; private set; }

    // Construtor privado para ORM/Dapper
    private TransferenciaEntity() { }

    // Construtor principal
    public TransferenciaEntity(
        long contaCorrenteOrigemId,
        long contaCorrenteDestinoId,
        decimal valor,
        string? identificacaoRequisicao = null)
    {
        if (contaCorrenteOrigemId <= 0)
            throw new ArgumentException("ID da conta de origem inválido");
        
        if (contaCorrenteDestinoId <= 0)
            throw new ArgumentException("ID da conta de destino inválido");
        
        if (valor <= 0)
            throw new ArgumentException("Valor deve ser positivo");
        
        if (contaCorrenteOrigemId == contaCorrenteDestinoId)
            throw new ArgumentException("Conta de origem e destino não podem ser iguais");

        ContaCorrenteOrigemId = contaCorrenteOrigemId;
        ContaCorrenteDestinoId = contaCorrenteDestinoId;
        Valor = valor;
        Status = TransferenciaStatus.Processando;
        DataMovimento = DateTime.UtcNow;
        IdentificacaoRequisicao = identificacaoRequisicao;
    }

    // Construtor para reconstrução completa (usado pelo repositório)
    public TransferenciaEntity(
        long id,
        long contaCorrenteOrigemId,
        long contaCorrenteDestinoId,
        DateTime dataMovimento,
        decimal valor,
        decimal? tarifaAplicada,
        TransferenciaStatus status,
        string? mensagemErro,
        string? identificacaoRequisicao)
    {
        Id = id;
        ContaCorrenteOrigemId = contaCorrenteOrigemId;
        ContaCorrenteDestinoId = contaCorrenteDestinoId;
        DataMovimento = dataMovimento;
        Valor = valor;
        TarifaAplicada = tarifaAplicada;
        Status = status;
        MensagemErro = mensagemErro;
        IdentificacaoRequisicao = identificacaoRequisicao;
    }

    public void AplicarTarifa(decimal tarifa)
    {
        if (tarifa < 0)
            throw new ArgumentException("Tarifa não pode ser negativa");
        
        TarifaAplicada = tarifa;
    }

    public void Concluir()
    {
        if (Status != TransferenciaStatus.Processando)
            throw new InvalidOperationException($"Transferência não está em processamento. Status atual: {Status}");
        
        Status = TransferenciaStatus.Concluida;
    }

    public void Falhar(string mensagemErro)
    {
        if (Status != TransferenciaStatus.Processando)
            throw new InvalidOperationException($"Transferência não está em processamento. Status atual: {Status}");
        
        Status = TransferenciaStatus.Falha;
        MensagemErro = mensagemErro;
    }

    public void Estornar()
    {
        if (Status != TransferenciaStatus.Concluida)
            throw new InvalidOperationException("Só é possível estornar transferências concluídas");
        
        Status = TransferenciaStatus.Estornada;
    }
}

public enum TransferenciaStatus
{
    Processando,
    Concluida,
    Falha,
    Estornada
}
