/*
    Post-deployment seed for dbo.Users — ONE row, the bootstrap SUPERUSER.

    WHAT THIS FILE IS FOR
      A freshly published database has no accounts at all, so nobody can log in and
      nothing else in the portal can be reached. This file creates the single account
      that opens the door on a brand-new installation.

    THE CREDENTIALS

          Username : SUPERUSER
          Password : ChangeMe!123

      That password satisfies the portal's own policy in CRC.Web/appsettings.json
      ("Account" > "Password": RequiredLength 12, RequireUppercase, RequireLowercase,
      RequireDigit, RequireNonAlphanumeric, RequiredUniqueChars 2), so it can be typed
      into the login form as-is and Change Password will accept a replacement of the
      same strength.

      Only the PBKDF2 hash below is stored. The plaintext CANNOT be recovered from it —
      it is written out here purely so the owner of a new installation knows what to
      type the first time.

    *** CHANGE IT IMMEDIATELY AFTER THE FIRST LOGIN, via Account > Change Password. ***

      dbo.Users has no MustChangePassword column, so NOTHING IN THE APP FORCES THIS.
      There is no first-login redirect, no expiry, no reminder: the account keeps this
      password until a human changes it. A published database is reachable by anyone
      who can reach the site, and this password is public in source control — it is
      printed a few lines above, in a file that ships with the project. Until it is
      changed, the installation has a publicly known superuser.

    THE HASH
      Produced by the same hasher the login path uses: Microsoft.AspNetCore.Identity's
      PasswordHasher<string> (see the _hasher field in CRC.Web/Controllers/
      AccountController.cs, used by both Login and ChangePassword). It is the standard
      ASP.NET Core V3 format — a salted PBKDF2-HMAC-SHA512 hash, 84 base64 characters
      beginning with "AQAAAA" — so it verifies on the very first login attempt with no
      code change anywhere.

      PasswordHasher<TUser> IGNORES its user argument: the hash covers the password and
      its random salt and nothing else. The value below is therefore not coupled to the
      username 'SUPERUSER' and stays valid if the display name or e-mail is edited later.

    WHY IT IS SAFE TO RE-RUN
      SSDT runs the post-deployment script on EVERY publish. The insert is guarded by
      IF NOT EXISTS on [Username], which carries the UNIQUE index IX_Users_Username, so
      a publish against a database that already holds this account inserts nothing.

      That guard is also what protects a CHANGED password: once the row exists, a later
      publish sees it and skips — it NEVER resets Password_Hash back to the seeded value.
      Re-publishing a live installation cannot undo the password change above.

      The corollary: if the SUPERUSER account is ever deleted outright, the next publish
      recreates it with the seeded password again.

    WHAT IS NOT SEEDED
      No second account, and no dbo.Staff rows. Staff_ID is required only for User_Type 3
      (STAFF) — spUsers_Register enforces that and validates the id against dbo.Staff —
      so this SUPERUSER row carries Staff_ID = NULL and depends on nothing outside
      dbo.Users. Branches, staff and everything operational are created through the
      portal's own admin screens after this first login.

    COLUMNS LEFT TO THEIR DEFAULTS
      Created_At and Last_Login (DEFAULT GETUTCDATE()) and Failed_Login_Count (DEFAULT 0)
      are not passed, so the account's timestamps reflect when the database was actually
      published. User_ID is left to IDENTITY; nothing in the codebase depends on the
      superuser holding a particular id.

      dbo.Users columns are VARCHAR, not NVARCHAR, and every value here is plain ASCII,
      so the literals are ordinary '...' rather than N'...'.
*/
SET NOCOUNT ON;

-------------------------------------------------------------------------------
-- 1. The bootstrap SUPERUSER — Username 'SUPERUSER', password 'ChangeMe!123'.
--    Guarded on [Username]: inserted once, never re-seeded, never reset.
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Username] = 'SUPERUSER')
    INSERT INTO [dbo].[Users] ([User_Name], [Username], [User_Email], [Password_Hash], [User_Type], [Staff_ID])
    VALUES
    (
        'SYSTEM SUPERUSER',
        'SUPERUSER',
        'superuser@crc.local',
        'AQAAAAIAAYagAAAAEJoaTj2x3FE2iVcO057SBNjNaNTarZWmScxCIaYxji7GB9C7E87slLEswCCYbroD4g==',  -- PBKDF2 hash of 'ChangeMe!123'
        1,      -- 1 = SUPERUSER (Program.cs: 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF)
        NULL    -- only User_Type 3 (STAFF) requires one — see spUsers_Register
    );
GO
