using Microsoft.Data.SqlClient;
using System.Data;

namespace CRC.Web.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("CRC_DB")
                ?? throw new InvalidOperationException("Connection string 'CRC_DB' not found.");
        }

        public async Task<int> ExecuteNonQueryAsync(string storedProcedure, SqlParameter[]? parameters = null)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(storedProcedure, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            if (parameters is { Length: > 0 })
            {
                cmd.Parameters.AddRange(parameters);
            }

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<DataTable> ExecuteDataTableAsync(string storedProcedure, SqlParameter[]? parameters = null)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(storedProcedure, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            if (parameters is { Length: > 0 })
            {
                cmd.Parameters.AddRange(parameters);
            }

            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            return dt;
        }
    }
}