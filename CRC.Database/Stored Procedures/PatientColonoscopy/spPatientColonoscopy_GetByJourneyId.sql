CREATE PROCEDURE dbo.spPatientColonoscopy_GetByJourneyId
    @PatientJourney_ID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        pj.PatientJourney_ID,
        pj.Patient_ID,
        pb.Patient_Name,
        pj.PjAppType_Name,
        pj.PatientJourney_Date,
        pj.Staff_ID,

        pc.PatientColonoscopy_ID,

        pc.ColonoscopyStatus,
        pc.ColonoscopyStatus_Details,
        pc.BowelPreparation,

        pc.Findings_Anus,
        pc.Findings_AnusDetails,
        pc.Findings_Rectum,
        pc.Findings_RectumDetails,
        pc.Findings_SigmoidColon,
        pc.Findings_SigmoidColonDetails,
        pc.Findings_DescendingColon,
        pc.Findings_DescendingColonDetails,
        pc.Findings_SplenicFlexure,
        pc.Findings_SplenicFlexureDetails,
        pc.Findings_TransverseColon,
        pc.Findings_TransverseColonDetails,
        pc.Findings_HepaticFlexure,
        pc.Findings_HepaticFlexureDetails,
        pc.Findings_AscendingColon,
        pc.Findings_AscendingColonDetails,
        pc.Findings_Caecum,
        pc.Findings_CaecumDetails,

        pc.HPE_Status,
        pc.HPE_Details,

        pc.Complications,
        pc.Complications_Details,

        pc.DischargePlan,

        pc.Medication_Details
    FROM dbo.PatientJourney pj
    INNER JOIN dbo.PatientBasic pb
        ON pb.Patient_ID = pj.Patient_ID
    INNER JOIN dbo.PatientColonoscopy pc
        ON pc.PatientJourney_ID = pj.PatientJourney_ID
    WHERE pj.PatientJourney_ID = @PatientJourney_ID;
END
GO