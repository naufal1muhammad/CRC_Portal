namespace CRC.Data.Models
{
    // Grid 4 of spStaff_GetPerformance: one row per kind of anomaly this clinician has found, with how many
    // DISTINCT PATIENTS it was found in.
    //
    // Where it comes from is worth knowing before anyone tries to index it: dbo.PatientColonoscopy stores
    // its per-segment findings as NINE separate NVARCHAR JSON columns — anus, rectum, sigmoid colon,
    // descending colon, splenic flexure, transverse colon, hepatic flexure, ascending colon, caecum. The
    // procedure CROSS APPLYs all nine into one column, keeps the ones where ISJSON() = 1, and pulls
    // JSON_VALUE(…, '$.TypeOfAnomaly') out of each. So a finding recorded in three segments of one patient
    // is one row of PatientCount = 1, and a JSON document without a TypeOfAnomaly key contributes nothing.
    //
    // PatientCount is COUNT(DISTINCT Patient_ID), which is what makes this grid answer a different question
    // from grid 3's COUNT(*) despite the identical {string, int} shape. See StaffPerformanceComplication.
    public class StaffPerformanceAnomaly
    {
        public string? TypeOfAnomaly { get; set; }
        public int? PatientCount { get; set; }
    }
}
