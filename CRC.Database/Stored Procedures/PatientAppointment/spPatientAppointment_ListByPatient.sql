CREATE PROCEDURE [dbo].[spPatientAppointment_ListByPatient]
(
    @Patient_ID VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pa.[PatientAppointment_ID],
        pa.[Patient_ID],
        pa.[PatientAppointment_Date],
        CONVERT(VARCHAR(5), pa.[PatientAppointment_StartTime], 108) AS [PatientAppointment_StartTime],
        CONVERT(VARCHAR(5), pa.[PatientAppointment_EndTime], 108)   AS [PatientAppointment_EndTime],
        pa.[PatientAppointment_Status],
        pa.[Staff_ID],
        s.[Staff_Name],
        pa.[PjAppType_ID],
        t.[PjAppType_Name],
        pa.[Branch_ID],
        b.[Branch_Name]
    FROM [dbo].[PatientAppointment] pa
    LEFT JOIN [dbo].[Staff] s
        ON pa.[Staff_ID] = s.[Staff_ID]
    LEFT JOIN [dbo].[LU_PJ_APP_TYPE] t
        ON pa.[PjAppType_ID] = t.[PjAppType_ID]
    LEFT JOIN [dbo].[Branch] b
        ON pa.[Branch_ID] = b.[Branch_ID]
    WHERE pa.[Patient_ID] = @Patient_ID
    ORDER BY
        pa.[PatientAppointment_Date] DESC,
        pa.[PatientAppointment_StartTime] DESC,
        pa.[PatientAppointment_ID] DESC;
END;
GO