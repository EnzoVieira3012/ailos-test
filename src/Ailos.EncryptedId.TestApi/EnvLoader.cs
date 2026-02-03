using DotNetEnv;
using System;
using System.IO;

namespace Ailos.EncryptedId.TestApi;

public static class EnvLoader
{
    public static void LoadFromRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var envPath = FindEnvFile(currentDir);
        
        if (string.IsNullOrEmpty(envPath))
        {
            throw new FileNotFoundException($"Arquivo .env não encontrado. Procurou a partir de: {currentDir}");
        }
        
        Console.WriteLine($"✅ Carregando .env de: {envPath}");
        Env.Load(envPath);
    }

    private static string? FindEnvFile(string currentDir)
    {
        var directory = new DirectoryInfo(currentDir);
        
        // Sobe até 10 níveis procurando o .env
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
