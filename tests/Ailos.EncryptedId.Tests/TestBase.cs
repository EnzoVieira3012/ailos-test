using DotNetEnv;
using System;
using System.IO;

namespace Ailos.EncryptedId.Tests;

public abstract class TestBase
{
    protected readonly string TestSecret;

    protected TestBase()
    {
        // Tenta carregar o .env da raiz do projeto
        var currentDir = Directory.GetCurrentDirectory();
        var envPath = FindEnvFile(currentDir);
        
        if (string.IsNullOrEmpty(envPath))
        {
            throw new InvalidOperationException("Arquivo .env não encontrado. Procurou em: " + currentDir);
        }
        
        Env.Load(envPath);
        
        TestSecret = Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET")
            ?? throw new InvalidOperationException("ENCRYPTED_ID_SECRET não configurada no .env");
    }

    private static string? FindEnvFile(string currentDir)
    {
        var directory = new DirectoryInfo(currentDir);
        
        // Sobe até 5 níveis procurando o .env
        for (int i = 0; i < 10; i++)
        {
            var envPath = Path.Combine(directory.FullName, ".env");
            if (File.Exists(envPath))
            {
                return envPath;
            }
            
            if (directory.Parent == null)
                break;
                
            directory = directory.Parent;
        }
        
        return null;
    }
}
