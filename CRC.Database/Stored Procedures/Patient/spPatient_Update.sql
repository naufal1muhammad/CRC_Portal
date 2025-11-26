CREATE PROCEDURE [dbo].[spPatient_Update]
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

    IF NOT EXISTS (SELECT 1 FROM [dbo].[Patient] WHERE [Patient_ID] = @Patient_ID)
    BEGIN
        RAISERROR('Patient not found.', 16, 1);
        RETURN;
    END

    UPDATE [dbo].[Patient]
    SET
        [Patient_Name] = @Patient_Name,
        [Patient_NRIC] = @Patient_NRIC,
        [Patient_Phone] = @Patient_Phone,
        [Patient_Email] = @Patient_Email,
        [Branch_ID] = @Branch_ID,
        [Branch_Name] = @Branch_Name,
        [Patient_Stage] = @Patient_Stage,
        [Patient_Remarks] = @Patient_Remarks,
        [Appointment_Date] = @Appointment_Date
    WHERE [Patient_ID] = @Patient_ID;
END;