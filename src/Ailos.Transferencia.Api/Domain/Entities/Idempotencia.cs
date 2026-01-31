namespace Ailos.Transferencia.Api.Domain.Entities;

public sealed class Idempotencia
{
    public string Chave { get; private set; }
    public string? Requisicao { get; private set; }
    public string? Resultado { get; private set; }
    public DateTime DataCriacao { get; private set; }

    public Idempotencia(string chave, string? requisicao, string? resultado, DateTime dataCriacao)
    {
        if (string.IsNullOrWhiteSpace(chave))
            throw new ArgumentException("Chave de idempotência é obrigatória");
        
        Chave = chave;
        Requisicao = requisicao;
        Resultado = resultado;
        DataCriacao = dataCriacao;
    }

    public static Idempotencia Criar(string chave, string? requisicao = null, string? resultado = null)
    {
        return new Idempotencia(chave, requisicao, resultado, DateTime.UtcNow);
    }

    public void AtualizarResultado(string resultado)
    {
        if (string.IsNullOrWhiteSpace(resultado))
            throw new ArgumentException("Resultado não pode ser vazio");
        
        Resultado = resultado;
    }
}
