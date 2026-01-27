CREATE TABLE [dbo].[Staff]
(
    [Staff_ID]          VARCHAR(100) NOT NULL,
    [Staff_Name]        VARCHAR(100) NOT NULL,
    [Staff_NRIC]        VARCHAR(100) NOT NULL,
    [Staff_BirthDate]   DATETIME NOT NULL,
    [Staff_Age]         INT NOT NULL,
    [Staff_Phone]       VARCHAR(100) NOT NULL,
    [Staff_Email]       VARCHAR(100) NOT NULL,
    [Staff_Gender]      VARCHAR(100) NOT NULL,
    [Staff_ResState]    VARCHAR(100) NOT NULL,
    [Staff_ResCity]     VARCHAR(100) NOT NULL,
    [Staff_ResPostcode] VARCHAR(100) NOT NULL,
    [Staff_AddLine1]    VARCHAR(MAX) NOT NULL,
    [Staff_AddLine2]    VARCHAR(MAX) NOT NULL,
    [Staff_Base]        VARCHAR(100) NOT NULL,
    [Staff_Type]        VARCHAR(100) NOT NULL,  -- stores StaffType_ID
    CONSTRAINT [PK_Staff] PRIMARY KEY ([Staff_ID])
);
GO
