using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Data;
using System.Security.Claims;

namespace CRC.Data.Data
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

        public async Task<DataSet> ExecuteDataSetAsync(string storedProcedure, SqlParameter[]? parameters = null)
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

            var ds = new DataSet();
            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(ds);
            return ds;
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        /// <summary>
        /// The logged-in user's id, from the <see cref="ClaimTypes.NameIdentifier"/> claim; null when there is
        /// no authenticated caller (background work, a request that failed authentication).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is public because <see cref="SqlData"/> needs it, and it needs it because Dapper cannot do what
        /// the ADO surface below does. <see cref="TryInjectUserIdAsync"/> asks sys.parameters whether the
        /// procedure it is about to run declares <c>@User_ID</c> and, if so, silently appends the claim value —
        /// which is how dbo.AuditTrails learns who performed a write without any controller passing an actor.
        /// Dapper sends exactly the properties of the anonymous parameter object and nothing else, so there is
        /// no hook to hang that injection on. <see cref="SqlData"/> therefore passes the value as an ordinary
        /// parameter, per call, to the 19 procedures that record an audit actor.
        /// </para>
        /// <para>
        /// Read DapperLayerPlan.md's "@User_ID" section, and CoreFlow.md §0, before using this. The failure mode
        /// is silent: every one of those 19 procedures declares <c>@User_ID INT = NULL</c>, so omitting it does
        /// not throw — it writes <c>AuditTrails.User_Id = 0</c> and the audit trail stops naming anyone. Note
        /// also that <c>@User_ID</c> means the ACTOR in those 19 and a TARGET USER ROW in five spUsers_*
        /// procedures; this property answers only the first question.
        /// </para>
        /// </remarks>
        public int? CurrentUserId => GetCurrentUserId();


        /// <summary>
        /// Creates a SqlCommand for a stored procedure using an existing open connection/transaction,
        /// and auto-injects @User_ID when the stored procedure supports it (same behavior as ExecuteNonQueryAsync / ExecuteDataTableAsync).
        /// Caller is responsible to dispose the returned command.
        /// </summary>
        public async Task<SqlCommand> CreateStoredProcedureCommandAsync(
            SqlConnection conn,
            SqlTransaction? transaction,
            string storedProcedure,
            SqlParameter[]? parameters = null)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));
            if (string.IsNullOrWhiteSpace(storedProcedure)) throw new ArgumentException("Stored procedure name is required.", nameof(storedProcedure));

            if (transaction != null && !ReferenceEquals(transaction.Connection, conn))
                throw new InvalidOperationException("The provided transaction does not belong to the provided connection.");

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var cmd = new SqlCommand(storedProcedure, conn)
            {
                CommandType = CommandType.StoredProcedure,
                Transaction = transaction
            };

            if (parameters is { Length: > 0 })
            {
                cmd.Parameters.AddRange(parameters);
            }

            await TryInjectUserIdAsync(conn, storedProcedure, cmd);
            return cmd;
        }

        private async Task TryInjectUserIdAsync(SqlConnection conn, string storedProcedure, SqlCommand cmd)
        {
            // If caller already supplies @User_ID, do not duplicate.
            if (HasParameter(cmd, "@User_ID"))
                return;

            var (schemaName, procName, cacheKey) = NormalizeStoredProcedure(storedProcedure);
            if (string.IsNullOrWhiteSpace(procName))
                return;

            // IMPORTANT: if the caller is running inside a local transaction, the metadata query must
            // participate in the same transaction, otherwise ADO.NET throws:
            // "The Transaction property of the command has not been initialized".
            var supportsUserId = await SupportsUserIdParameterAsync(conn, cmd.Transaction, schemaName, procName, cacheKey);
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
            SqlTransaction? transaction,
            string schemaName,
            string procName,
            string cacheKey)
        {
            if (_userIdParamSupportCache.TryGetValue(cacheKey, out var supported))
                return supported;

            // We query sys.parameters (requirement) to see if @User_ID exists.
            using var metaCmd = conn.CreateCommand();
            metaCmd.Transaction = transaction;
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