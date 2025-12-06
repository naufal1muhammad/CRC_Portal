CREATE PROCEDURE [dbo].[spPatientAppointment_Insert]
(
    @Patient_ID                VARCHAR(100),
    @PjAppType_Name            VARCHAR(100),
    @PatientAppointment_Date   DATETIME,
    @PatientAppointment_Status VARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE
        @Patient_Name  VARCHAR(100),
        @Patient_Email VARCHAR(100),
        @Patient_Phone VARCHAR(100);

    SELECT
        @Patient_Name  = [Patient_Name],
        @Patient_Email = [Patient_Email],
        @Patient_Phone = [Patient_Phone]
    FROM [dbo].[PatientBasic]
    WHERE [Patient_ID] = @Patient_ID;

    IF @Patient_Name IS NULL
    BEGIN
        RAISERROR('Patient not found for the given Patient_ID.', 11, 1);
        RETURN;
    END

    INSERT INTO [dbo].[PatientAppointment]
    (
        [Patient_ID],
        [Patient_Name],
        [Patient_Email],
        [Patient_Phone],
        [PjAppType_Name],
        [PatientAppointment_Date],
        [PatientAppointment_Status]
    )
    VALUES
    (
        @Patient_ID,
        @Patient_Name,
        @Patient_Email,
        @Patient_Phone,
        @PjAppType_Name,
        @PatientAppointment_Date,
        @PatientAppointment_Status
    );
END;
GO