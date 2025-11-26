CREATE TABLE [dbo].[Users]
(
    [User_ID] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY,
    [User_Name] VARCHAR(100) NOT NULL,
    [Username] VARCHAR(100) NOT NULL,
    [User_Email] VARCHAR(100) NOT NULL,
    [Password] VARCHAR(200) NOT NULL,
    [User_Type] INT NOT NULL -- 1 = Administrator
);
GO

CREATE UNIQUE INDEX [IX_Users_Username] ON [dbo].[Users]([Username]);