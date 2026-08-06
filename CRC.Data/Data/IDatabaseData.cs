using CRC.Data.Models;

namespace CRC.Data.Data
{
    // =================================================================================================
    // The data-access contract for nucentra. Every stored procedure in CRC.Database gets exactly one
    // method here, and SqlData is its only implementation. A controller depends on this interface and
    // never on DatabaseHelper, SqlParameter, SqlConnection or DataTable.
    //
    // This file is the DOCUMENTATION of the data layer; SqlData.cs is the mechanism. Read this one to
    // find out what the portal can ask the database. Read that one only when you need to know how.
    //
    // -------------------------------------------------------------------------------------------------
    // THE RULES — every prompt in DapperLayerPlan.md obeys these, and so does every later change
    // -------------------------------------------------------------------------------------------------
    //
    // 1. ONE METHOD PER STORED PROCEDURE. No method calls two procedures. The two transactional units of
    //    work (Prompts 3 and 6) are the only exceptions, and each is named and commented as such where it
    //    is declared — a reader must never have to guess whether a method is atomic.
    //
    // 2. NAME THE METHOD FOR WHAT IT DOES, NOT FOR THE PROCEDURE. GetActiveBranchesAsync, not
    //    SpBranchListActiveAsync. The procedure name belongs in the comment above it and in the Dapper
    //    call inside SqlData; a caller reading the interface should not need to know it exists.
    //
    // 3. EVERY METHOD CARRIES A `//` COMMENT saying what it is for and naming the procedure it calls.
    //    Anything surprising about the procedure — a column that is nullable when it looks like it should
    //    not be, a result set that is empty rather than throwing, an ordering the UI depends on — goes in
    //    that comment. That surprise is the most valuable thing in this file.
    //
    // 4. GROUP BY FEATURE AREA under a `// ----- Area (where it is used) -----` banner, and keep the
    //    banners AND the methods inside them in THE SAME ORDER AS SqlData.cs. The two files are meant to
    //    be read side by side; a divergent order makes that impossible and nothing will warn you.
    //
    // 5. RETURN List<T>, T? OR A SCALAR. Never DataTable, never object, never dynamic. A row that may not
    //    exist returns T? and the caller decides what "not found" means. The whole point of this layer is
    //    that a mistyped column name becomes a compile-time or startup-time problem instead of a page a
    //    user hits at 4pm.
    //
    // 6. MODELS LIVE IN CRC.Data/Models/, one POCO per file, named for the data and not for the
    //    procedure. Reuse an existing model when a procedure genuinely returns the same shape; do NOT add
    //    a column alias to a .sql file to force a fit.
    //
    // -------------------------------------------------------------------------------------------------
    // 🔴 THE @User_ID RULE — the one thing in this migration that fails silently
    // -------------------------------------------------------------------------------------------------
    //
    // 24 procedures declare a parameter called @User_ID, and it means TWO DIFFERENT THINGS. The tell is
    // the default, and getting it wrong is a data-integrity bug that breaks no build and no page.
    //
    // ── `@User_ID INT = NULL` — THE ACTOR (19 procedures) ────────────────────────────────────────────
    //
    // "Who is performing this write", recorded on the dbo.AuditTrails row the procedure inserts. It is
    // NOT a business argument, so it NEVER APPEARS IN A METHOD SIGNATURE IN THIS FILE. SqlData supplies
    // it from DatabaseHelper.CurrentUserId and puts a comment on every one of the 19 calls saying so.
    //
    //     spBranch_Insert              spPatientAppointment_Insert       spStaff_Insert
    //     spBranch_Update              spPatientAppointment_Update       spStaff_Update
    //     spBranch_Delete              spPatientAppointment_Delete       spStaff_Delete
    //     spPatientBasic_Insert        spPatientAppointment_UpdateStatus spStaffDocument_Insert
    //     spPatientBasic_Update        spPatientDocument_Insert          spStaffDocument_Delete
    //     spPatient_DeleteCascade      spPatientDocument_Delete          spStaffSlots_CreateRange
    //                                                                    spStaffSlots_Delete
    //
    // DatabaseHelper used to add this parameter by itself: before running any command it queried
    // sys.parameters, asked "does this procedure declare @User_ID?", and appended the claim value if so.
    // Dapper has no equivalent hook — it sends the anonymous object's properties and nothing else. And
    // because all 19 declare a DEFAULT, a forgotten parameter does not throw. It writes
    // AuditTrails.User_Id = 0, and nobody finds out until somebody needs the audit trail.
    //
    // ── `@User_ID INT` (no default) — A TARGET USER ROW (5 procedures) ───────────────────────────────
    //
    // "Which user row this operates ON". This IS an ordinary argument and DOES appear in the method
    // signature; the caller decides its value and it has nothing to do with who is logged in.
    //
    //     spUsers_GetById        spUsers_Unlock          spUsers_UpdatePassword
    //     spUsers_ResetFailedLogins                      spUsers_UpdateLastLogin
    //
    // spUsers_Unlock is the one to stare at. Its @User_ID is THE LOCKED-OUT ACCOUNT BEING UNLOCKED — a
    // SUPERUSER unlocking somebody else. Filling it from the caller's claim would unlock the
    // administrator's own account, leave the locked-out user locked, and report success.
    //
    // That asymmetry is exactly why the injection is not hidden inside a generic helper: each call site
    // in SqlData makes the choice explicitly, in the open, where a reader can see which kind it is.
    //
    // (spPatientDocument_GetById LOOKS like it declares @User_ID and does not — its header comment says
    // "Read-only: no @User_ID and no audit row", and a naive grep matches the comment. Trust the two
    // lists above, not a grep.)
    //
    // See DapperLayerPlan.md's "@User_ID" section and CoreFlow.md §0.
    // =================================================================================================
    public interface IDatabaseData
    {
        // Methods are added here by Prompts 1-9, one feature area at a time, under the banners described
        // in rule 4. Prompt 0 deliberately leaves this empty: the layer is registered and wired before a
        // single call moves into it, so that the move of DatabaseHelper and the DI change can be proved
        // harmless on their own.
    }
}
