using Microsoft.Data.SqlClient;

namespace rental_ps_smart_billing.Data;

public interface ISqlConnectionFactory
{
    string ConnectionString { get; }
    SqlConnection Create();
}
