CREATE TABLE [dbo].[Staff]
(
    [Staff_ID] VARCHAR(100) NOT NULL CONSTRAINT [PK_Staff] PRIMARY KEY,
    [Staff_Name] VARCHAR(100) NOT NULL,
    [Staff_NRIC] VARCHAR(100) NOT NULL,
    [Staff_Phone] VARCHAR(50) NOT NULL,
    [Staff_Email] VARCHAR(100) NOT NULL,
    [Branch_ID] VARCHAR(100) NOT NULL,
    [Branch_Name] VARCHAR(100) NOT NULL,
    [Staff_Type] INT NOT NULL -- 1=Doctor 2=Nurse 3=Counsellor 4=Clerk
);
GO