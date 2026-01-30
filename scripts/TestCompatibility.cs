using Ailos.EncryptedId;
using DotNetEnv;

class Program
{
    static void Main()
    {
        // Carregar do .env
        Env.Load();
        
        var secret = Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET")
            ?? throw new InvalidOperationException("ENCRYPTED_ID_SECRET não configurada no .env");
            
        var service = EncryptedIdFactory.CreateService(secret);
        
        // IDs de teste
        var testIds = new long[] { 1, 123, 12345, 999999, 123456789 };
        
        Console.WriteLine("=== Teste de Compatibilidade ===");
        Console.WriteLine($"Secret usado: {secret.Substring(0, Math.Min(10, secret.Length))}...");
        Console.WriteLine();
        
        foreach (var id in testIds)
        {
            try
            {
                var encrypted = service.Encrypt(id);
                var decrypted = service.Decrypt(encrypted);
                var isValid = id == decrypted;
                
                Console.WriteLine($"ID: {id}");
                Console.WriteLine($"  Token: {encrypted.Value}");
                Console.WriteLine($"  Decriptado: {decrypted}");
                Console.WriteLine($"  Válido: {isValid}");
                Console.WriteLine($"  Tamanho token: {encrypted.Value.Length} chars");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERRO com ID {id}: {ex.Message}");
                Console.WriteLine();
            }
        }
        
        // Testar se é determinístico
        Console.WriteLine("=== Teste Determinístico ===");
        var testId = 42L;
        var token1 = service.Encrypt(testId);
        var token2 = service.Encrypt(testId);
        Console.WriteLine($"ID: {testId}");
        Console.WriteLine($"  Token 1: {token1.Value}");
        Console.WriteLine($"  Token 2: {token2.Value}");
        Console.WriteLine($"  São iguais: {token1.Value == token2.Value}");
    }
}
