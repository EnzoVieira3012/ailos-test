using System.Data;

namespace Ailos.Common.Infrastructure.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    string ConnectionString { get; }
}
