CREATE PROCEDURE [dbo].[spBranch_Delete]
    @Branch_ID VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[Branch]
    WHERE [Branch_ID] = @Branch_ID;
END;