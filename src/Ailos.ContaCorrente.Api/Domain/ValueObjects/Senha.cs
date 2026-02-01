// Senha.cs - CORRIGIDO
using BCrypt.Net; // Adicione este using

public sealed record Senha
{
    public string Hash { get; }
    
    // REMOVA o Salt - BCrypt já gerencia isso internamente
    // public string Salt { get; }

    private Senha(string hash)
    {
        Hash = hash;
    }

    public static Senha Criar(string senhaTexto)
    {
        if (string.IsNullOrWhiteSpace(senhaTexto) || senhaTexto.Length < 6)
            throw new ArgumentException("Senha deve ter no mínimo 6 caracteres");

        // BCrypt gera o hash com salt embutido automaticamente
        var hash = BCrypt.Net.BCrypt.HashPassword(senhaTexto);
        
        // Hash inclui: $2a$12$ + salt (22 chars) + hash (31 chars)
        // Exemplo: "$2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW"

        return new Senha(hash);
    }

    public static Senha Recriar(string hash) =>
        new(hash); // Agora só recebe o hash

    public bool Validar(string senhaTexto) =>
        BCrypt.Net.BCrypt.Verify(senhaTexto, Hash);

    public static implicit operator string(Senha senha) => senha.Hash;
}
