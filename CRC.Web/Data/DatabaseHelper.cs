using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Data;
using System.Security.Claims;

namespace CRC.Web.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Cache: "dbo.spName" -> true/false whether @User_ID exists
        private static readonly ConcurrentDictionary<string, bool> _userIdParamSupportCache =
            new(StringComparer.OrdinalIgnoreCase);

        public DatabaseHelper(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _connectionString = configuration.GetConnectionString("CRC_DB")
                ?? throw new InvalidOperationException("Connection string 'CRC_DB' not found.");

            _httpContextAccessor = httpContextAccessor;
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
            await TryInjectUserIdAsync(conn, storedProcedure, cmd);

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
            await TryInjectUserIdAsync(conn, storedProcedure, cmd);

            using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            return dt;
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        private async Task TryInjectUserIdAsync(SqlConnection conn, string storedProcedure, SqlCommand cmd)
        {
            // If caller already supplies @User_ID, do not duplicate.
            if (HasParameter(cmd, "@User_ID"))
                return;

            var (schemaName, procName, cacheKey) = NormalizeStoredProcedure(storedProcedure);
            if (string.IsNullOrWhiteSpace(procName))
                return;

            var supportsUserId = await SupportsUserIdParameterAsync(conn, schemaName, procName, cacheKey);
            if (!supportsUserId)
                return;

            // Read from claims (logged-in user), fallback to 0 when unavailable.
            var userId = GetCurrentUserId() ?? 0;

            cmd.Parameters.Add(new SqlParameter("@User_ID", SqlDbType.Int)
            {
                Value = userId
            });
        }

        private static bool HasParameter(SqlCommand cmd, string parameterName)
        {
            foreach (SqlParameter p in cmd.Parameters)
            {
                if (string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private int? GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            var raw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(raw, out var uid))
                return uid;

            return null;
        }

        private async Task<bool> SupportsUserIdParameterAsync(
            SqlConnection conn,
            string schemaName,
            string procName,
            string cacheKey)
        {
            if (_userIdParamSupportCache.TryGetValue(cacheKey, out var supported))
                return supported;

            // We query sys.parameters (requirement) to see if @User_ID exists.
            using var metaCmd = conn.CreateCommand();
            metaCmd.CommandType = CommandType.Text;
            metaCmd.CommandText = @"
SELECT TOP (1) 1
FROM sys.parameters p
INNER JOIN sys.objects o ON p.object_id = o.object_id
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE o.type = 'P'
  AND s.name = @SchemaName
  AND o.name = @ProcName
  AND p.name = '@User_ID';";

            metaCmd.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.NVarChar, 128) { Value = schemaName });
            metaCmd.Parameters.Add(new SqlParameter("@ProcName", SqlDbType.NVarChar, 128) { Value = procName });

            var result = await metaCmd.ExecuteScalarAsync();
            supported = result != null;

            _userIdParamSupportCache[cacheKey] = supported;
            return supported;
        }

        private static (string SchemaName, string ProcName, string CacheKey) NormalizeStoredProcedure(string storedProcedure)
        {
            // Accept inputs like:
            //   spBranch_Insert
            //   dbo.spBranch_Insert
            //   [dbo].[spBranch_Insert]
            //   [spBranch_Insert]
            var raw = (storedProcedure ?? string.Empty).Trim();
            if (raw.Length == 0)
                return ("dbo", string.Empty, "dbo.");

            // Remove [ and ]
            raw = raw.Replace("[", "").Replace("]", "");

            // Strip any whitespace
            raw = raw.Trim();

            string schema = "dbo";
            string name = raw;

            // Split schema.name if provided
            var parts = raw.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                schema = parts[0];
                name = parts[1];
            }

            var key = $"{schema}.{name}";
            return (schema, name, key);
        }
    }
}