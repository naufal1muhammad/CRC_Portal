namespace CRC.Data.Models
{
    // What spPatient_DeleteCascade answers: the blob keys of every document belonging to the patient it
    // just erased, so the caller can remove those objects from the private container.
    //
    // 🔴 THE PROCEDURE EMITS EXACTLY ONE RESULT SET, AND DapperLayerPlan.md's Prompt 5 SAYS IT EMITS TWO.
    // The plan describes "a summary AND a set of BlobName keys", by analogy with spStaff_Delete (§5.4),
    // which really does return `{Status, Message}` then `{BlobName}`. This procedure has no summary grid:
    // read spPatient_DeleteCascade.sql and there is a single statement-level SELECT, the last line of the
    // body, `SELECT [BlobName] FROM @DocBlobs;`. Checked three ways and all three agree — the .sql file,
    // the deployed definition in CRC_DB, and PatientController.DeletePatient, which has only ever read
    // ds.Tables[0] and indexed it by "BlobName". So this is a QueryAsync, not a QueryMultipleAsync, and
    // this type carries one list rather than a status beside it.
    //
    // It stays a named type rather than a bare List<string> for two reasons. It is a RESULT, not a
    // collection — the caller's next move is "delete these from storage", which is a different act from
    // "here are some rows". And if the procedure ever does gain the summary grid the plan expected, the
    // property lands here and no signature changes.
    //
    // ── WHAT "NO KEYS" MEANS, AND WHAT IT DOES NOT ─────────────────────────────────────────────────────
    //
    // An empty list means the patient owned no documents. IT DOES NOT MEAN THE DELETE DID NOT HAPPEN, and
    // nothing in this result says whether it did: spPatient_DeleteCascade takes any Patient_ID, deletes
    // whatever matches, and returns normally when nothing does — no status, no row count, no RAISERROR.
    // A delete against an unknown id is a silent success, exactly like spBranch_Delete (§5.2). The only
    // trace is the dbo.AuditTrails row, which the procedure writes ONLY when a PatientBasic row actually
    // went (`IF @RowsAffected > 0`).
    //
    // ── WHY THE KEYS HAVE TO TRAVEL OUT AT ALL ─────────────────────────────────────────────────────────
    //
    // Storage takes no part in a database transaction. The procedure captures these keys into a table
    // variable BEFORE it deletes the dbo.PatientDocument rows, because after the rows are gone there is
    // nothing left to read them from — and CRC.Data cannot delete a blob itself, since IDocumentStorage is
    // a CRC.Web service and this project has no reference to CRC.Web and must not gain one (CoreFlow.md
    // §6.6). So the keys come back here and PatientController removes the objects, best effort, per key.
    // Leaving them would not merely waste storage: it would RETAIN PATIENT DATA AFTER THE PATIENT RECORD
    // ITSELF HAS BEEN DELETED.
    public class PatientDeleteResult
    {
        // The container keys of the deleted patient's documents. The procedure already excludes NULL and
        // blank ones when it captures them; SqlData filters again so the element type is honest.
        public List<string> BlobNames { get; set; } = new();
    }
}
