namespace Ailos.ContaCorrente.Api.Domain.Entities;

public sealed class Movimento
{
    public long Id { get; set; }
    public long ContaCorrenteId { get; private set; }
    public DateTime DataMovimento { get; set; }
    public char TipoMovimento { get; private set; } // 'C' ou 'D'
    public decimal Valor { get; private set; }
    public string? Descricao { get; private set; }

    private Movimento() { }

    public Movimento(long contaCorrenteId, char tipoMovimento, decimal valor, string? descricao = null)
    {
        if (contaCorrenteId <= 0)
            throw new ArgumentException("ID da conta corrente inválido");
        
        if (tipoMovimento != 'C' && tipoMovimento != 'D')
            throw new ArgumentException("Tipo de movimento inválido");
        
        if (valor <= 0)
            throw new ArgumentException("Valor deve ser positivo");

        ContaCorrenteId = contaCorrenteId;
        TipoMovimento = tipoMovimento;
        Valor = valor;
        Descricao = descricao;
        DataMovimento = DateTime.UtcNow;
    }

    // Construtor para reconstrução com ID
    public Movimento(long id, long contaCorrenteId, char tipoMovimento, decimal valor, string? descricao = null)
        : this(contaCorrenteId, tipoMovimento, valor, descricao)
    {
        Id = id;
    }
}
