using Microsoft.Data.SqlClient;

namespace BTQCDar.Services
{
    /// <summary>
    /// Reads connection strings directly from IConfiguration using full key path
    /// "ConnectionStrings:BT_QCDAR" — avoids GetConnectionString() shorthand
    /// which can be overridden by environment variables on IIS.
    /// </summary>
    public class DbService : IDbService
    {
        private readonly IConfiguration _config;

        public DbService(IConfiguration config)
        {
            _config = config;
        }

        // Read with full key path every time — no caching, no shorthand
        public SqlConnection GetQCDarConnection()
            => new SqlConnection(_config["ConnectionStrings:BT_QCDAR"]);

        public SqlConnection GetHRConnection()
            => new SqlConnection(_config["ConnectionStrings:BT_HR"]);
    }
}