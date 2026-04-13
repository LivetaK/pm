using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace pm.Infrastructure;

public class DapperContext
{
    private readonly string _connectionString;

    public DapperContext(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public IDbConnection CreateConnection() =>
        new NpgsqlConnection(_connectionString);
}