CREATE PROCEDURE [dbo].[spPatient_Insert]
    @Patient_ID       VARCHAR(100),
    @Patient_Name     VARCHAR(100),
    @Patient_NRIC     VARCHAR(100),
    @Patient_Phone    VARCHAR(50),
    @Patient_Email    VARCHAR(100),
    @Branch_ID        VARCHAR(100),
    @Branch_Name      VARCHAR(100),
    @Patient_Stage    VARCHAR(10),
    @Patient_Remarks  VARCHAR(MAX),
    @Appointment_Date DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[Patient] WHERE [Patient_ID] = @Patient_ID)
    BEGIN
        RAISERROR('Patient ID already exists.', 16, 1);
        RETURN;
    END

    INSERT INTO [dbo].[Patient] (
        [Patient_ID],
        [Patient_Name],
        [Patient_NRIC],
        [Patient_Phone],
        [Patient_Email],
        [Branch_ID],
        [Branch_Name],
        [Patient_Stage],
        [Patient_Remarks],
        [Appointment_Date]
    )
    VALUES (
        @Patient_ID,
        @Patient_Name,
        @Patient_NRIC,
        @Patient_Phone,
        @Patient_Email,
        @Branch_ID,
        @Branch_Name,
        @Patient_Stage,
        @Patient_Remarks,
        @Appointment_Date
    );
END;