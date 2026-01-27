CREATE PROCEDURE [dbo].[spStaff_Update]
(
    @Staff_ID          VARCHAR(100),
    @Staff_Name        VARCHAR(100),
    @Staff_NRIC        VARCHAR(100),
    @Staff_BirthDate   DATETIME,
    @Staff_Age         INT,
    @Staff_Phone       VARCHAR(100),
    @Staff_Email       VARCHAR(100),
    @Staff_Gender      VARCHAR(100),
    @Staff_ResState    VARCHAR(100),
    @Staff_ResCity     VARCHAR(100),
    @Staff_ResPostcode VARCHAR(100),
    @Staff_AddLine1    VARCHAR(MAX),
    @Staff_AddLine2    VARCHAR(MAX),
    @Staff_Base        VARCHAR(100),
    @Staff_Type        VARCHAR(100)  -- still pass it, but UI will keep it fixed
)
AS
BEGIN
    SET NOCOUNT ON;

    ---------------------------------
    -- 1) Update main Staff
    ---------------------------------
    UPDATE [dbo].[Staff]
    SET
        [Staff_Name]        = @Staff_Name,
        [Staff_NRIC]        = @Staff_NRIC,
        [Staff_BirthDate]   = @Staff_BirthDate,
        [Staff_Age]         = @Staff_Age,
        [Staff_Phone]       = @Staff_Phone,
        [Staff_Email]       = @Staff_Email,
        [Staff_Gender]      = @Staff_Gender,
        [Staff_ResState]    = @Staff_ResState,
        [Staff_ResCity]     = @Staff_ResCity,
        [Staff_ResPostcode] = @Staff_ResPostcode,
        [Staff_AddLine1]    = @Staff_AddLine1,
        [Staff_AddLine2]    = @Staff_AddLine2,
        [Staff_Base]        = @Staff_Base,
        [Staff_Type]        = @Staff_Type
    WHERE [Staff_ID] = @Staff_ID;

    ---------------------------------
    -- 2) Cascade updates to children
    ---------------------------------

    -- PatientAppointment: Staff_ID, Staff_Name
    UPDATE pa
    SET
        pa.[Staff_Name] = @Staff_Name
    FROM [dbo].[PatientAppointment] pa
    WHERE pa.[Staff_ID] = @Staff_ID;

    -- PatientJourneyAudit: Staff_ID, Staff_Name
    UPDATE pja
    SET
        pja.[Staff_Name] = @Staff_Name
    FROM [dbo].[PatientJourneyAudit] pja
    WHERE pja.[Staff_ID] = @Staff_ID;

    -- StaffDocument: Staff_ID, Staff_Name
    UPDATE sd
    SET
        sd.[Staff_Name] = @Staff_Name
    FROM [dbo].[StaffDocument] sd
    WHERE sd.[Staff_ID] = @Staff_ID;
END;
GO
