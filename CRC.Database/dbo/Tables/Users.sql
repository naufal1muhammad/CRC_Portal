CREATE TABLE [dbo].[Users]
(
    [User_ID] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY,
    [User_Name] VARCHAR(100) NOT NULL,
    [Username] VARCHAR(100) NOT NULL,
    [User_Email] VARCHAR(100) NOT NULL,
    [Password_Hash] VARCHAR(500) NOT NULL,
    [User_Type] INT NOT NULL,
    [Staff_ID] VARCHAR(100) NULL,

    [Created_At] DATETIME NOT NULL CONSTRAINT [DF_Users_Created_At] DEFAULT (GETUTCDATE()),
    [Last_Login] DATETIME NOT NULL CONSTRAINT [DF_Users_Last_Login] DEFAULT (GETUTCDATE()),

    [Failed_Login_Count]   INT       NOT NULL CONSTRAINT [DF_Users_Failed_Login_Count] DEFAULT (0),
    [Last_Failed_Login_At] DATETIME  NULL,
    [Lockout_End_Utc]      DATETIME  NULL
);
GO

CREATE UNIQUE INDEX [IX_Users_Username] ON [dbo].[Users]([Username]);
GO
