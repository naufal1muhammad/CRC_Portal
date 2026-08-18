using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;
using CRC.Data.Models;

namespace CRC.Data.Data
{
    // =================================================================================================
    // THE ONLY PLACE IN THE SOLUTION THAT NAMES A STORED PROCEDURE.
    //
    // If you are grepping for "sp" to find out who calls something, this file is the whole answer. That
    // is the property worth protecting: a procedure's signature changes here, once, and the compiler
    // finds every caller — rather than a string literal in a controller finding a user.
    //
    // Every call is `commandType: CommandType.StoredProcedure`, with an anonymous parameter object whose
    // property names match the procedure's parameters without the `@`. THERE IS NO INLINE SQL ANYWHERE IN
    // NUCENTRA AND NONE IS TO BE ADDED — not a SELECT, not a one-line UPDATE, not "just for this". A new
    // query means a new .sql file under CRC.Database/Stored Procedures/{Feature}/, registered in
    // CRC.Database.sqlproj, and a new method here.
    //
    // Pick the Dapper verb by what the procedure guarantees, not by what today's caller happens to want:
    // QuerySingleOrDefaultAsync for a row that may not exist, QuerySingleAsync when exactly one is
    // guaranteed, QueryAsync for a set, ExecuteAsync for a write with no result set, QueryMultipleAsync
    // for several result sets (read the grids in the order the procedure emits them).
    //
    // 🔴 @User_ID. 19 procedures declare `@User_ID INT = NULL` — the ACTOR for their dbo.AuditTrails row.
    // DatabaseHelper used to append that parameter automatically off sys.parameters; Dapper cannot, so it
    // is passed here explicitly as `User_ID = _databaseHelper.CurrentUserId`, with a comment on every one
    // of the 19 saying it is the actor. Five spUsers_* procedures declare `@User_ID INT` with no default,
    // meaning a TARGET user row, which arrives as a method argument instead. The full lists, and why the
    // difference matters, are in IDatabaseData.cs and CoreFlow.md §0. Omitting an actor parameter throws
    // nothing and writes AuditTrails.User_Id = 0.
    //
    // Ordering: the banners here, and the methods inside them, MATCH IDatabaseData.cs exactly, so the two
    // files can be read side by side.
    // =================================================================================================
    public class SqlData : IDatabaseData
    {
        private readonly DatabaseHelper _databaseHelper;

        public SqlData(DatabaseHelper databaseHelper)
        {
            _databaseHelper = databaseHelper;
        }

        // Methods are added here by Prompts 1-9. Each opens its own connection with
        // `using var connection = _databaseHelper.CreateConnection();` — Dapper opens and closes it
        // around the call, so nothing here manages connection lifetime by hand except the two
        // transactional units of work, which own a connection and a SqlTransaction deliberately.

        // ----- Lookups (LU_* reference data) -----

        // 🔴 THE ONE PLACE IN THIS FILE THAT READS A COLUMN BY ORDINAL, and it is deliberate.
        //
        // Eleven of the fourteen lookup procedures return exactly two columns — a VARCHAR(100) code and
        // its VARCHAR(100) display name — and every one of them names those columns after its own table:
        //
        //     spLU_DischargeType_List        DischargeType_ID       DischargeType_Name
        //     spLU_MaritalStatus_List        MaritalStatus_ID       MaritalStatus_Name
        //     spLU_Occupation_List           Occupation_ID          Occupation_Name
        //     spLU_ORGANIZATION_List         Organization_ID        Organization_Name
        //     spLU_PatientDocumentType_List  PatientDocumentType_ID PatientDocumentType_Name
        //     spLU_PJ_AppType_List           PjAppType_ID           PjAppType_Name
        //     spLU_Race_List                 Race_ID                Race_Name
        //     spLU_Religion_List             Religion_ID            Religion_Name
        //     spLU_Source_List               Source_ID              Source_Name
        //     spLU_STAFFDOCUMENTTYPE_List    StaffDocumentType_ID   StaffDocumentType_Name
        //     spLU_STAFFTYPE_List            StaffType_ID           StaffType_Name
        //
        // Dapper maps a result column onto a property BY NAME, so QueryAsync<LookupItem> would match
        // nothing at all here: it would return the right number of rows with Id and Name empty on every
        // one, throwing no exception and logging nothing. The two obvious fixes are both worse — aliasing
        // eleven .sql files to fit a C# type inverts which one is the contract, and eleven two-property
        // models differing only in a prefix is not a data layer, it is a rubber stamp.
        //
        // What all eleven DO share is the shape: column 0 is the code, column 1 is the display name. That
        // is what this helper depends on, once, in the open, instead of eleven times by accident. If a
        // twelfth lookup ever returns a third column, it still works — anything past column 1 is ignored.
        // If one ever returns the name FIRST, this is the one place that has to change.
        private async Task<List<LookupItem>> QueryLookupAsync(string procedureName)
        {
            using var connection = _databaseHelper.CreateConnection();
            using var reader = await connection.ExecuteReaderAsync(
                procedureName,
                commandType: CommandType.StoredProcedure);

            var items = new List<LookupItem>();

            while (reader.Read())
            {
                items.Add(new LookupItem
                {
                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString() ?? string.Empty,
                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetValue(1).ToString() ?? string.Empty
                });
            }

            return items;
        }

        public Task<List<LookupItem>> GetDischargeTypesAsync() =>
            QueryLookupAsync("dbo.spLU_DischargeType_List");

        public Task<List<LookupItem>> GetMaritalStatusesAsync() =>
            QueryLookupAsync("dbo.spLU_MaritalStatus_List");

        public Task<List<LookupItem>> GetOccupationsAsync() =>
            QueryLookupAsync("dbo.spLU_Occupation_List");

        public Task<List<LookupItem>> GetOrganizationsAsync() =>
            QueryLookupAsync("dbo.spLU_ORGANIZATION_List");

        public Task<List<LookupItem>> GetPatientDocumentTypesAsync() =>
            QueryLookupAsync("dbo.spLU_PatientDocumentType_List");

        public Task<List<LookupItem>> GetJourneyAppointmentTypesAsync() =>
            QueryLookupAsync("dbo.spLU_PJ_AppType_List");

        public Task<List<LookupItem>> GetRacesAsync() =>
            QueryLookupAsync("dbo.spLU_Race_List");

        public Task<List<LookupItem>> GetReligionsAsync() =>
            QueryLookupAsync("dbo.spLU_Religion_List");

        public Task<List<LookupItem>> GetSourcesAsync() =>
            QueryLookupAsync("dbo.spLU_Source_List");

        public Task<List<LookupItem>> GetStaffDocumentTypesAsync() =>
            QueryLookupAsync("dbo.spLU_STAFFDOCUMENTTYPE_List");

        public Task<List<LookupItem>> GetStaffTypesAsync() =>
            QueryLookupAsync("dbo.spLU_STAFFTYPE_List");

