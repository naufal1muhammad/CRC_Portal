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
    }
}
