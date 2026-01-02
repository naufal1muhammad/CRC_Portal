CREATE TABLE [dbo].[Staff]
(
    [Staff_ID]    VARCHAR(100) NOT NULL,
    [Staff_Name]  VARCHAR(100) NOT NULL,
    [Staff_NRIC]  VARCHAR(100) NOT NULL,
    [Staff_Phone] VARCHAR(100) NOT NULL,
    [Staff_Email] VARCHAR(100) NOT NULL,
    [Branch_ID]   VARCHAR(100) NOT NULL,
    [Branch_Name] VARCHAR(100) NOT NULL,
    [Staff_Type]  VARCHAR(100) NOT NULL,  -- stores StaffType_ID
    CONSTRAINT [PK_Staff] PRIMARY KEY ([Staff_ID])
);
GO