        // The three LU_LOCATION reads map by name like everything else in this file: LocationLookupItem's
        // properties are the procedures' column names.
        public async Task<List<LocationLookupItem>> GetStatesAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<LocationLookupItem>(
                "dbo.spLU_LOCATION_ListStates",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<LocationLookupItem>> GetCitiesByStateAsync(int stateId)
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<LocationLookupItem>(
                "dbo.spLU_LOCATION_ListCityByState",
                new { StateId = stateId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<LocationLookupItem>> GetPostcodesByCityAsync(int cityId)
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<LocationLookupItem>(
                "dbo.spLU_LOCATION_ListPostcodesByCity",
                new { CityId = cityId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // ----- Branch (Admin > Branch) -----

        public async Task<List<BranchDetail>> GetAllBranchesAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<BranchDetail>(
                "dbo.spBranch_ListAll",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<BranchDetail?> GetBranchByIdAsync(string branchId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // SELECT TOP 1 … WHERE Branch_ID = @Branch_ID: at most one row, possibly none.
            return await connection.QuerySingleOrDefaultAsync<BranchDetail>(
                "dbo.spBranch_GetById",
                new { Branch_ID = branchId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<List<BranchOption>> GetActiveBranchesAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<BranchOption>(
                "dbo.spBranch_ListActive",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<string> CreateBranchAsync(string branchName, string branchLocation, string branchState,
            bool branchStatus, string organizationId, string organizationName)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spBranch_Insert declares @User_ID INT = NULL for its dbo.AuditTrails row: the ACTOR, not a
            // target. DatabaseHelper used to append it automatically off sys.parameters; Dapper cannot, so
            // it is passed explicitly here. Drop it and the insert still succeeds — with
            // AuditTrails.User_Id = 0. See DapperLayerPlan.md's "@User_ID" section and CoreFlow.md §0.1.
            //
            // QuerySingleAsync, not …OrDefault: on a successful run the procedure always ends with
            // `SELECT @Branch_ID AS NewBranch_ID`. Every path that skips that SELECT RAISERRORs first, so
            // the caller gets a SqlException rather than an empty result.
            return await connection.QuerySingleAsync<string>(
                "dbo.spBranch_Insert",
                new
                {
                    Branch_Name = branchName,
                    Branch_Location = branchLocation,
                    Branch_State = branchState,
                    Branch_Status = branchStatus,
                    Organization_ID = organizationId,
                    Organization_Name = organizationName,
                    User_ID = _databaseHelper.CurrentUserId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task UpdateBranchAsync(string branchId, string branchName, string branchLocation,
            string branchState, bool branchStatus, string organizationId, string organizationName)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spBranch_Update declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row, passed
            // explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters injection.
            await connection.ExecuteAsync(
                "dbo.spBranch_Update",
                new
                {
                    Branch_ID = branchId,
                    Branch_Name = branchName,
                    Branch_Location = branchLocation,
                    Branch_State = branchState,
                    Branch_Status = branchStatus,
                    Organization_ID = organizationId,
                    Organization_Name = organizationName,
                    User_ID = _databaseHelper.CurrentUserId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task DeleteBranchAsync(string branchId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spBranch_Delete declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row, passed
            // explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters injection.
            await connection.ExecuteAsync(
                "dbo.spBranch_Delete",
                new { Branch_ID = branchId, User_ID = _databaseHelper.CurrentUserId },
                commandType: CommandType.StoredProcedure);
        }

        // ----- Users, authentication and lockout -----
        //
        // 🔴 READ THIS BEFORE ADDING A CALL TO THIS BANNER. Five of the nine procedures declare @User_ID,
        // and NOT ONE of them gets _databaseHelper.CurrentUserId. In all five it is `@User_ID INT` with NO
        // DEFAULT, meaning A TARGET USER ROW — the account being read or written — which arrives as an
        // ordinary method argument. The 19 audit-actor procedures are the ones that declare
        // `@User_ID INT = NULL`; that default is the whole tell. Every one of the five below says TARGET on
        // its call, deliberately, because the mistake this prevents is invisible: passing the claim to
        // spUsers_Unlock unlocks the administrator's own account and answers "Account unlocked."
        //
        // None of the nine writes a dbo.AuditTrails row, so there is no audit assertion to make here — the
        // security trail for this area is the Serilog audit channel, written by AccountController.

        public async Task<UserAuthRecord?> GetUserForLoginAsync(string username)
        {
            using var connection = _databaseHelper.CreateConnection();

            // SELECT TOP 1 … WHERE Username = @Username, over a UNIQUE index: at most one row, possibly
            // none. No @User_ID — this procedure reads by username, not by id.
            return await connection.QuerySingleOrDefaultAsync<UserAuthRecord>(
                "dbo.spUsers_ValidateLogin",
                new { Username = username },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<UserAccountRecord?> GetUserByIdAsync(int userId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spUsers_GetById declares @User_ID INT with no default: A TARGET — the user row to read. It
            // is the caller's argument and has nothing to do with who is logged in.
            return await connection.QuerySingleOrDefaultAsync<UserAccountRecord>(
                "dbo.spUsers_GetById",
                new { User_ID = userId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<List<UserListItem>> GetAllUsersAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<UserListItem>(
                "dbo.spUsers_GetAll",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task RegisterUserAsync(string userName, string username, string userEmail,
            string passwordHash, int userType, string? staffId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // No @User_ID of either kind: spUsers_Register records no actor and writes no audit row. Who
            // created an account is captured on the Serilog audit channel by the controller instead.
            //
            // staffId is passed as null rather than "" for non-STAFF users. The procedure only inspects it
            // when @User_Type = 3 (where it NULLIFs the blank anyway), so both work today — but null is
            // what the column stores, and "" would be a Staff_ID that matches no staff row.
            await connection.ExecuteAsync(
                "dbo.spUsers_Register",
                new
                {
                    User_Name = userName,
                    Username = username,
                    User_Email = userEmail,
                    PasswordHash = passwordHash,
                    User_Type = userType,
                    Staff_ID = staffId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<FailedLoginResult?> RegisterFailedLoginAsync(string username, int maxFailedAttempts,
            int lockoutMinutes, int attemptWindowMinutes)
        {
            using var connection = _databaseHelper.CreateConnection();

            // THE ONE PROCEDURE THE DAPPER MIGRATION EDITED. It answers through three OUTPUT parameters,
            // which Dapper can only reach via DynamicParameters — a string-keyed bag with a manual
            // .Get<T>("@Name") per value, i.e. exactly the untyped plumbing this layer exists to remove.
            // Prompt 2 therefore APPENDED a trailing SELECT of the same three values to the .sql and KEPT
            // ALL THREE OUTPUT PARAMETERS, so the change is invisible to any caller still using them. The
            // three OUTPUT parameters are simply not sent from here; each declares a default.
            //
            // @NowUtc is likewise not sent, so the procedure uses GETUTCDATE() — the SQL SERVER's clock,
            // not the web server's. The controller compares the returned LockoutEndUtc against the web
            // server's DateTime.UtcNow, so the two machines' clocks must agree for a lockout window to be
            // the length it claims. On one box, as locally, they are the same clock.
            //
            // 🔴 QuerySingleOrDefault, NOT QuerySingle. Two early RETURNs in the procedure skip the new
            // SELECT entirely and emit NO result set: an unknown @Username, and an attempt against an
            // account whose lockout window is already open. Neither is reachable from
            // AccountController.Login — it calls this only after spUsers_ValidateLogin returned a row and
            // after its own lockout check passed — but QuerySingleAsync would throw "Sequence contains no
            // elements" if either ever were, turning a failed login into a 500. Null means "the procedure
            // decided nothing"; the caller treats that as no lockout.
            return await connection.QuerySingleOrDefaultAsync<FailedLoginResult>(
                "dbo.spUsers_RegisterFailedLogin",
                new
                {
                    Username = username,
                    MaxFailedAttempts = maxFailedAttempts,
                    LockoutMinutes = lockoutMinutes,
                    AttemptWindowMinutes = attemptWindowMinutes
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task ResetFailedLoginsAsync(int userId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spUsers_ResetFailedLogins declares @User_ID INT with no default: A TARGET — the account whose
            // counters are being cleared, which is the user who just logged in successfully.
            await connection.ExecuteAsync(
                "dbo.spUsers_ResetFailedLogins",
                new { User_ID = userId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task UnlockUserAsync(int userId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 A TARGET, AND THE MOST DANGEROUS ONE IN THE MIGRATION. spUsers_Unlock declares @User_ID INT
            // with no default, and it is THE LOCKED-OUT ACCOUNT BEING UNLOCKED — a SUPERUSER acting on
            // somebody else's row. Writing `User_ID = _databaseHelper.CurrentUserId` here would clear the
            // SUPERUSER's own counters, leave the locked-out user locked, throw nothing, and return
            // "Account unlocked." to the administrator. It is the caller's argument. Do not "tidy" it.
            await connection.ExecuteAsync(
                "dbo.spUsers_Unlock",
                new { User_ID = userId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task UpdateLastLoginAsync(int userId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spUsers_UpdateLastLogin declares @User_ID INT with no default: A TARGET — the user whose
            // Last_Login is being stamped. Note that this runs BEFORE HttpContext.SignInAsync, so
            // DatabaseHelper.CurrentUserId is still null at this point in the login flow anyway.
            await connection.ExecuteAsync(
                "dbo.spUsers_UpdateLastLogin",
                new { User_ID = userId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task UpdateUserPasswordAsync(int userId, string passwordHash)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spUsers_UpdatePassword declares @User_ID INT with no default: A TARGET — the account whose
            // hash is being replaced. Today the only caller passes the logged-in user's own id (there is no
            // admin password reset in nucentra), which is exactly why it would be easy to mistake this for
            // an actor parameter. It is not: it is the row in the WHERE clause.
            await connection.ExecuteAsync(
                "dbo.spUsers_UpdatePassword",
                new { User_ID = userId, PasswordHash = passwordHash },
                commandType: CommandType.StoredProcedure);
        }

        // ----- Staff (Admin > Staff) -----

        public async Task<List<StaffListItem>> GetAllStaffAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<StaffListItem>(
                "dbo.spStaff_List",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<StaffDetail?> GetStaffByIdAsync(string staffId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // SELECT TOP 1 … WHERE Staff_ID = @Staff_ID: at most one row, possibly none.
            return await connection.QuerySingleOrDefaultAsync<StaffDetail>(
                "dbo.spStaff_GetById",
                new { Staff_ID = staffId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<string> CreateStaffAsync(StaffSaveInput staff)
        {
            using var connection = _databaseHelper.CreateConnection();

            return await InsertStaffAsync(connection, null, staff, _databaseHelper.CurrentUserId);
        }

        public async Task UpdateStaffAsync(StaffSaveInput staff)
        {
            using var connection = _databaseHelper.CreateConnection();

            await UpdateStaffRowAsync(connection, null, staff, _databaseHelper.CurrentUserId);
        }

        public async Task<StaffDeleteResult> DeleteStaffAsync(string staffId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spStaff_Delete declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row, passed
            // explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters injection.
            //
            // TWO RESULT SETS, ALWAYS, IN THIS ORDER — which is the whole reason this is QueryMultipleAsync
            // and not ExecuteAsync, and why the controller used to reach for ExecuteDataSetAsync:
            //
            //   grid 1  one row   Status VARCHAR(20), Message VARCHAR(500)
            //   grid 2  N rows    BlobName VARCHAR(500) — the container keys of the documents just removed
            //
            // Both early-return branches ("NotFound" and "Blocked") emit
            // `SELECT TOP 0 CAST(NULL AS VARCHAR(500)) AS [BlobName]` before returning, so the GRID COUNT
            // IS STABLE and grid 2 can be read unconditionally. An empty grid 2 means "no storage to
            // reclaim", never "something failed" — only Status says whether the delete happened.
            using var grids = await connection.QueryMultipleAsync(
                "dbo.spStaff_Delete",
                new { Staff_ID = staffId, User_ID = _databaseHelper.CurrentUserId },
                commandType: CommandType.StoredProcedure);

            // ReadSingleAsync into StaffDeleteResult maps Status and Message by name and leaves BlobNames
            // at its initialiser — grid 2 fills it below. Dapper ignores properties it finds no column for.
            var result = await grids.ReadSingleAsync<StaffDeleteResult>();

            var blobNames = await grids.ReadAsync<string?>();

            // The procedure already excludes NULL and blank keys when it captures them, so this filter is
            // belt-and-braces; it is here so the list's element type is honest under nullable reference
            // types rather than carrying nulls a caller would have to re-check.
            result.BlobNames = blobNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList();

            return result;
        }

        // 🔴 THE FIRST OF EXACTLY TWO TRANSACTIONAL UNITS OF WORK IN THIS FILE. Everything else here is one
        // method, one procedure; this one runs spStaff_Insert or spStaff_Update, then
        // spStaffDocument_GetById + spStaffDocument_Delete per removed document, then
        // spStaffDocument_Insert per added document — on ONE connection, inside ONE SqlTransaction. The
        // other exception is SaveAppointmentAsync (Prompt 6). Read IDatabaseData for why this is atomic and
        // why the uploads arrive through a callback; this comment covers only the mechanism.
        //
        // The connection is opened and the transaction begun BY HAND here, which is the one thing in this
        // file that does not let Dapper manage the connection: every call below passes `transaction:` and
        // must, because a command on this connection without the transaction throws.
        public async Task<StaffSaveResult> SaveStaffWithDocumentsAsync(
            StaffSaveInput staff,
            IReadOnlyList<int> deleteDocumentIds,
            Func<string, Task<IReadOnlyList<StaffDocumentInput>>> uploadDocumentsAsync)
        {
            var result = new StaffSaveResult();

            // Resolved ONCE, before the transaction opens, and reused for all four audit-actor calls below.
            // It reads the claim off IHttpContextAccessor, which cannot change mid-request — reading it per
            // call would be four identical answers and four chances to forget one.
            var actorUserId = _databaseHelper.CurrentUserId;

            await using var connection = _databaseHelper.CreateConnection();
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();

            try
            {
                if (staff.IsNew)
                {
                    result.StaffId = await InsertStaffAsync(connection, transaction, staff, actorUserId);

                    // spStaff_Insert always ends with `SELECT @Staff_ID AS NewStaff_ID` on the path that
                    // inserted, so this cannot fire — but a blank id here would name every blob
                    // "staff//{guid}" and orphan the documents, so it fails loudly rather than continuing.
                    if (string.IsNullOrWhiteSpace(result.StaffId))
                    {
                        throw new InvalidOperationException("Failed to generate Staff ID.");
                    }
                }
                else
                {
                    result.StaffId = staff.Staff_ID;
                    await UpdateStaffRowAsync(connection, transaction, staff, actorUserId);
                }

                // Delete the document ROWS, and capture the blob keys on the way past. Nothing is removed
                // from storage here: the transaction can still roll the rows back, and a deleted blob
                // cannot be un-deleted. The keys travel out on the result for the caller to act on AFTER
                // the commit.
                foreach (var documentId in deleteDocumentIds)
                {
                    var existing = await connection.QuerySingleOrDefaultAsync<StaffDocumentItem>(
                        "dbo.spStaffDocument_GetById",
                        new { StaffDocument_ID = documentId },
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure);

                    if (existing != null && !string.IsNullOrWhiteSpace(existing.BlobName))
                    {
                        result.RemovedDocuments.Add(new StaffDocumentDeletion
                        {
                            StaffDocument_ID = documentId,
                            BlobName = existing.BlobName
                        });
                    }

                    // spStaffDocument_Delete declares @User_ID INT = NULL: the ACTOR for its
                    // dbo.AuditTrails row. Its @DeletedBlobName OUTPUT parameter is not requested here —
                    // it also declares a default — because the read above already has the key AND the row
                    // is only audited on the Serilog channel by id, which the caller already holds.
                    await connection.ExecuteAsync(
                        "dbo.spStaffDocument_Delete",
                        new { StaffDocument_ID = documentId, User_ID = actorUserId },
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure);
                }

                // THE CALLBACK. It uploads the files to the private container — which only it can do, since
                // IDocumentStorage lives in CRC.Web — and hands back the rows to insert. It runs HERE,
                // after the staff row exists, because the blob key is staff/{Staff_ID}/{guid}{ext} and on
                // an insert that id was generated three statements ago. Anything it throws propagates into
                // the catch below and rolls the whole transaction back; the caller compensates the blobs.
                var documents = await uploadDocumentsAsync(result.StaffId);

                foreach (var document in documents)
                {
                    await InsertStaffDocumentAsync(connection, transaction, result.StaffId, document, actorUserId);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return result;
        }

        // spStaff_Insert has TWO call sites — CreateStaffAsync and the transaction above — so its fifteen
        // parameters are written out once, here, and cannot drift between them. `transaction` is null on
        // the non-transactional path, which Dapper accepts.
        //
        // spStaff_Insert declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row, passed
        // explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters injection. Drop it
        // and the insert still succeeds — with AuditTrails.User_Id = 0.
        //
        // QuerySingleAsync, not …OrDefault: every path that skips the trailing
        // `SELECT @Staff_ID AS NewStaff_ID` RAISERRORs first (a blank @Staff_Type), so the caller gets a
        // SqlException rather than an empty result.
        private static Task<string> InsertStaffAsync(
            SqlConnection connection, SqlTransaction? transaction, StaffSaveInput staff, int? actorUserId)
        {
            return connection.QuerySingleAsync<string>(
                "dbo.spStaff_Insert",
                new
                {
                    staff.Staff_Name,
                    staff.Staff_NRIC,
                    staff.Staff_BirthDate,
                    staff.Staff_Age,
                    staff.Staff_Phone,
                    staff.Staff_Email,
                    staff.Staff_Gender,
                    staff.Staff_ResState,
                    staff.Staff_ResCity,
                    staff.Staff_ResPostcode,
                    staff.Staff_AddLine1,
                    staff.Staff_AddLine2,
                    staff.Staff_Base,
                    staff.Staff_Type,
                    User_ID = actorUserId
                },
                transaction: transaction,
                commandType: CommandType.StoredProcedure);
        }

        // Same arrangement for spStaff_Update, whose parameter list is spStaff_Insert's plus @Staff_ID.
        // It too declares @User_ID INT = NULL — the ACTOR — and it audits only when a row actually
        // changed, so a bad id writes nothing and reports nothing.
        private static Task UpdateStaffRowAsync(
            SqlConnection connection, SqlTransaction? transaction, StaffSaveInput staff, int? actorUserId)
        {
            return connection.ExecuteAsync(
                "dbo.spStaff_Update",
                new
                {
                    staff.Staff_ID,
                    staff.Staff_Name,
                    staff.Staff_NRIC,
                    staff.Staff_BirthDate,
                    staff.Staff_Age,
                    staff.Staff_Phone,
                    staff.Staff_Email,
                    staff.Staff_Gender,
                    staff.Staff_ResState,
                    staff.Staff_ResCity,
                    staff.Staff_ResPostcode,
                    staff.Staff_AddLine1,
                    staff.Staff_AddLine2,
                    staff.Staff_Base,
                    staff.Staff_Type,
                    User_ID = actorUserId
                },
                transaction: transaction,
                commandType: CommandType.StoredProcedure);
        }

        // ----- Staff documents -----

        public async Task<List<StaffDocumentItem>> GetStaffDocumentsAsync(string staffId)
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<StaffDocumentItem>(
                "dbo.spStaffDocument_List",
                new { Staff_ID = staffId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<StaffDocumentItem?> GetStaffDocumentByIdAsync(int documentId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // SELECT TOP 1 … WHERE StaffDocument_ID = @StaffDocument_ID over the primary key: at most one
            // row, possibly none.
            return await connection.QuerySingleOrDefaultAsync<StaffDocumentItem>(
                "dbo.spStaffDocument_GetById",
                new { StaffDocument_ID = documentId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task AddStaffDocumentAsync(string staffId, string staffName, string documentTypeId,
            string documentTypeName, string fileName, string blobName, string contentType)
        {
            using var connection = _databaseHelper.CreateConnection();

            await InsertStaffDocumentAsync(
                connection,
                null,
                staffId,
                new StaffDocumentInput
                {
                    Staff_Name = staffName,
                    StaffDocumentType_ID = documentTypeId,
                    StaffDocumentType_Name = documentTypeName,
                    FileName = fileName,
                    BlobName = blobName,
                    ContentType = contentType
                },
                _databaseHelper.CurrentUserId);
        }

        // spStaffDocument_Insert's other two-call-site helper, for the same reason as InsertStaffAsync:
        // AddStaffDocumentAsync runs it on its own, SaveStaffWithDocumentsAsync runs it inside the
        // transaction, and the parameter list is written once.
        //
        // spStaffDocument_Insert declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row,
        // passed explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters injection.
        private static Task InsertStaffDocumentAsync(
            SqlConnection connection, SqlTransaction? transaction, string staffId,
            StaffDocumentInput document, int? actorUserId)
        {
            return connection.ExecuteAsync(
                "dbo.spStaffDocument_Insert",
                new
                {
                    Staff_ID = staffId,
                    document.Staff_Name,
                    document.StaffDocumentType_ID,
                    document.StaffDocumentType_Name,
                    document.FileName,
                    document.BlobName,
                    document.ContentType,
                    User_ID = actorUserId
                },
                transaction: transaction,
                commandType: CommandType.StoredProcedure);
        }

        public async Task<string?> DeleteStaffDocumentAsync(int documentId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 THE ONE PLACE IN THIS FILE THAT USES DynamicParameters, and the only one that should.
            // spStaffDocument_Delete answers through an OUTPUT parameter — @DeletedBlobName VARCHAR(500) —
            // and an OUTPUT parameter is the single thing Dapper cannot reach through an anonymous object.
            // §5.3 records spUsers_RegisterFailedLogin as "the only procedure in nucentra with OUTPUT
            // parameters"; THAT IS WRONG, and this is the second. The difference is what could be done
            // about it: Prompt 2 was allowed to append a trailing SELECT to that procedure, Prompt 3 was
            // not allowed to touch any .sql, and this one has no result set to append to. So the untyped
            // bag stays, confined to four lines, with the name and the type written out where a reader can
            // check them against the procedure.
            //
            // AnsiString, not String: the parameter is VARCHAR(500), and DbType.String would send NVARCHAR
            // for SQL Server to convert back.
            //
            // @User_ID INT = NULL is the ACTOR for the dbo.AuditTrails row, passed explicitly as everywhere
            // else. The procedure audits only when a row was actually deleted.
            var parameters = new DynamicParameters();
            parameters.Add("StaffDocument_ID", documentId);
            parameters.Add("User_ID", _databaseHelper.CurrentUserId);
            parameters.Add("DeletedBlobName", dbType: DbType.AnsiString, size: 500,
                direction: ParameterDirection.Output);

            await connection.ExecuteAsync(
                "dbo.spStaffDocument_Delete",
                parameters,
                commandType: CommandType.StoredProcedure);

            // NULL means no row was deleted, so there is nothing in storage to remove.
            var deletedBlobName = parameters.Get<string?>("DeletedBlobName");

            return string.IsNullOrWhiteSpace(deletedBlobName) ? null : deletedBlobName;
        }

        public async Task<List<StaffDocumentSetting>> GetStaffDocumentSettingsAsync(string staffTypeId)
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<StaffDocumentSetting>(
                "dbo.spStaffDocumentSettings_GetByStaffType",
                new { StaffType_ID = staffTypeId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // Reuses QueryLookupAsync — the ordinal-reading helper written for the eleven LU_* code procedures
        // — because this procedure has exactly the shape that helper assumes: two columns, the code first
        // (StaffDocumentType_ID) and the display name second (StaffDocumentType_Name). Mapping
        // LookupItem by NAME would match neither column and return empty strings on every row.
        public Task<List<LookupItem>> GetStaffDocumentTypeFiltersAsync() =>
            QueryLookupAsync("dbo.spStaffDocument_LookupDocuments");

        public async Task<List<string>> GetStaffDocumentStaffNamesAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // One VARCHAR column, so the row type is the column type. The procedure's WHERE already
            // excludes blank names, and its INNER JOIN excludes documents whose Staff_ID matches no staff.
            var results = await connection.QueryAsync<string>(
                "dbo.spStaffDocument_StaffNames",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // ----- Staff slots (Staff Schedule) -----
        //
        // 🔴 FOUR OF THE SIX PROCEDURES IN Stored Procedures/StaffSlots/ ARE HERE.
        // spStaffSlots_AssignAppointment and spStaffSlots_ClearAppointment ARE DELIBERATELY ABSENT — they
        // are only ever run inside PatientController.SaveAppointment's transaction, and PROMPT 6 adds them
        // to SaveAppointmentAsync rather than here. See the matching banner in IDatabaseData.cs for why
        // publishing them as standalone methods would be a mistake rather than a convenience.

        public async Task<List<StaffSlotItem>> GetStaffSlotsAsync(string staffId, DateTime? fromDate,
            DateTime? toDate)
        {
            using var connection = _databaseHelper.CreateConnection();

            // No @User_ID of either kind: this is a plain read and writes no audit row.
            //
            // @FromDate and @ToDate are DATE and both default to NULL in the procedure, where NULL means
            // "unbounded at that end" (`@FromDate IS NULL OR SlotDate >= @FromDate`). Dapper sends a
            // DateTime as DbType.DateTime and SQL Server narrows it to DATE on the way in; the caller
            // already passes midnight, so nothing is lost. The old ADO code said SqlDbType.Date explicitly
            // — same value, one fewer conversion.
            var results = await connection.QueryAsync<StaffSlotItem>(
                "dbo.spStaffSlots_List",
                new { Staff_ID = staffId, FromDate = fromDate, ToDate = toDate },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<StaffSlotCreateResult> CreateStaffSlotRangeAsync(string staffId, DateTime fromDate,
            DateTime toDate, TimeSpan startTime, TimeSpan endTime)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spStaffSlots_CreateRange declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row,
            // not a target. DatabaseHelper used to append it automatically off sys.parameters; Dapper
            // cannot, so it is passed explicitly here. Drop it and the whole range is still created — with
            // AuditTrails.User_Id = 0, and one audit row per range means one lost attribution per range.
            //
            // QuerySingleAsync, not …OrDefault: the procedure always ends with
            // `SELECT @CreatedCount …, @SkippedExistingCount …`, and every path that skips it THROWs first,
            // so the caller gets a SqlException rather than an empty result.
            //
            // The two TimeSpans map onto @StartTime / @EndTime TIME(0) — Dapper's default for TimeSpan is
            // DbType.Time, which is what the procedure declares.
            return await connection.QuerySingleAsync<StaffSlotCreateResult>(
                "dbo.spStaffSlots_CreateRange",
                new
                {
                    Staff_ID = staffId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    StartTime = startTime,
                    EndTime = endTime,
                    User_ID = _databaseHelper.CurrentUserId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task DeleteStaffSlotAsync(int staffSlotId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spStaffSlots_Delete declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row,
            // passed explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters
            // injection.
            //
            // ExecuteAsync and no return value, because the procedure has no result set and no row count to
            // report: it answers "refused" and "not found" by THROWing (50002 and 50003), which arrive at
            // the controller as a SqlException. An audit row is written only on the path that actually
            // deleted, since both THROWs abort before reaching it.
            await connection.ExecuteAsync(
                "dbo.spStaffSlots_Delete",
                new { StaffSlot_ID = staffSlotId, User_ID = _databaseHelper.CurrentUserId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<string?> GetStaffSlotOwnerAsync(int staffSlotId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // SELECT TOP 1 [Staff_ID] … WHERE StaffSlot_ID = @StaffSlot_ID over the primary key: at most one
            // row, possibly none, and one VARCHAR column — so the row type is the column type and a missing
            // slot is a null. No @User_ID; this is a read.
            //
            // The null is load-bearing: it is how StaffScheduleController.Delete tells "no such slot" from
            // "not yours", which are two different answers to the caller.
            return await connection.QuerySingleOrDefaultAsync<string?>(
                "dbo.spStaffSlots_GetOwner",
                new { StaffSlot_ID = staffSlotId },
                commandType: CommandType.StoredProcedure);
        }

        // ----- Staff performance -----

        public async Task<StaffPerformanceResult> GetStaffPerformanceAsync(string staffId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 FOUR RESULT SETS, ALWAYS, IN THIS ORDER — which is why this is QueryMultipleAsync and why
            // the controller used to reach for ExecuteDataSetAsync:
            //
            //   grid 1  one row   TotalColonoscopy, TotalColonoscopyThisMonth        (dbo.PatientJourney)
            //   grid 2  N rows    PjAppType_ID, PjAppType_Name, TotalHours           (dbo.PatientAppointment)
            //   grid 3  N rows    Complication, Total                                (dbo.PatientColonoscopy)
            //   grid 4  N rows    TypeOfAnomaly, PatientCount                        (dbo.PatientColonoscopy)
            //
            // NOTHING IN THE DATA SAYS WHICH GRID IS WHICH. Grids 3 and 4 have identical shapes — one string
            // and one int — so reading them in the wrong order compiles, runs, returns the right number of
            // rows, and labels complications as anomalies. The reads below are in the procedure's order and
            // the before/after JSON diff in Prompt 4 is what proves it.
            //
            // No @User_ID: a read, no audit row.
            using var grids = await connection.QueryMultipleAsync(
                "dbo.spStaff_GetPerformance",
                new { Staff_ID = staffId },
                commandType: CommandType.StoredProcedure);

            // Grid 1 is an aggregate with NO GROUP BY, so it returns exactly one row even when the staff
            // member has no journeys at all — in which case both SUMs are NULL, which is why the two
            // properties are int? and why ReadSingleAsync is correct rather than optimistic. It maps the two
            // counts by name and leaves the three lists at their initialisers, exactly as
            // StaffDeleteResult.BlobNames is left for the second grid.
            var result = await grids.ReadSingleAsync<StaffPerformanceResult>();

            result.HoursByType = (await grids.ReadAsync<StaffPerformanceHours>()).ToList();
            result.Complications = (await grids.ReadAsync<StaffPerformanceComplication>()).ToList();
            result.Anomalies = (await grids.ReadAsync<StaffPerformanceAnomaly>()).ToList();

            return result;
        }

        // ----- Patient (Admin > Patient) -----
        //
        // Seven procedures; three declare `@User_ID INT = NULL` — the ACTOR — and each of those three says
        // so on its own call below. The four reads take none and must not be given one.

        public async Task<List<PatientListItem>> GetActivePatientsAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // No @User_ID of either kind: a plain read, no audit row. The procedure takes no parameters at
            // all — "active" is `DischargeType_ID IS NULL`, hard-coded in its WHERE clause.
            var results = await connection.QueryAsync<PatientListItem>(
                "dbo.spPatientBasic_ListActive",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<PatientDischargedItem>> GetDischargedPatientsAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<PatientDischargedItem>(
                "dbo.spPatientBasic_ListDischarged",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<PatientBasicDetail?> GetPatientByIdAsync(string patientId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // WHERE pb.Patient_ID = @Patient_ID over the primary key: at most one row, possibly none. Note
            // there is no SELECT TOP 1 here, unlike spBranch_GetById and spStaff_GetById — the key alone
            // guarantees it, and QuerySingleOrDefaultAsync would throw if that ever stopped being true,
            // which is the behaviour worth having.
            return await connection.QuerySingleOrDefaultAsync<PatientBasicDetail>(
                "dbo.spPatientBasic_GetById",
                new { Patient_ID = patientId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<List<PatientDocumentRequirement>> GetMissingDischargeDocumentsAsync(
            string patientId, string dischargeTypeId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // Returns the mandatory document types this patient does NOT have for this discharge reason.
            // AN EMPTY SET IS THE PASS CONDITION — every row is a blocker. No @User_ID; a read.
            var results = await connection.QueryAsync<PatientDocumentRequirement>(
                "dbo.spPatient_Discharge_CheckMissingDocuments",
                new { Patient_ID = patientId, DischargeType_ID = dischargeTypeId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<string> CreatePatientAsync(PatientSaveInput patient)
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 THE THIRD PROCEDURE IN NUCENTRA WITH AN OUTPUT PARAMETER, and the second place in this
            // file that has to use DynamicParameters because of one. spPatientBasic_Insert hands the id it
            // generated back through `@NewPatient_ID VARCHAR(100) OUTPUT` — there is no trailing
            // `SELECT … AS NewPatient_ID` the way spBranch_Insert and spStaff_Insert have one, so
            // QuerySingleAsync<string> would return nothing and throw "Sequence contains no elements".
            //
            // Prompt 2 solved the same problem on spUsers_RegisterFailedLogin by APPENDING a trailing
            // SELECT to the .sql. That was not an option here: Prompt 5 is permitted to touch no .sql at
            // all, and the additive change would have to be made and published before the C# could rely on
            // it. So the untyped bag stays, confined to four lines, with the parameter's name and type
            // written out where a reader can check them against the procedure.
            //
            // AnsiString, not String: @NewPatient_ID is VARCHAR(100), and DbType.String would send NVARCHAR
            // for SQL Server to convert back.
            //
            // AddDynamicParams takes the same anonymous object the other writes in this file pass directly,
            // so the parameter list still reads as one block and still mirrors PatientSaveInput.
            //
            // 🔴 spPatientBasic_Insert declares @User_ID INT = NULL for its dbo.AuditTrails row: the ACTOR,
            // not a target. DatabaseHelper used to append it automatically off sys.parameters; Dapper
            // cannot, so it is passed explicitly here. Drop it and the patient is still created — with
            // AuditTrails.User_Id = 0, and nothing anywhere fails. See CoreFlow.md §0.1.
            //
            // NOTE WHAT IS ABSENT: the three discharge parameters. spPatientBasic_Insert does not declare
            // them — it hard-codes NULL into all three columns, because a new patient is by definition
            // active. Sending them would fail with "procedure has no parameter named …".
            var parameters = new DynamicParameters();

            parameters.AddDynamicParams(new
            {
                patient.Patient_Name,
                patient.Patient_Email,
                patient.Patient_Phone,
                patient.Patient_NRIC,
                patient.Patient_BirthDate,
                patient.Patient_Age,
                patient.Race_ID,
                patient.Source_ID,
                patient.Patient_Gender,
                patient.Religion_ID,
                patient.MaritalStatus_ID,
                patient.Occupation_ID,
                patient.Patient_ResState,
                patient.Patient_ResCity,
                patient.Patient_ResPostcode,
                patient.Patient_AddLine1,
                patient.Patient_AddLine2,
                patient.Patient_EmergencyName,
                patient.Patient_EmergencyRelationship,
                patient.Patient_EmergencyNumber,
                patient.Patient_iFOBTStatus,
                patient.Patient_iFOBTCompletionDate,
                patient.Patient_iFOBTResults,
                User_ID = _databaseHelper.CurrentUserId
            });

            parameters.Add("NewPatient_ID", dbType: DbType.AnsiString, size: 100,
                direction: ParameterDirection.Output);

            await connection.ExecuteAsync(
                "dbo.spPatientBasic_Insert",
                parameters,
                commandType: CommandType.StoredProcedure);

            // The procedure SETs this before it INSERTs and has no path that skips doing so, so an empty
            // value here would mean the row went in under a blank primary key rather than that the id is
            // merely unknown. The caller returns it to the browser, which uses it as the patient's identity
            // from that moment on — so it fails loudly instead of handing back "".
            var newPatientId = parameters.Get<string?>("NewPatient_ID");

            if (string.IsNullOrWhiteSpace(newPatientId))
            {
                throw new InvalidOperationException(
                    "spPatientBasic_Insert did not return a new Patient_ID.");
            }

            return newPatientId;
        }

        public async Task UpdatePatientAsync(PatientSaveInput patient)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spPatientBasic_Update declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row,
            // passed explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters
            // injection.
            //
            // ExecuteAsync and no return value: the procedure emits no result set and reports no row count,
            // so an update against an unknown id succeeds silently and writes no audit row.
            //
            // 🔴 THE THREE DISCHARGE PARAMETERS ARE SENT UNCONDITIONALLY, INCLUDING AS NULLS. The procedure
            // assigns all three columns on every call, so omitting them (they all default to NULL) would
            // silently un-discharge every patient this method touched. They are properties on
            // PatientSaveInput for exactly that reason — the caller decides, per save, and always answers.
            await connection.ExecuteAsync(
                "dbo.spPatientBasic_Update",
                new
                {
                    patient.Patient_ID,
                    patient.Patient_Name,
                    patient.Patient_Email,
                    patient.Patient_Phone,
                    patient.Patient_NRIC,
                    patient.Patient_BirthDate,
                    patient.Patient_Age,
                    patient.Race_ID,
                    patient.Source_ID,
                    patient.Patient_Gender,
                    patient.Religion_ID,
                    patient.MaritalStatus_ID,
                    patient.Occupation_ID,
                    patient.Patient_ResState,
                    patient.Patient_ResCity,
                    patient.Patient_ResPostcode,
                    patient.Patient_AddLine1,
                    patient.Patient_AddLine2,
                    patient.Patient_EmergencyName,
                    patient.Patient_EmergencyRelationship,
                    patient.Patient_EmergencyNumber,
                    patient.Patient_iFOBTStatus,
                    patient.Patient_iFOBTCompletionDate,
                    patient.Patient_iFOBTResults,
                    patient.DischargeType_ID,
                    patient.Patient_DischargeDate,
                    patient.Patient_DischargeRemarks,
                    User_ID = _databaseHelper.CurrentUserId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<PatientDeleteResult> DeletePatientCascadeAsync(string patientId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spPatient_DeleteCascade declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails row,
            // passed explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters
            // injection. The audit row is the ONLY record that this delete happened — there is no status,
            // no row count and no soft-delete flag — so losing the actor here loses the whole "who".
            //
            // 🔴 ONE RESULT SET, NOT TWO — WHICH IS NOT WHAT DapperLayerPlan.md's Prompt 5 EXPECTS. The plan
            // describes a summary grid followed by the blob keys, by analogy with spStaff_Delete (§5.4).
            // The .sql has a single statement-level SELECT, its last line, `SELECT [BlobName] FROM
            // @DocBlobs;`, and the DataTable code this replaces read ds.Tables[0] and indexed it by
            // "BlobName" — grid 0 IS the keys. So this is QueryAsync, not QueryMultipleAsync; using the
            // latter and calling ReadAsync twice would throw on the second read. See PatientDeleteResult.
            //
            // The keys are captured by the procedure into a table variable BEFORE it deletes the
            // dbo.PatientDocument rows, which is the only reason they can be returned at all. Removing the
            // objects is the caller's job: IDocumentStorage is a CRC.Web service and CRC.Data has no
            // reference to CRC.Web (CoreFlow.md §6.6).
            var blobNames = await connection.QueryAsync<string?>(
                "dbo.spPatient_DeleteCascade",
                new { Patient_ID = patientId, User_ID = _databaseHelper.CurrentUserId },
                commandType: CommandType.StoredProcedure);

            // The procedure already excludes NULL and blank keys when it captures them, so this filter is
            // belt-and-braces; it is here so the list's element type is honest under nullable reference
            // types rather than carrying nulls a caller would have to re-check. Same as DeleteStaffAsync.
            return new PatientDeleteResult
            {
                BlobNames = blobNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .ToList()
            };
        }

        // ----- Patient appointments -----
        //
        // Twelve procedures. FOUR declare `@User_ID INT = NULL` — the ACTOR — and each of those four says
        // so on its own call below: spPatientAppointment_Insert, _Update, _Delete and _UpdateStatus. The
        // six reads take none and must not be given one; neither slot procedure declares one either.
        // Checked in the .sql files rather than inferred from the pattern.
        //
        // spStaffSlots_AssignAppointment and spStaffSlots_ClearAppointment appear ONLY inside
        // SaveAppointmentAsync. They have no methods of their own, deliberately — see the Staff slots
        // banner above and the comment on the transaction itself.

        public async Task<List<PatientAppointmentItem>> GetAppointmentsByPatientAsync(string patientId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // No @User_ID of either kind: a plain read, no audit row. An unknown Patient_ID is not an
            // error — the WHERE simply matches nothing and the list comes back empty.
            var results = await connection.QueryAsync<PatientAppointmentItem>(
                "dbo.spPatientAppointment_ListByPatient",
                new { Patient_ID = patientId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<AppointmentSearchItem>> SearchAppointmentsAsync(string? patientName,
            string? staffName, string? status, DateTime? fromDate, DateTime? toDate, string? pjAppTypeName,
            string? branchName)
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 NULL MEANS "DO NOT FILTER" AND A BLANK STRING DOES NOT. Every predicate is
            // `@X IS NULL OR column = @X`, so sending "" for an unused filter would match only rows whose
            // column is the empty string — i.e. nothing. Dapper sends a C# null as DBNull, which is what
            // the procedure's IS NULL test wants, so the conversion the callers already do (blank → null)
            // is the whole of it. No @User_ID: a read.
            //
            // The two dates are DATE in the procedure. Dapper sends a DateTime as DbType.DateTime and SQL
            // Server narrows it on the way in; both callers pass a midnight .Date, so nothing is lost —
            // the same note as GetStaffSlotsAsync.
            var results = await connection.QueryAsync<AppointmentSearchItem>(
                "dbo.spPatientAppointment_Search",
                new
                {
                    PatientName = patientName,
                    StaffName = staffName,
                    Status = status,
                    FromDate = fromDate,
                    ToDate = toDate,
                    PjAppTypeName = pjAppTypeName,
                    BranchName = branchName
                },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // The four filter lookups are one column each, so the row type IS the column type. Every one of
        // them already excludes blanks in its own WHERE, and the three that join exclude unresolvable ids
        // by using an INNER JOIN — so nothing here needs filtering a second time. No @User_ID on any of
        // the four; all are reads.
        private async Task<List<string>> QueryAppointmentNamesAsync(string procedureName)
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<string>(
                procedureName,
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public Task<List<string>> GetAppointmentBranchNamesAsync() =>
            QueryAppointmentNamesAsync("dbo.spPatientAppointment_LookupBranches");

        public Task<List<string>> GetAppointmentPatientNamesAsync() =>
            QueryAppointmentNamesAsync("dbo.spPatientAppointment_LookupPatientNames");

        public Task<List<string>> GetAppointmentStaffNamesAsync() =>
            QueryAppointmentNamesAsync("dbo.spPatientAppointment_LookupStaffNames");

        public Task<List<string>> GetAppointmentStatusesAsync() =>
            QueryAppointmentNamesAsync("dbo.spPatientAppointment_LookupStatuses");

        // 🔴 THE SECOND AND LAST TRANSACTIONAL UNIT OF WORK IN THIS FILE. Read IDatabaseData for WHY the
        // slot read is inside the transaction and why the validation returns a reason rather than a
        // message; this comment covers only the mechanism.
        //
        // The connection is opened and the transaction begun BY HAND, exactly as in
        // SaveStaffWithDocumentsAsync: every call below passes `transaction:` and must, because a command
        // on this connection without it throws.
        public async Task<AppointmentSaveResult> SaveAppointmentAsync(AppointmentSaveInput input)
        {
            var appointmentId = input.PatientAppointment_ID;
            var isInsert = appointmentId <= 0;

            // Resolved ONCE, before the transaction opens, and reused for whichever audit-actor call runs.
            // It reads the claim off IHttpContextAccessor, which cannot change mid-request.
            var actorUserId = _databaseHelper.CurrentUserId;

            var result = new AppointmentSaveResult { IsInsert = isInsert };

            await using var connection = _databaseHelper.CreateConnection();
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();

            try
            {
                // ── STEP 1: THE READ, AND IT IS THE CONCURRENCY CHECK ────────────────────────────────
                //
                // spStaffSlots_List, on THIS connection and inside THIS transaction, narrowed to the
                // chosen clinician and the single day being booked. Everything below decides whether the
                // requested hours are free, and that answer is only sound while this transaction holds
                // its locks — which is why the read cannot be hoisted into the caller. Two administrators
                // booking the same hour is exactly what this ordering prevents.
                //
                // Note what the narrowing already does: because @FromDate = @ToDate = the appointment's
                // date and @Staff_ID is the chosen one, a slot belonging to another clinician or another
                // day is simply NOT IN THIS RESULT — so the wrong-staff and wrong-date checks below can
                // never fire, and the count check catches all three cases. That is measured behaviour,
                // not a guess; see AppointmentSaveFailure.SlotNotFound.
                var allSlots = await connection.QueryAsync<StaffSlotItem>(
                    "dbo.spStaffSlots_List",
                    new
                    {
                        Staff_ID = input.Staff_ID,
                        FromDate = input.PatientAppointment_Date.Date,
                        ToDate = input.PatientAppointment_Date.Date
                    },
                    transaction: transaction,
                    commandType: CommandType.StoredProcedure);

                var requestedIds = input.SlotIds;
                var slots = allSlots
                    .Where(slot => requestedIds.Contains(slot.StaffSlot_ID))
                    .ToList();

                // ── STEP 2: THE FOUR CHECKS ──────────────────────────────────────────────────────────
                //
                // Each one rolls back and reports a REASON. The controller owns the wording.

                // (a) Every requested slot exists — i.e. the read gave back one row per requested id.
                if (slots.Count != requestedIds.Count)
                {
                    await transaction.RollbackAsync();
                    result.Reason = AppointmentSaveFailure.SlotNotFound;
                    return result;
                }

                // (b) 🔴 EVERY SLOT BELONGS TO THE SELECTED STAFF MEMBER — AND THIS IS THE ONE CHECK WITH
                //     NO CODE, BECAUSE THE READ ITSELF IS THE CHECK. spStaffSlots_List filters
                //     `WHERE Staff_ID = @Staff_ID` and does not PROJECT Staff_ID, so there is no per-row
                //     value here to compare and no need for one: another clinician's slot cannot be in
                //     `allSlots` at all, and asking for one fails (a) above as a missing id.
                //
                //     The pre-migration code did write this check, and it could not fire either — it
                //     built each SlotInfo with `StaffId = staffId` from the request and then compared
                //     that field back to the same request value. Reproducing that tautology here would
                //     look like a bug and read like one. AppointmentSaveFailure.SlotWrongStaff is kept,
                //     and the controller still maps it to the exact sentence it always did, so the reason
                //     is available the day spStaffSlots_List projects Staff_ID or drops its filter.
                //     Measured on the running site: another staff member's slot id is refused, before and
                //     after this migration, with the SlotNotFound message.

                // (c) Every slot is on the selected date. Also narrowed by the read — @FromDate = @ToDate
                //     = this date — so it cannot fire today either. Unlike (b) it is written out, because
                //     spStaffSlots_List DOES project SlotDate, so there is a real per-row value to assert
                //     and the assertion costs nothing.
                if (slots.Any(slot => slot.SlotDate.Date != input.PatientAppointment_Date.Date))
                {
                    await transaction.RollbackAsync();
                    result.Reason = AppointmentSaveFailure.SlotWrongDate;
                    return result;
                }

                // (d) 🔴 NO SLOT IS ALREADY BOOKED BY A DIFFERENT APPOINTMENT — the check the transaction
                //     exists for. A slot carrying THIS appointment's own id passes, and that permission
                //     is what makes an edit work: re-saving over hours the appointment already holds must
                //     not be refused as a double booking. On an insert appointmentId is 0, which no real
                //     PatientAppointment_ID can be, so every taken slot fails.
                if (slots.Any(slot => slot.PatientAppointment_ID.HasValue &&
                                      slot.PatientAppointment_ID.Value != appointmentId))
                {
                    await transaction.RollbackAsync();
                    result.Reason = AppointmentSaveFailure.SlotTaken;
                    return result;
                }

                // The hours must be one unbroken run, which is why only the first start and the last end
                // are stored on the row. The times arrive from spStaffSlots_List as VARCHAR(5) strings
                // ("09:00"), so they are parsed here — at the call site that needs them, exactly where the
                // pre-migration code parsed them, and with the same InvariantCulture.
                var sorted = slots
                    .Select(slot => new
                    {
                        Start = TimeSpan.Parse(slot.SlotStartTime, CultureInfo.InvariantCulture),
                        End = TimeSpan.Parse(slot.SlotEndTime, CultureInfo.InvariantCulture)
                    })
                    .OrderBy(slot => slot.Start)
                    .ToList();

                for (var i = 0; i < sorted.Count - 1; i++)
                {
                    if (sorted[i + 1].Start != sorted[i].Start + TimeSpan.FromHours(1))
                    {
                        await transaction.RollbackAsync();
                        result.Reason = AppointmentSaveFailure.SlotsNotConsecutive;
                        return result;
                    }
                }

                var startTime = sorted.First().Start;
                var endTime = sorted.Last().End;

                result.StartTime = startTime;
                result.EndTime = endTime;

                // The persisted set is SEEDED from the request and from the times just derived, then
                // overwritten below by whatever spPatientAppointment_Update actually re-read. The seed is
                // live, not decoration: the procedure fills its OUTPUT parameters inside
                // `IF @RowsAffected > 0`, so an update against an id that matches nothing leaves all eight
                // NULL and these values stand. See AppointmentSaveResult.
                result.PersistedPatientId = input.Patient_ID;
                result.PersistedStaffId = input.Staff_ID;
                result.PersistedDate = input.PatientAppointment_Date;
                result.PersistedStartTime = startTime;
                result.PersistedEndTime = endTime;
                result.PersistedTypeId = input.PjAppType_ID;
                result.PersistedBranchId = input.Branch_ID;
                result.PersistedStatus = input.PatientAppointment_Status;

                if (isInsert)
                {
                    // ── STEP 3a: INSERT ──────────────────────────────────────────────────────────────
                    //
                    // 🔴 ANOTHER OUTPUT PARAMETER, AND ANOTHER PROCEDURE THAT LOOKS LIKE IT HAS A
                    // TRAILING SELECT AND DOES NOT. spPatientAppointment_Insert hands its new identity
                    // back through `@NewPatientAppointment_ID INT OUTPUT`, exactly as
                    // spPatientBasic_Insert does with @NewPatient_ID — so QuerySingleAsync<int> would
                    // throw "Sequence contains no elements" on every successful insert. DynamicParameters
                    // it is, with the parameter's name and type written out where a reader can check them
                    // against the .sql.
                    //
                    // 🔴 spPatientAppointment_Insert declares @User_ID INT = NULL for its dbo.AuditTrails
                    // row: the ACTOR, not a target. DatabaseHelper used to append it automatically off
                    // sys.parameters; Dapper cannot, so it is passed explicitly here. Drop it and the
                    // appointment is still created — with AuditTrails.User_Id = 0, and nothing fails.
                    var insertParameters = new DynamicParameters();

                    insertParameters.AddDynamicParams(new
                    {
                        Patient_ID = input.Patient_ID,
                        PatientAppointment_Date = input.PatientAppointment_Date.Date,
                        Staff_ID = input.Staff_ID,
                        PatientAppointment_StartTime = startTime,
                        PatientAppointment_EndTime = endTime,
                        PjAppType_ID = input.PjAppType_ID,
                        Branch_ID = input.Branch_ID,
                        PatientAppointment_Status = input.PatientAppointment_Status,
                        User_ID = actorUserId
                    });

                    insertParameters.Add("NewPatientAppointment_ID", dbType: DbType.Int32,
                        direction: ParameterDirection.Output);

                    await connection.ExecuteAsync(
                        "dbo.spPatientAppointment_Insert",
                        insertParameters,
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure);

                    appointmentId = insertParameters.Get<int?>("NewPatientAppointment_ID") ?? 0;

                    if (appointmentId <= 0)
                    {
                        // The procedure SETs this from SCOPE_IDENTITY() immediately after the INSERT and
                        // has no path that skips doing so, so this cannot fire. It rolls back rather than
                        // continuing because the alternative is assigning the slots to appointment 0.
                        await transaction.RollbackAsync();
                        result.Reason = AppointmentSaveFailure.InsertFailed;
                        return result;
                    }
                }
                else
                {
                    // ── STEP 3b: UPDATE ──────────────────────────────────────────────────────────────
                    //
                    // EIGHT MORE OUTPUT PARAMETERS. spPatientAppointment_Update re-reads the saved row
                    // into @Out_* so a caller can audit database state rather than the request payload —
                    // its own comment says so — and all eight declare `= NULL` defaults, which is why
                    // they are optional and why they stay NULL when the UPDATE matched nothing.
                    //
                    // spPatientAppointment_Update declares @User_ID INT = NULL: the ACTOR for its
                    // dbo.AuditTrails row, passed explicitly because Dapper has no equivalent of
                    // DatabaseHelper's sys.parameters injection. The procedure audits only when a row
                    // actually changed.
                    var updateParameters = new DynamicParameters();

                    updateParameters.AddDynamicParams(new
                    {
                        PatientAppointment_ID = appointmentId,
                        PatientAppointment_Date = input.PatientAppointment_Date.Date,
                        Staff_ID = input.Staff_ID,
                        PatientAppointment_StartTime = startTime,
                        PatientAppointment_EndTime = endTime,
                        PjAppType_ID = input.PjAppType_ID,
                        Branch_ID = input.Branch_ID,
                        PatientAppointment_Status = input.PatientAppointment_Status,
                        User_ID = actorUserId
                    });

                    AddAppointmentOutputParameters(updateParameters);

                    await connection.ExecuteAsync(
                        "dbo.spPatientAppointment_Update",
                        updateParameters,
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure);

                    // Each one overwrites its seed only when the procedure returned something, so a
                    // no-op update keeps the request-derived values — today's behaviour exactly.
                    result.PersistedPatientId =
                        updateParameters.Get<string?>("Out_Patient_ID") ?? result.PersistedPatientId;
                    result.PersistedStaffId =
                        updateParameters.Get<string?>("Out_Staff_ID") ?? result.PersistedStaffId;
                    result.PersistedDate =
                        updateParameters.Get<DateTime?>("Out_Date") ?? result.PersistedDate;
                    result.PersistedStartTime =
                        updateParameters.Get<TimeSpan?>("Out_StartTime") ?? result.PersistedStartTime;
                    result.PersistedEndTime =
                        updateParameters.Get<TimeSpan?>("Out_EndTime") ?? result.PersistedEndTime;
                    result.PersistedTypeId =
                        updateParameters.Get<string?>("Out_PjAppType_ID") ?? result.PersistedTypeId;
                    result.PersistedBranchId =
                        updateParameters.Get<string?>("Out_Branch_ID") ?? result.PersistedBranchId;
                    result.PersistedStatus =
                        updateParameters.Get<string?>("Out_Status") ?? result.PersistedStatus;

                    // ── STEP 4: RELEASE THE HOURS THIS APPOINTMENT ALREADY HELD ─────────────────────
                    //
                    // spStaffSlots_ClearAppointment, keyed on the APPOINTMENT id rather than on a slot
                    // list, so it releases every hour the appointment held whether or not the caller knew
                    // about it. It runs BEFORE the assign below and the order is the point: an hour kept
                    // across the edit would otherwise be cleared after being re-assigned and end up free
                    // while the appointment believed it held it.
                    //
                    // It declares no @User_ID and writes no dbo.AuditTrails row — verified in the .sql.
                    // The appointment's own UPDATE audit row is the record that this happened.
                    //
                    // Insert path skips it: a brand-new appointment holds nothing to release.
                    await connection.ExecuteAsync(
                        "dbo.spStaffSlots_ClearAppointment",
                        new { ApptId = appointmentId },
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure);
                }

                // ── STEP 5: CLAIM THE HOURS ─────────────────────────────────────────────────────────
                //
                // spStaffSlots_AssignAppointment stamps the appointment id onto every named slot. It
                // takes the ids as ONE COMMA-SEPARATED VARCHAR(MAX) and splits them with STRING_SPLIT +
                // TRY_CAST, so the joined string is the parameter's real shape rather than a shortcut —
                // and it THROWs 50001 on a blank one, which the controller's "at least one slot" check
                // has already made unreachable.
                //
                // 🔴 IT SILENTLY IGNORES AN ID THAT MATCHES NO ROW: the UPDATE is an INNER JOIN against
                // the split list, so a missing slot updates nothing and reports nothing. That is only
                // safe because check (a) above already proved every requested id was in the
                // in-transaction read. Declares no @User_ID and writes no audit row.
                await connection.ExecuteAsync(
                    "dbo.spStaffSlots_AssignAppointment",
                    new
                    {
                        ApptId = appointmentId,
                        StaffSlotIds = string.Join(",", requestedIds)
                    },
                    transaction: transaction,
                    commandType: CommandType.StoredProcedure);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            result.PatientAppointment_ID = appointmentId;

            return result;
        }

        public async Task DeleteAppointmentAsync(int appointmentId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spPatientAppointment_Delete declares @User_ID INT = NULL: the ACTOR for its dbo.AuditTrails
            // row, passed explicitly because Dapper has no equivalent of DatabaseHelper's sys.parameters
            // injection. The audit row is written only when a row actually went.
            //
            // ExecuteAsync and no return value: the procedure emits no result set and reports no row
            // count, so a delete against an unknown id succeeds silently.
            //
            // 🔴 IT FREES THE APPOINTMENT'S SLOTS ITSELF, before the DELETE, because
            // FK_StaffSlots_PatientAppointment would otherwise refuse it. There is no second call to make
            // here and none to add: adding one would be a way to release slots outside this procedure.
            await connection.ExecuteAsync(
                "dbo.spPatientAppointment_Delete",
                new { PatientAppointment_ID = appointmentId, User_ID = _databaseHelper.CurrentUserId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<AppointmentStatusResult> UpdateAppointmentStatusAsync(int appointmentId, string status)
        {
            using var connection = _databaseHelper.CreateConnection();

            // The third OUTPUT-parameter procedure in this area: spPatientAppointment_UpdateStatus
            // re-reads the saved row into the same eight @Out_* parameters spPatientAppointment_Update
            // uses, so a caller audits database state rather than its own request payload.
            //
            // spPatientAppointment_UpdateStatus declares @User_ID INT = NULL: the ACTOR for its
            // dbo.AuditTrails row, passed explicitly because Dapper has no equivalent of DatabaseHelper's
            // sys.parameters injection.
            //
            // 🔴 UNLIKE THE OTHER THREE WRITES IN THIS AREA IT IS NOT SILENT ON A BAD ID — it RAISERRORs
            // 'Appointment not found.' and RETURNs before it reaches the SELECT, so the OUTPUT parameters
            // stay NULL and this method never gets to read them: the SqlException reaches the caller
            // first. That is why the properties below can be non-nullable without a defensive seed, and
            // why the `?? …` fallbacks that SaveAppointmentAsync needs are absent here.
            var parameters = new DynamicParameters();

            parameters.AddDynamicParams(new
            {
                PatientAppointment_ID = appointmentId,
                PatientAppointment_Status = status,
                User_ID = _databaseHelper.CurrentUserId
            });

            AddAppointmentOutputParameters(parameters);

            await connection.ExecuteAsync(
                "dbo.spPatientAppointment_UpdateStatus",
                parameters,
                commandType: CommandType.StoredProcedure);

            return new AppointmentStatusResult
            {
                Patient_ID = parameters.Get<string?>("Out_Patient_ID") ?? string.Empty,
                Staff_ID = parameters.Get<string?>("Out_Staff_ID") ?? string.Empty,
                PatientAppointment_Date = parameters.Get<DateTime?>("Out_Date") ?? default,
                StartTime = parameters.Get<TimeSpan?>("Out_StartTime") ?? default,
                EndTime = parameters.Get<TimeSpan?>("Out_EndTime") ?? default,
                PjAppType_ID = parameters.Get<string?>("Out_PjAppType_ID") ?? string.Empty,
                Branch_ID = parameters.Get<string?>("Out_Branch_ID") ?? string.Empty,
                PatientAppointment_Status = parameters.Get<string?>("Out_Status") ?? string.Empty
            };
        }

        // spPatientAppointment_Update and spPatientAppointment_UpdateStatus declare THE SAME eight @Out_*
        // parameters, in the same types and sizes, for the same reason — so they are declared once, here,
        // and cannot drift between the two call sites. Same arrangement as InsertStaffAsync above.
        //
        // The types are the procedures': VARCHAR(100) for the four ids and the status, DATE for the date,
        // TIME(0) for the two times. AnsiString rather than String on the five text ones because they are
        // VARCHAR, not NVARCHAR — DbType.String would send NVARCHAR for SQL Server to convert back.
        private static void AddAppointmentOutputParameters(DynamicParameters parameters)
        {
            parameters.Add("Out_Patient_ID", dbType: DbType.AnsiString, size: 100,
                direction: ParameterDirection.Output);
            parameters.Add("Out_Staff_ID", dbType: DbType.AnsiString, size: 100,
                direction: ParameterDirection.Output);
            parameters.Add("Out_Date", dbType: DbType.Date,
                direction: ParameterDirection.Output);
            parameters.Add("Out_StartTime", dbType: DbType.Time,
                direction: ParameterDirection.Output);
            parameters.Add("Out_EndTime", dbType: DbType.Time,
                direction: ParameterDirection.Output);
            parameters.Add("Out_PjAppType_ID", dbType: DbType.AnsiString, size: 100,
                direction: ParameterDirection.Output);
            parameters.Add("Out_Branch_ID", dbType: DbType.AnsiString, size: 100,
                direction: ParameterDirection.Output);
            parameters.Add("Out_Status", dbType: DbType.AnsiString, size: 100,
                direction: ParameterDirection.Output);
        }

        // ----- Patient journey (Staff > Patient) -----
        //
        // Twelve procedures, and 🔴 NOT ONE OF THEM DECLARES @User_ID — not the three journey reads, not
        // the three detail reads, not the six writes. Checked in every .sql file rather than inferred from
        // the other feature areas, and it means none of them writes a dbo.AuditTrails row either. The six
        // writes take @Staff_ID instead, which is A DIFFERENT IDENTITY: the clinician the journey belongs
        // to, an ordinary business argument from the controller's "StaffId" claim, landing in
        // dbo.PatientJourney.Staff_ID and dbo.PatientJourneyAudit.Staff_ID. It must never be filled from
        // DatabaseHelper.CurrentUserId, which is a dbo.Users id. See IDatabaseData and CoreFlow.md §7.
        //
        // 🔴 THE SIX WRITES EACH HOLD THEIR OWN TRANSACTION — `SET XACT_ABORT ON; BEGIN TRY BEGIN TRAN`,
        // COMMIT, and ROLLBACK + THROW from the CATCH — around all three tables they touch. So each is one
        // ordinary Dapper call and NOT a unit of work here; there is no connection or transaction managed
        // by hand anywhere in this banner.

        public async Task<PatientJourneyDetail?> GetJourneyByIdAsync(int patientJourneyId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // SELECT TOP 1 on the identity, INNER JOIN dbo.PatientBasic — at most one row, and none at all
            // when the id is unknown OR when the journey's patient has been deleted. No @User_ID: a read.
            return await connection.QuerySingleOrDefaultAsync<PatientJourneyDetail>(
                "dbo.spPatientJourney_GetById",
                new { PatientJourney_ID = patientJourneyId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<List<PatientJourneyTimelineItem>> GetJourneyTimelineAsync(string patientId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // The two OUTER APPLYs over dbo.PatientJourneyAudit alias their columns to CreatedAt /
            // CreatedByStaffId / CreatedByStaffName and UpdatedAt / UpdatedByStaffId / UpdatedByStaffName,
            // so PatientJourneyTimelineItem's property names match those ALIASES rather than the audit
            // table's column names. Dapper maps by name; a property called Audit_At would stay null on
            // every row and nothing would say so.
            //
            // No @User_ID: a read. An unknown patient id returns an empty set, not an error.
            var results = await connection.QueryAsync<PatientJourneyTimelineItem>(
                "dbo.spPatientJourney_TimelineByPatient",
                new { Patient_ID = patientId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<PatientJourneyAuditItem>> GetJourneyAuditsAsync(string patientId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // No @User_ID: a read — and note that the thing it reads is nucentra's OTHER audit trail,
            // dbo.PatientJourneyAudit, which is keyed on a Staff_ID and is nothing to do with the
            // dbo.AuditTrails rows the @User_ID actor names.
            var results = await connection.QueryAsync<PatientJourneyAuditItem>(
                "dbo.spPatientJourney_AuditsByPatient",
                new { Patient_ID = patientId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // 🔴 THE SECOND PLACE IN THIS FILE THAT READS A RESULT SET WITHOUT A MODEL, and — like
        // QueryLookupAsync above — it is one helper doing it in the open rather than three call sites doing
        // it by accident.
        //
        // The three detail endpoints serialize their row AS THE PROCEDURE NAMES ITS COLUMNS. The browser
        // gets {"PatientJourney_ID":5,"iFOBTPositive_Date":"…","Risks_Smoking":true,…} and the three
        // template scripts read exactly those keys. ASP.NET Core serializes with JsonSerializerDefaults.Web,
        // which camelCases PROPERTY names and leaves DICTIONARY KEYS untouched — so a POCO would ship
        // "patientJourney_ID" and "risks_Smoking", break all three clinical forms, and return 200 while
        // doing it. A dictionary keyed on the column name is therefore the shape, not a shortcut.
        //
        // It reads the field NAMES off the reader rather than accepting a `dynamic` row, for two reasons:
        // this layer does not deal in dynamic (IDatabaseData rule 5), and doing it explicitly is what makes
        // the two properties the JSON depends on visible —
        //
        //   • KEY ORDER IS THE PROCEDURE'S SELECT ORDER, preserved because Dictionary<,> enumerates in
        //     insertion order and nothing here removes a key. That reproduces the DataTable column order
        //     this replaced.
        //   • DBNull BECOMES null, which is what DataRow-plus-`== DBNull.Value ? null : …` produced, and
        //     what the templates test for.
        //
        // Null means NO DETAIL ROW, which is a real state rather than an error: all three procedures INNER
        // JOIN their detail table, so a COLONOSCOPY journey asked for as an assessment returns nothing
        // while the journey row itself still exists. The endpoint answers {success:true, assessment:null}.
        private async Task<IReadOnlyDictionary<string, object?>?> QueryJourneyDetailRowAsync(
            string procedureName, int patientJourneyId)
        {
            using var connection = _databaseHelper.CreateConnection();
            using var reader = await connection.ExecuteReaderAsync(
                procedureName,
                new { PatientJourney_ID = patientJourneyId },
                commandType: CommandType.StoredProcedure);

            if (!reader.Read())
            {
                return null;
            }

            var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            return row;
        }

        public Task<IReadOnlyDictionary<string, object?>?> GetAssessmentByJourneyIdAsync(int patientJourneyId) =>
            QueryJourneyDetailRowAsync("dbo.spPatientAssessment_GetByJourneyId", patientJourneyId);

        public Task<IReadOnlyDictionary<string, object?>?> GetColonoscopyByJourneyIdAsync(int patientJourneyId) =>
            QueryJourneyDetailRowAsync("dbo.spPatientColonoscopy_GetByJourneyId", patientJourneyId);

        public Task<IReadOnlyDictionary<string, object?>?> GetFollowUpByJourneyIdAsync(int patientJourneyId) =>
            QueryJourneyDetailRowAsync("dbo.spPatientFollowUp_GetByJourneyId", patientJourneyId);

        // The create and the update declare THE SAME clinical parameters and differ in exactly one: the
        // create takes @Patient_ID, the update takes @PatientJourney_ID. Sending both would fail with
        // "has no parameter named …" on whichever the procedure does not declare, so the shared block is
        // built once here and each caller adds its own key.
        //
        // DynamicParameters rather than an anonymous object because that is what lets the two paths share
        // the block; there is no OUTPUT parameter anywhere in this feature area. Property names are the
        // procedure's parameter names verbatim, INCLUDING iFOBTPositive_Date's lower-case first letter.
        private static DynamicParameters BuildAssessmentParameters(PatientAssessmentSaveInput input)
        {
            var parameters = new DynamicParameters();

            parameters.AddDynamicParams(new
            {
                input.PatientJourney_Date,
                input.Staff_ID,
                input.Audit_Note,

                input.iFOBTPositive_Date,
                input.Risks_Smoking,
                input.Risks_AlcoholConsumption,
                input.Risks_InflammatoryBowelDisease,
                input.Risks_Diet,
                input.Risks_SedentaryLifestyle,

                input.Symptoms_WeightLoss,
                input.Symptoms_AppetiteLoss,
                input.Symptoms_Lethargic,
                input.Symptoms_AbdominalPain,
                input.Symptoms_Constipation,
                input.Symptoms_Diarrhea,
                input.Symptoms_RectalBleedingMucous,
                input.Symptoms_RectalBleedingNoMucous,
                input.Symptoms_Tenesmus,

                input.MedicalHistory_Diabetes,
                input.MedicalHistory_Hypertension,
                input.MedicalHistory_Dyslipidemia,
                input.MedicalHistory_Bleeding,
                input.MedicalHistory_Asthma,

                input.AllergyHistory_Medication,
                input.AllergyHistory_MedicationDetails,
                input.AllergyHistory_Food,
                input.AllergyHistory_FoodDetails,

                input.MedicationHistory_Anticoagulant,
                input.MedicationHistory_AnticoagulantDetails,
                input.MedicationHistory_Narcotics,
                input.MedicationHistory_NarcoticsDetails,
                input.MedicationHistory_Insulin,
                input.MedicationHistory_InsulinDetails,
                input.MedicationHistory_AntiHypertensives,
                input.MedicationHistory_AntiHypertensivesDetails,

                input.PreviousScope_Date,

                input.FamilyHistory_FirstDegree,
                input.FamilyHistory_SecondDegree,

                input.PhysicalExamination_Details,

                input.Investigation_FBC,
                input.Investigation_BUSE,
                input.Investigation_RBS,
                input.Investigation_LFT,
                input.Investigation_Coag,

                input.Management_BowelPrep,
                input.Management_Procedure,
                input.Management_Consent,
                input.Management_Advise
            });

            return parameters;
        }

        public async Task<int> CreateAssessmentWithJourneyAsync(PatientAssessmentSaveInput input)
        {
            using var connection = _databaseHelper.CreateConnection();

            var parameters = BuildAssessmentParameters(input);
            parameters.Add("Patient_ID", input.Patient_ID);

            // 🔴 THE PROCEDURE OWNS THE TRANSACTION, NOT THIS METHOD. spPatientAssessment_CreateWithJourney
            // wraps dbo.PatientJourney + dbo.PatientAssessment + dbo.PatientJourneyAudit in its own
            // BEGIN TRAN … COMMIT with ROLLBACK and THROW in its CATCH, so all three land together or none
            // does. Opening a SqlTransaction around it here would nest a transaction inside one that
            // already exists and would advertise, in IDatabaseData, an atomicity guarantee this layer is
            // not the source of.
            //
            // QuerySingleAsync, not Execute: it ends with `SELECT @PatientJourney_ID AS PatientJourney_ID`
            // — a real result set, unlike spPatientBasic_Insert and spPatientAppointment_Insert, which both
            // answer through an OUTPUT parameter. The family resemblance is the trap; this one is a SELECT.
            //
            // No @User_ID of either kind: the procedure declares none and writes no dbo.AuditTrails row.
            // @Staff_ID is the CLINICIAN, a business argument, and the procedure RAISERRORs 'Staff not
            // found.' if it does not resolve — which is exactly what would happen if somebody filled it
            // from DatabaseHelper.CurrentUserId.
            return await connection.QuerySingleAsync<int>(
                "dbo.spPatientAssessment_CreateWithJourney",
                parameters,
                commandType: CommandType.StoredProcedure);
        }

        public async Task UpdateAssessmentWithJourneyAsync(PatientAssessmentSaveInput input)
        {
            using var connection = _databaseHelper.CreateConnection();

            var parameters = BuildAssessmentParameters(input);
            parameters.Add("PatientJourney_ID", input.PatientJourney_ID);

            // 🔴 UPDATES THE EXISTING JOURNEY ROW; IT DOES NOT INSERT ONE. That is the whole reason the
            // create and the update are separate procedures, and an update that inserted would duplicate
            // the patient's timeline without erroring. Same own-transaction arrangement as the create.
            //
            // ExecuteAsync: it ends with `SELECT 1 AS Success`, which nothing has ever read. Its
            // colonoscopy sibling ends with nothing at all — so neither can be turned into a
            // QuerySingleAsync without opening the .sql first.
            await connection.ExecuteAsync(
                "dbo.spPatientAssessment_UpdateWithJourney",
                parameters,
                commandType: CommandType.StoredProcedure);
        }

        // Same create/update parameter split as BuildAssessmentParameters, for the same reason.
        private static DynamicParameters BuildColonoscopyParameters(PatientColonoscopySaveInput input)
        {
            var parameters = new DynamicParameters();

            parameters.AddDynamicParams(new
            {
                input.PatientJourney_Date,
                input.Staff_ID,
                input.Audit_Note,

                input.ColonoscopyStatus,
                input.ColonoscopyStatus_Details,
                input.BowelPreparation,

                input.Findings_Anus,
                input.Findings_AnusDetails,
                input.Findings_Rectum,
                input.Findings_RectumDetails,
                input.Findings_SigmoidColon,
                input.Findings_SigmoidColonDetails,
                input.Findings_DescendingColon,
                input.Findings_DescendingColonDetails,
                input.Findings_SplenicFlexure,
                input.Findings_SplenicFlexureDetails,
                input.Findings_TransverseColon,
                input.Findings_TransverseColonDetails,
                input.Findings_HepaticFlexure,
                input.Findings_HepaticFlexureDetails,
                input.Findings_AscendingColon,
                input.Findings_AscendingColonDetails,
                input.Findings_Caecum,
                input.Findings_CaecumDetails,

                input.HPE_Status,
                input.HPE_Details,

                input.Complications,
                input.Complications_Details,

                input.DischargePlan,

                input.Medication_Details
            });

            return parameters;
        }

        public async Task<int> CreateColonoscopyWithJourneyAsync(PatientColonoscopySaveInput input)
        {
            using var connection = _databaseHelper.CreateConnection();

            var parameters = BuildColonoscopyParameters(input);
            parameters.Add("Patient_ID", input.Patient_ID);

            // Own transaction inside the procedure, trailing SELECT of the new PatientJourney_ID, no
            // @User_ID — as the assessment create above. The one difference is that this procedure only
            // refuses a BLANK @Patient_ID and never looks the patient up, so a colonoscopy can be recorded
            // against a patient id that resolves to nothing. Nothing here compensates for that; the check
            // belongs in the procedure and adding it would be a .sql change.
            return await connection.QuerySingleAsync<int>(
                "dbo.spPatientColonoscopy_CreateWithJourney",
                parameters,
                commandType: CommandType.StoredProcedure);
        }

        public async Task UpdateColonoscopyWithJourneyAsync(PatientColonoscopySaveInput input)
        {
            using var connection = _databaseHelper.CreateConnection();

            var parameters = BuildColonoscopyParameters(input);
            parameters.Add("PatientJourney_ID", input.PatientJourney_ID);

            // Updates the journey row and the detail row and writes an 'UPDATED' audit row; no second
            // journey row. The only one of the six writes with NO trailing SELECT of any kind.
            await connection.ExecuteAsync(
                "dbo.spPatientColonoscopy_UpdateWithJourney",
                parameters,
                commandType: CommandType.StoredProcedure);
        }

        // Three clinical parameters against the assessment's forty-five, and the same create/update split.
        private static DynamicParameters BuildFollowUpParameters(PatientFollowUpSaveInput input)
        {
            var parameters = new DynamicParameters();

            parameters.AddDynamicParams(new
            {
                input.PatientJourney_Date,
                input.Staff_ID,
                input.Audit_Note,

                input.HPE_Results,
                input.DischargePlan,
                input.DischargeSummary_Status
            });

            return parameters;
        }

        public async Task<int> CreateFollowUpWithJourneyAsync(PatientFollowUpSaveInput input)
        {
            using var connection = _databaseHelper.CreateConnection();

            var parameters = BuildFollowUpParameters(input);
            parameters.Add("Patient_ID", input.Patient_ID);

            // 🔴 This is the procedure that writes PjAppType_Name = 'PATIENT FOLLOW UP' while
            // LU_PJ_APP_TYPE holds "FOLLOW UP". The literal is inside the .sql and nothing joins the two,
            // so nothing detects it — see PatientFollowUpSaveInput and CoreFlow.md §7.
            //
            // Own transaction, trailing SELECT of the new PatientJourney_ID, no @User_ID. It DOES validate
            // the patient, unlike the colonoscopy create.
            return await connection.QuerySingleAsync<int>(
                "dbo.spPatientFollowUp_CreateWithJourney",
                parameters,
                commandType: CommandType.StoredProcedure);
        }

        public async Task UpdateFollowUpWithJourneyAsync(PatientFollowUpSaveInput input)
        {
            using var connection = _databaseHelper.CreateConnection();

            var parameters = BuildFollowUpParameters(input);
            parameters.Add("PatientJourney_ID", input.PatientJourney_ID);

            await connection.ExecuteAsync(
                "dbo.spPatientFollowUp_UpdateWithJourney",
                parameters,
                commandType: CommandType.StoredProcedure);
        }

        // ----- Patient documents -----
        //
        // Five procedures, and 🔴 THE @User_ID PICTURE IS NOT UNIFORM. Two of the five declare
        // `@User_ID INT = NULL` — the ACTOR — and each says so on its own call: spPatientDocument_Insert and
        // spPatientDocument_Delete. The other three declare none and must not be given one.
        //
        // 🔴 spPatientDocument_GetById IS THE TRAP. Its header comment contains the words "no @User_ID and
        // no audit row", so a grep for "@User_ID" matches the FILE — and its parameter list is
        // `@PatientDocument_ID INT` and nothing else. Read the parameter list, not the grep.

        public async Task<List<PatientDocumentItem>> GetPatientDocumentsAsync(string patientId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // No @User_ID: a read, no audit row. @Patient_ID is REQUIRED here — unlike spStaffDocument_List,
            // whose @Staff_ID defaults to NULL and returns every document in the system when omitted.
            var results = await connection.QueryAsync<PatientDocumentItem>(
                "dbo.spPatientDocument_List",
                new { Patient_ID = patientId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<PatientDocumentItem?> GetPatientDocumentByIdAsync(int patientDocumentId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 NO @User_ID PARAMETER EXISTS ON THIS PROCEDURE. Its comment mentions the name; its
            // signature does not declare it. Sending one would throw "has no parameter named '@User_ID'".
            //
            // SELECT TOP 1 on the identity: at most one row, none when the id is unknown.
            return await connection.QuerySingleOrDefaultAsync<PatientDocumentItem>(
                "dbo.spPatientDocument_GetById",
                new { PatientDocument_ID = patientDocumentId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task AddPatientDocumentAsync(PatientDocumentInput document)
        {
            using var connection = _databaseHelper.CreateConnection();

            // spPatientDocument_Insert declares @User_ID INT = NULL for its dbo.AuditTrails row: the ACTOR,
            // not a target. DatabaseHelper used to append it automatically off sys.parameters — the
            // controller never mentioned it — and Dapper cannot, so it is passed explicitly here. Drop it
            // and the document is still recorded, with AuditTrails.User_Id = 0 and nothing failing.
            //
            // ExecuteAsync and no return value: the procedure emits no result set and does NOT hand back
            // the new identity, which is why the caller's audit line records DocumentId=0 and names the
            // blob key instead.
            await connection.ExecuteAsync(
                "dbo.spPatientDocument_Insert",
                new
                {
                    document.Patient_ID,
                    document.Patient_Name,
                    document.PatientDocumentType_ID,
                    document.PatientDocumentType_Name,
                    document.FileName,
                    document.BlobName,
                    document.ContentType,
                    User_ID = _databaseHelper.CurrentUserId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<string?> DeletePatientDocumentAsync(int patientDocumentId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // The patient twin of DeleteStaffDocumentAsync, and the second call in this file that needs
            // DynamicParameters for the same reason: spPatientDocument_Delete answers through
            // `@DeletedBlobName VARCHAR(500) = NULL OUTPUT`, and an OUTPUT parameter is the one thing
            // Dapper cannot reach through an anonymous object. The name and the type are written out where
            // a reader can check them against the .sql.
            //
            // AnsiString, not String: the parameter is VARCHAR(500), and DbType.String would send NVARCHAR
            // for SQL Server to convert back.
            //
            // @User_ID INT = NULL is the ACTOR for the dbo.AuditTrails row, passed explicitly as everywhere
            // else. The procedure audits only when a row was actually deleted.
            var parameters = new DynamicParameters();
            parameters.Add("PatientDocument_ID", patientDocumentId);
            parameters.Add("User_ID", _databaseHelper.CurrentUserId);
            parameters.Add("DeletedBlobName", dbType: DbType.AnsiString, size: 500,
                direction: ParameterDirection.Output);

            await connection.ExecuteAsync(
                "dbo.spPatientDocument_Delete",
                parameters,
                commandType: CommandType.StoredProcedure);

            // NULL means no row was deleted, so there is nothing in storage to remove.
            return parameters.Get<string?>("DeletedBlobName");
        }

        // The patient twin of GetStaffDocumentTypeFiltersAsync, and it reads the same two columns in the
        // same order — code first, display name second — so QueryLookupAsync's ordinal mapping applies
        // unchanged. It is NOT one of the fourteen spLU_* procedures, but it is the same shape, and the
        // helper's contract is the shape rather than the folder.
        //
        // No @User_ID: a read. No caller until Prompt 8's Documents search page.
        public Task<List<LookupItem>> GetPatientDocumentTypeFiltersAsync() =>
            QueryLookupAsync("dbo.spPatientDocument_LookupDocuments");

        // ----- Document settings (Admin > Settings) -----
        //
        // Five procedures, and NOT ONE OF THEM DECLARES @User_ID — verified by reading all five parameter
        // lists, not by grepping. Nothing in this area writes a dbo.AuditTrails row.
        //
        // 🔴 THE SAVE ASYMMETRY. The patient side replaces a discharge type's whole set inside ONE
        // procedure; the staff side has no such procedure and the controller runs a DELETE followed by N
        // INSERTs with NO TRANSACTION around them — which is what it has always done. It is left that way
        // deliberately: adding a transaction here would change behaviour, and this migration does not.
        // The two staff procedures are therefore two methods, and the sequencing stays in
        // SettingsController where a reader can see it. See IDatabaseData.cs and CoreFlow.md §5.9.

        public async Task<List<PatientDocumentSetting>> GetDischargeDocumentSettingsAsync(string dischargeTypeId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // A plain SELECT over dbo.PatientDocumentSettings — so it returns ONLY the mandatory types,
            // where the staff read returns every type with a flag. An unknown discharge type is an empty
            // list, not an error.
            var results = await connection.QueryAsync<PatientDocumentSetting>(
                "dbo.spPatientDocumentSettings_GetByDischargeType",
                new { DischargeType_ID = dischargeTypeId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task SaveDischargeDocumentSettingsAsync(string dischargeTypeId, string? patientDocumentTypeIdsCsv)
        {
            using var connection = _databaseHelper.CreateConnection();

            // @PatientDocumentType_IDs is NVARCHAR(MAX) and the procedure splits it with STRING_SPLIT, so
            // the ids travel as one CSV string rather than as a list. NULL clears the discharge reason's
            // settings — that is the empty-checklist save, not a no-op.
            //
            // ExecuteAsync: the procedure emits no result set. It RAISERRORs on an unknown
            // @DischargeType_ID, which arrives here as a SqlException for the caller to catch.
            await connection.ExecuteAsync(
                "dbo.spPatientDocumentSettings_SaveForDischargeType",
                new
                {
                    DischargeType_ID = dischargeTypeId,
                    PatientDocumentType_IDs = patientDocumentTypeIdsCsv
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task DeleteStaffDocumentSettingsAsync(string staffTypeId)
        {
            using var connection = _databaseHelper.CreateConnection();

            // Step one of the staff save. On its own it leaves the staff type with NO mandatory documents,
            // which is exactly what an empty-checklist save wants and exactly what a crashed save leaves
            // behind. No @User_ID, no audit row.
            await connection.ExecuteAsync(
                "dbo.spStaffDocumentSettings_DeleteByStaffType",
                new { StaffType_ID = staffTypeId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task AddStaffDocumentSettingAsync(string staffTypeId, string? staffTypeName,
            string staffDocumentTypeId, string staffDocumentTypeName)
        {
            using var connection = _databaseHelper.CreateConnection();

            // Step two, run once per selected document type on its own connection. A bare INSERT with no
            // upsert: a repeat of the same (StaffType_ID, StaffDocumentType_ID) pair violates the composite
            // primary key and throws, so the caller de-duplicates and only ever runs this after the delete.
            await connection.ExecuteAsync(
                "dbo.spStaffDocumentSettings_Insert",
                new
                {
                    StaffType_ID = staffTypeId,
                    StaffType_Name = staffTypeName,
                    StaffDocumentType_ID = staffDocumentTypeId,
                    StaffDocumentType_Name = staffDocumentTypeName
                },
                commandType: CommandType.StoredProcedure);
        }

        // ----- Documents (the SUPERUSER search page) -----

        public async Task<List<DocumentSearchItem>> SearchDocumentsAsync(string mode, string? individualName,
            string? documentType)
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 @Mode PICKS THE TABLE, and an unrecognised value is not an error: the procedure's third
            // branch returns the same seven columns with `WHERE 1 = 0`. Both filters are optional and the
            // procedure NULLIFs a blank itself, so passing null and passing "" mean the same thing — null
            // is passed because that is what "no filter" means, not because the procedure needs it.
            //
            // One model for both branches because the procedure aliases both to the same seven column
            // names. See DocumentSearchItem, and note that BlobName comes back and must not be projected.
            var results = await connection.QueryAsync<DocumentSearchItem>(
                "dbo.spDocuments_Search",
                new
                {
                    Mode = mode,
                    IndividualName = individualName,
                    DocumentType = documentType
                },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<string>> GetPatientDocumentPatientNamesAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // One VARCHAR column, so the row type is the column type — the same shape as
            // GetStaffDocumentStaffNamesAsync. The procedure's own SELECT DISTINCT, its WHERE excluding
            // blank names and its ORDER BY are the whole contract; nothing here re-sorts.
            var results = await connection.QueryAsync<string>(
                "dbo.spPatientDocument_PatientNames",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // ----- Dashboard (SUPERUSER) -----
        //
        // Four parameterless aggregates. No @User_ID on any of them — verified by reading all four
        // parameter lists, which are empty. Nothing here writes, so nothing here audits.

        public async Task<int> GetActiveBranchCountAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // ExecuteScalarAsync, not QuerySingleAsync, and the difference is only visible in a case that
            // cannot happen: `COUNT(*)` with no GROUP BY always returns exactly one row, so both are
            // correct — but ExecuteScalarAsync answers 0 for an empty result set where QuerySingleAsync
            // throws, which is precisely what the DataTable code this replaces did with its
            // `if (dt.Rows.Count > 0)` guard. The defensive zero stays defensive.
            return await connection.ExecuteScalarAsync<int>(
                "dbo.spDashboard_Branch_CountActive",
                commandType: CommandType.StoredProcedure);
        }

        public async Task<List<PatientsByRaceItem>> GetPatientsByRaceAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // Mapped by name: the procedure's COALESCE is aliased back to Race_Name, so the two columns
            // carry the names the model declares. Ordered by count descending, inside SQL.
            var results = await connection.QueryAsync<PatientsByRaceItem>(
                "dbo.spDashboard_Patient_ByRace",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<PatientsByAgeGroupItem>> GetPatientsByAgeGroupAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // The five age bands and their order are both the procedure's, computed in a CASE over
            // dbo.PatientBasic.Patient_Age and ordered by a second CASE. There is no C# equivalent to keep
            // in step, and there must not be one.
            var results = await connection.QueryAsync<PatientsByAgeGroupItem>(
                "dbo.spDashboard_Patient_ByAgeGroup",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<PatientsByDischargeTypeItem>> GetPatientsByDischargeTypeAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 The only one of the three charts with a WHERE: `DischargeType_ID IS NOT NULL`. Its total is
            // the discharged population, not the patient population, and the other two cannot be read
            // against it.
            var results = await connection.QueryAsync<PatientsByDischargeTypeItem>(
                "dbo.spDashboard_Patient_ByDischargeType",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // ----- Staff dashboard (STAFF) -----
        //
        // 🔴 staffId IS A SCOPING PREDICATE, NOT A CONVENIENCE. All three procedures filter on
        // `pa.Staff_ID = @Staff_ID`, and that is the only thing that keeps one clinician out of another's
        // diary. The value arrives as an argument, resolved from the caller's own StaffId claim by
        // StaffDashboardController. NOTHING IN THIS CLASS MAY FILL IT FROM A CLAIM: the @User_ID actor
        // injection is bookkeeping, this is authorization, and authorization belongs where the endpoint can
        // be held to it. None of the three declares @User_ID; none writes an audit row.

        public async Task<List<StaffDashboardAppointmentItem>> GetStaffTodayAppointmentsAsync(string staffId, DateTime forDate)
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<StaffDashboardAppointmentItem>(
                "dbo.spStaffDashboard_TodayAppointments",
                new { Staff_ID = staffId, ForDate = forDate },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<StaffDashboardAppointmentItem>> GetStaffWeekAppointmentsAsync(string staffId, DateTime fromDate)
        {
            using var connection = _databaseHelper.CreateConnection();

            // The seven-day window is the procedure's: @FromDate inclusive to @FromDate + 7 days exclusive.
            // A rolling week from the date given, not a calendar week.
            var results = await connection.QueryAsync<StaffDashboardAppointmentItem>(
                "dbo.spStaffDashboard_ThisWeekAppointments",
                new { Staff_ID = staffId, FromDate = fromDate },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<StaffDashboardAppointmentItem>> GetStaffMonthAppointmentsAsync(string staffId, int year, int month)
        {
            using var connection = _databaseHelper.CreateConnection();

            // DATEFROMPARTS(@Year, @Month, 1) inside the procedure THROWS on a month outside 1-12 rather
            // than returning nothing, which is why the caller validates the range first and answers for
            // itself. Nothing is validated here.
            var results = await connection.QueryAsync<StaffDashboardAppointmentItem>(
                "dbo.spStaffDashboard_ThisMonthAppointments",
                new { Staff_ID = staffId, Year = year, Month = month },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // ----- Patient tracker -----
        //
        // Five parameterless reads, none declaring @User_ID, loaded together by one endpoint. The page
        // filters client-side, so everything the tracker can show is fetched on every load.

        // The tracker's own copy of the journey-type list — a different procedure from
        // spLU_PJ_AppType_List over the same table, and therefore its own method. Same two-column shape, so
        // the ordinal helper serves it; see the note above QueryLookupAsync.
        public Task<List<LookupItem>> GetTrackerAppointmentTypesAsync() =>
            QueryLookupAsync("dbo.spPatientTracker_AppointmentTypes_List");

        public async Task<List<PatientTrackerPatientItem>> GetTrackerPatientsAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // 🔴 IsStalled is computed in SQL — latest appointment per patient, and stalled when its status
            // is not 'Scheduled'. spPatientTracker_StalledCount_Get repeats the same CTE to produce the
            // badge. Two copies of one business rule; change one and you must change the other.
            var results = await connection.QueryAsync<PatientTrackerPatientItem>(
                "dbo.spPatientTracker_Patients_List",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<PatientTrackerAppointmentItem>> GetTrackerAppointmentsAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // One row per (patient, journey type) — the latest booking only, ranked inside the procedure.
            // No outer ORDER BY: the caller indexes the result by the two ids rather than reading it in
            // sequence.
            var results = await connection.QueryAsync<PatientTrackerAppointmentItem>(
                "dbo.spPatientTracker_Appointments_List",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<PatientTrackerProcedureItem>> GetTrackerProceduresAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // Every dbo.PatientJourney row, unfiltered — what was DONE, against the list above, which is
            // what was BOOKED. The type arrives as a name here and as an id there, because the two tables
            // store it differently.
            var results = await connection.QueryAsync<PatientTrackerProcedureItem>(
                "dbo.spPatientTracker_Procedures_List",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<int> GetTrackerStalledCountAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // ExecuteScalarAsync for the same reason as GetActiveBranchCountAsync: one guaranteed row, and
            // an empty set answers 0 instead of throwing, exactly as the DataTable guard did.
            return await connection.ExecuteScalarAsync<int>(
                "dbo.spPatientTracker_StalledCount_Get",
                commandType: CommandType.StoredProcedure);
        }

        // ----- Audit trails (SUPERUSER) -----
        //
        // The only four procedures in the portal that READ dbo.AuditTrails. Not one of them declares
        // @User_ID and not one writes a row: looking at the audit trail is not itself audited.
        //
        // 🔴 spAuditTrails_Search's `@UserId INT = NULL` IS A FILTER, NOT AN ACTOR. It is spelled without
        // the underscore, it is chosen by the SUPERUSER from a dropdown, and it must be passed straight
        // through from the caller. Filling it from _databaseHelper.CurrentUserId — the reflex nineteen
        // other call sites in this file deliberately have — would quietly restrict every search to the
        // searcher's own actions and look like an empty audit trail for everybody else.

        public async Task<List<AuditTrailSearchItem>> SearchAuditTrailsAsync(int? userId, DateTime? fromDate,
            DateTime? toDate, string? action, string? category)
        {
            using var connection = _databaseHelper.CreateConnection();

            // All five parameters default to NULL in the procedure and NULL means "no filter" for each, so
            // nulls are passed as nulls rather than being branched on here. The dates are DATE parameters
            // compared against DATEADD(HOUR, 8, AuditTrail_EventUTC) — Malaysian local time, with @ToDate
            // inclusive of its whole day.
            var results = await connection.QueryAsync<AuditTrailSearchItem>(
                "dbo.spAuditTrails_Search",
                new
                {
                    UserId = userId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Action = action,
                    Category = category
                },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // (User_ID, User_Name) — the two-column shape the ordinal helper exists for, with an INT id that it
        // stringifies. That stringification is not incidental: the endpoint's JSON has always carried this
        // id as a string, because DataRow["User_ID"].ToString() did the same.
        public Task<List<LookupItem>> GetAuditTrailUsersAsync() =>
            QueryLookupAsync("dbo.spAuditTrails_LookupUsers");

        public async Task<List<string>> GetAuditTrailActionsAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            // ONE column, so the row type is the column type — QueryLookupAsync cannot serve these two,
            // because it reads ordinal 1 and there is no ordinal 1. The procedure's DISTINCT, its exclusion
            // of NULL and blank, and its ORDER BY are the whole contract.
            var results = await connection.QueryAsync<string>(
                "dbo.spAuditTrails_LookupActions",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        public async Task<List<string>> GetAuditTrailCategoriesAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            var results = await connection.QueryAsync<string>(
                "dbo.spAuditTrails_LookupCategories",
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }

        // ----- Agent API (machine-callable surface — CoreFlow.md §13) -----

        // 🔴 NO @User_ID, AND NOT BECAUSE IT WAS FORGOTTEN. spAgentUsers_GetServiceAccount declares none
        // of either kind: it writes no dbo.AuditTrails row, so there is no ACTOR to record, and it does
        // not operate on a user row chosen by a caller, so it is not the TARGET kind either. More than
        // that — it runs BEFORE ANY PRINCIPAL EXISTS. The request arrives with no cookie,
        // _databaseHelper.CurrentUserId is null, and the row this call returns is what the Agent API's
        // filter builds that principal FROM. The reflex the other nineteen call sites in this file have
        // would be asking this one for the answer it is being called to produce (§0.1, §13.3).
        //
        // QuerySingleOrDefaultAsync, not QuerySingleAsync: the procedure is `SELECT TOP 1 … WHERE
        // Username = 'AGENT_SERVICE'` over a UNIQUE index, so it returns at most one row — and NO ROW IS
        // A MEANINGFUL ANSWER, not an exception. It means the database was published without the seed,
        // and the caller is required to turn that null into a 503 rather than continue with a null actor.
        public async Task<AgentServiceAccount?> GetAgentServiceAccountAsync()
        {
            using var connection = _databaseHelper.CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<AgentServiceAccount>(
                "dbo.spAgentUsers_GetServiceAccount",
                commandType: CommandType.StoredProcedure);
        }
    }
}
