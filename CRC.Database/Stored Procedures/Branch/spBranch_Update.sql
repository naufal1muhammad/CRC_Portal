CREATE PROCEDURE [dbo].[spBranch_Update]
    @Branch_ID         VARCHAR(100),
    @Branch_Name       VARCHAR(100),
    @Branch_Location   VARCHAR(100),
    @Branch_State      VARCHAR(100),
    @Branch_Status     BIT,
    @Organization_ID   VARCHAR(100),
    @Organization_Name VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    ---------------------------------
    -- 0) Capture old Branch_Name
    ---------------------------------
    DECLARE @OldBranch_Name VARCHAR(100);

    SELECT @OldBranch_Name = b.[Branch_Name]
    FROM [dbo].[Branch] b
    WHERE b.[Branch_ID] = @Branch_ID;

    ---------------------------------
    -- 1) Update main Branch
    ---------------------------------
    UPDATE [dbo].[Branch]
    SET
        [Branch_Name]       = @Branch_Name,
        [Branch_Location]   = @Branch_Location,
        [Branch_State]      = @Branch_State,
        [Branch_Status]     = @Branch_Status,
        [Organization_ID]   = @Organization_ID,
        [Organization_Name] = @Organization_Name
    WHERE [Branch_ID] = @Branch_ID;

    ---------------------------------
    -- 2) Cascade updates to children
    ---------------------------------

    -- PatientBasic: Branch_Name
    UPDATE pb
    SET
        pb.[Branch_Name] = @Branch_Name
    FROM [dbo].[PatientBasic] pb
    WHERE pb.[Branch_Name] = @OldBranch_Name;
END;
