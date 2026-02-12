CREATE PROCEDURE dbo.spPatientJourney_AuditsByPatient
(
    @Patient_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        a.PatientJourneyAudit_ID,
        a.PatientJourney_ID,
        a.Audit_Action,
        a.Audit_At,
        a.Staff_ID,
        s.Staff_Name,
        a.Audit_Note
    FROM dbo.PatientJourneyAudit a
    INNER JOIN dbo.PatientJourney pj
        ON pj.PatientJourney_ID = a.PatientJourney_ID
    LEFT JOIN dbo.Staff s
        ON s.Staff_ID = a.Staff_ID
    WHERE pj.Patient_ID = @Patient_ID
    ORDER BY
        a.PatientJourney_ID ASC,
        a.Audit_At ASC,
        a.PatientJourneyAudit_ID ASC;
END
GO