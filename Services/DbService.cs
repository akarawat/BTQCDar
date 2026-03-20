using Microsoft.Data.SqlClient;

namespace BTQCDar.Services
{
    public class DbService : IDbService
    {
        private readonly IConfiguration _config;

        public DbService(IConfiguration config)
        {
            _config = config;
        }

        public SqlConnection GetQCDarConnection()
            => new SqlConnection(_config.GetConnectionString("BT_QCDAR"));

        public SqlConnection GetHRConnection()
            => new SqlConnection(_config.GetConnectionString("BT_HR"));
    }
}
