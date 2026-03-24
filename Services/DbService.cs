using Microsoft.Data.SqlClient;

namespace BTQCDar.Services
{
    public class DbService : IDbService
    {
        private readonly string _qcdarConn;
        private readonly string _hrConn;
        private readonly IConfiguration _config;
        public DbService(IConfiguration config)
        {
            _config = config;
            var authenUrl = _config["TBCorApiServices:AuthenUrl"] ?? "/";

            // Read connection strings directly from appsettings.json
            // "ConnectionStrings:BT_QCDAR" and "ConnectionStrings:BT_HR"
            _qcdarConn = config.GetConnectionString("BT_QCDAR")
                         ?? throw new InvalidOperationException(
                             "Connection string 'BT_QCDAR' not found in appsettings.json");

            _hrConn = config.GetConnectionString("BT_HR")
                         ?? throw new InvalidOperationException(
                             "Connection string 'BT_HR' not found in appsettings.json");
        }

        public SqlConnection GetQCDarConnection() => new SqlConnection(_qcdarConn);
        public SqlConnection GetHRConnection() => new SqlConnection(_hrConn);
    }
}
