using System.Data;
using Dapper;
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
    }
}
