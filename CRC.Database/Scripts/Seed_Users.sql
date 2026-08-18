/*
    Post-deployment seed for dbo.Users — TWO rows: the bootstrap SUPERUSER, and the
    AGENT_SERVICE account the Agent API performs its writes as.

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
      No human account beyond this one, and no dbo.Staff rows. (Section 2 below adds a
      MACHINE account, which is a different thing and never logs in.) Staff_ID is
      required only for User_Type 3 (STAFF) — spUsers_Register enforces that and
      validates the id against dbo.Staff — so this SUPERUSER row carries Staff_ID = NULL
      and depends on nothing outside dbo.Users. Branches, staff and everything
      operational are created through the portal's own admin screens after this first
      login.

    COLUMNS LEFT TO THEIR DEFAULTS
      Created_At and Last_Login (DEFAULT GETUTCDATE()) and Failed_Login_Count (DEFAULT 0)
      are not passed, so the account's timestamps reflect when the database was actually
      published. User_ID is left to IDENTITY; nothing in the codebase depends on the
      superuser holding a particular id — and nothing depends on the agent account's
      either. Both statements below rely on that.

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

/*
    2. THE AGENT SERVICE ACCOUNT — Username 'AGENT_SERVICE'.

    WHAT THIS ACCOUNT IS FOR
      It is the AUDIT ACTOR for every write the Agent API makes, and nothing else.
      It has no screen, no session and no purpose outside dbo.AuditTrails.

      The Agent API (CRC.Api) is called by an external WhatsApp agent with an API
      key in a header. An API-key request arrives WITH NO COOKIE, so it has no
      ClaimsPrincipal, so DatabaseHelper.CurrentUserId is NULL — and the 19
      audit-actor procedures write ISNULL(@User_ID, 0) (CoreFlow.md 0.1). Without
      this row, every appointment the agent books would be recorded as
      AuditTrails.User_Id = 0 — nobody — with no error and no failed request. A
      corrupt audit trail on a clinical system, arrived at silently.

      So the API's request filter reads this row through
      spAgentUsers_GetServiceAccount and builds a principal from it BEFORE any
      action runs. Its User_ID is the actor every agent write is attributed to.

      LOOKED UP BY USERNAME, NEVER BY ID. User_ID is INT IDENTITY, so this row's id
      here and its id on Azure SQL are different numbers; a configured id would be
      wrong on one of them, and a wrong id does not throw — it silently audits the
      agent's writes as a different, real user. [Username] carries UNIQUE INDEX
      IX_Users_Username, so the lookup is an index seek and cannot be ambiguous.
      Nothing in the solution stores this account's User_ID.

    IT NEVER LOGS IN
      Nobody is meant to sign in as AGENT_SERVICE, and nobody can. Its password is
      a RANDOM SECRET THAT WAS GENERATED ONCE AND THROWN AWAY — never printed,
      never recorded, never committed. Only the hash below survives, and PBKDF2 is
      one-way, so the plaintext is NOT RECOVERABLE BY ANYONE, including whoever
      created this row.

      🔴 CONTRAST THE SUPERUSER ROW A FEW LINES ABOVE. That password is PUBLIC — it
      is printed in this file and in SEEDING.md, both in source control, and the
      account is meant to be logged into and then changed. This one is the exact
      opposite: no usable password, on purpose, permanently. Do not "fix" that by
      setting a known password here, and do not copy the SUPERUSER hash into this
      row — that would hand a machine account the publicly known password
      'ChangeMe!123'.

      The hash IS a real, well-formed PBKDF2 hash in ASP.NET Core's V3 format,
      produced by the same Microsoft.AspNetCore.Identity PasswordHasher<string> the
      login path uses. That matters: an invented string would not be valid base64,
      and PasswordHasher throws FormatException on one — turning any login attempt
      against this account into a 500 instead of a clean "wrong password".

    User_Type 2 (ADMIN)
      The agent books appointments and reads patients; that is administrator work
      in nucentra's model (Program.cs: 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF). It is
      NOT User_Type 3 — STAFF requires a Staff_ID pointing at a real clinician, and
      this account is not a person. Staff_ID is therefore NULL.

    WHY IT IS SAFE TO RE-RUN
      Guarded on [Username], exactly like the SUPERUSER row. The post-deployment
      script runs on EVERY publish; once this row exists, a later publish sees it
      and inserts nothing. It NEVER re-seeds and never rewrites Password_Hash.

    🔴 DELETING THIS ROW BREAKS EVERY AGENT API CALL — BY DESIGN
      spAgentUsers_GetServiceAccount then returns no row, and the API answers 503
      and executes nothing. That is deliberate and is the only correct behaviour:
      the alternative is falling through to a null actor and auditing every write
      as user 0 forever. If the account is deleted, the next publish recreates it
      with a NEW random password nobody knows — which is fine, because nothing
      about this account depends on its password or on its id.
*/
-------------------------------------------------------------------------------
-- 2. The Agent API service actor — Username 'AGENT_SERVICE'.
--    Guarded on [Username]: inserted once, never re-seeded, never reset.
--    Password: a discarded random secret. This account cannot be logged into.
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Username] = 'AGENT_SERVICE')
    INSERT INTO [dbo].[Users] ([User_Name], [Username], [User_Email], [Password_Hash], [User_Type], [Staff_ID])
    VALUES
    (
        'AGENT SERVICE ACCOUNT',
        'AGENT_SERVICE',
        'agent@crc.local',
        'AQAAAAIAAYagAAAAEP37JPMknowdOr/JCZps7L0lMkX3tXtasZ4/ZPS/k819wOLZc6dpJCYvIOjLLg+LjQ==',  -- PBKDF2 hash of a random secret that was discarded; NOT recoverable
        2,      -- 2 = ADMIN (Program.cs: 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF)
        NULL    -- not a person; only User_Type 3 (STAFF) requires one — see spUsers_Register
    );
GO
