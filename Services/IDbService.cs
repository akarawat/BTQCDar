using Microsoft.Data.SqlClient;

namespace BTQCDar.Services
{
    /// <summary>
    /// Provides SqlConnection for BT_QCDAR and BT_HR databases.
    /// </summary>
    public interface IDbService
    {
        SqlConnection GetQCDarConnection();
        SqlConnection GetHRConnection();
    }
}
