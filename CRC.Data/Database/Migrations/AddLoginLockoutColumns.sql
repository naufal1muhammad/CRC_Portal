-- Adds login lockout / failed-login tracking columns to dbo.Users.
-- Safe to run multiple times.

IF COL_LENGTH('dbo.Users', 'Failed_Login_Count') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD [Failed_Login_Count] INT NOT NULL CONSTRAINT [DF_Users_Failed_Login_Count] DEFAULT (0);
END
GO

IF COL_LENGTH('dbo.Users', 'Last_Failed_Login_At') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD [Last_Failed_Login_At] DATETIME NULL;
END
GO

IF COL_LENGTH('dbo.Users', 'Lockout_End_Utc') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD [Lockout_End_Utc] DATETIME NULL;
END
GO
