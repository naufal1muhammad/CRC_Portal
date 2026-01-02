CREATE PROCEDURE dbo.spPatientJourney_GetById
(
    @PatientJourney_ID INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        pj.PatientJourney_ID,
        pj.Patient_ID,
        pj.Patient_Name,
        pj.PjAppType_Name,
        pj.PatientJourney_Date,
        pj.Staff_ID,

        pj.Created_At,
        pj.Updated_At,
        pj.CreatedBy_Staff_ID,
        pj.UpdatedBy_Staff_ID
    FROM dbo.PatientJourney pj
    WHERE pj.PatientJourney_ID = @PatientJourney_ID;
END
GO