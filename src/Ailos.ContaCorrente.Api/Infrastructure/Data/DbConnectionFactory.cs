using System.Data;
using Microsoft.Data.Sqlite;

namespace Ailos.ContaCorrente.Api.Infrastructure.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=ailos.db;";
    }

    public IDbConnection CreateConnection() =>
        new SqliteConnection(_connectionString);
}
