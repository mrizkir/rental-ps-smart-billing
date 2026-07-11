using Microsoft.Data.SqlClient;

namespace rental_ps_smart_billing.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = SqlConnectionHelper.Normalize(connectionString);
    }

    public string ConnectionString => _connectionString;

    public SqlConnection Create() => new(_connectionString);
}
