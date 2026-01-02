CREATE PROCEDURE dbo.spPatientJourney_TimelineByPatient
(
    @Patient_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pj.PatientJourney_ID,
        pj.Patient_ID,
        pj.Patient_Name,
        pj.PjAppType_Name,
        pj.PatientJourney_Date,

        ca.Audit_At     AS CreatedAt,
        ca.Staff_ID     AS CreatedByStaffId,
        ca.Staff_Name   AS CreatedByStaffName,

        ua.Audit_At     AS UpdatedAt,
        ua.Staff_ID     AS UpdatedByStaffId,
        ua.Staff_Name   AS UpdatedByStaffName
    FROM dbo.PatientJourney pj
    OUTER APPLY
    (
        SELECT TOP 1
            a.Audit_At,
            a.Staff_ID,
            a.Staff_Name
        FROM dbo.PatientJourneyAudit a
        WHERE a.PatientJourney_ID = pj.PatientJourney_ID
          AND a.Audit_Action = 'CREATED'
        ORDER BY a.Audit_At ASC
    ) ca
    OUTER APPLY
    (
        SELECT TOP 1
            a.Audit_At,
            a.Staff_ID,
            a.Staff_Name
        FROM dbo.PatientJourneyAudit a
        WHERE a.PatientJourney_ID = pj.PatientJourney_ID
          AND a.Audit_Action IN ('UPDATED','EDITED')
        ORDER BY a.Audit_At DESC
    ) ua
    WHERE pj.Patient_ID = @Patient_ID
    ORDER BY
        pj.PatientJourney_Date ASC,
        pj.PatientJourney_ID ASC;
END
GO