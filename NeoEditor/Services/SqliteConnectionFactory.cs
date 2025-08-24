using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace NeoEditor.Services;

public class SqliteConnectionFactory
{
    //获取web 中的配置文件
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ??
                            throw new InvalidOperationException();
    }

    public IDbConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        if (connection.State == ConnectionState.Closed) connection.Open();
        return connection;
    }
}