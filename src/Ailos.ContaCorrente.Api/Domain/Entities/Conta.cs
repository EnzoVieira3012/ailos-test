using Ailos.ContaCorrente.Api.Domain.ValueObjects;

namespace Ailos.ContaCorrente.Api.Domain.Entities;

public sealed class Conta
{
    public long Id { get; private set; }
    public Cpf Cpf { get; private set; } = null!;
    public int Numero { get; private set; }
    public string Nome { get; private set; } = null!;
    public bool Ativo { get; private set; }
    public Senha Senha { get; private set; } = null!;
    public DateTime DataCriacao { get; private set; }
    public DateTime? DataAtualizacao { get; private set; }

    private Conta() { } // Para ORM

    // Construtor público para criação
    public Conta(Cpf cpf, string nome, string senhaTexto)
    {
        Cpf = cpf ?? throw new ArgumentNullException(nameof(cpf));
        Nome = nome ?? throw new ArgumentNullException(nameof(nome));
        Senha = Senha.Criar(senhaTexto);
        Ativo = true;
        DataCriacao = DateTime.UtcNow;
        Numero = 0; // Será gerado
    }

    // Construtor interno para reconstrução do repositório
    internal Conta(long id, Cpf cpf, int numero, string nome, bool ativo, 
                   Senha senha, DateTime dataCriacao, DateTime? dataAtualizacao)
    {
        Id = id;
        Cpf = cpf;
        Numero = numero;
        Nome = nome;
        Ativo = ativo;
        Senha = senha;
        DataCriacao = dataCriacao;
        DataAtualizacao = dataAtualizacao;
    }

    public void Inativar()
    {
        if (!Ativo)
            throw new InvalidOperationException("Conta já está inativa");
        
        Ativo = false;
        DataAtualizacao = DateTime.UtcNow;
    }

    public bool ValidarSenha(string senhaTexto) =>
        Senha.Validar(senhaTexto);

    public void DefinirNumero(int numero)
    {
        if (numero <= 0)
            throw new ArgumentException("Número da conta deve ser positivo");
        
        Numero = numero;
    }

    public void AtualizarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome não pode ser vazio");
        
        Nome = nome;
        DataAtualizacao = DateTime.UtcNow;
    }
}
