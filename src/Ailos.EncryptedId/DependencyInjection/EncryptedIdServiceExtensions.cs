using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ailos.EncryptedId.DependencyInjection;

public static class EncryptedIdServiceExtensions
{
    public static IServiceCollection AddEncryptedId(this IServiceCollection services, IConfiguration configuration)
    {
        var secretKey = configuration["ENCRYPTED_ID_SECRET"] 
            ?? throw new InvalidOperationException("ENCRYPTED_ID_SECRET não configurada");
        
        services.AddSingleton<IEncryptedIdService>(_ => 
            EncryptedIdFactory.CreateService(secretKey));
        
        return services;
    }
}
