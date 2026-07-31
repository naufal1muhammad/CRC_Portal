/*
    Post-Deployment Script
    -------------------------------------------------------------------------------
    Runs once after the schema is deployed, on EVERY publish — not just the first.
    SSDT allows only ONE post-deployment script per project, so this file composes
    the individual idempotent seed files via SQLCMD ":r" includes. Each seed is
    itself idempotent (guarded by WHERE NOT EXISTS / IF NOT EXISTS), so publishing
    again over an already-seeded database inserts nothing and fails nothing.

    The ":r" paths are relative to THIS file's folder, and SSDT inlines the included
    files at BUILD time — the deployed database never needs the seed files on disk.
    A ":r" pointing at a file that does not exist breaks the build.

    The seed files are carried in the project as <None> items: source-controlled,
    but not compiled as schema objects.

    ORDER: lookups -> location -> users. That is a convention here, not a
    requirement — none of the three depends on another. dbo.LU_LOCATION has no
    foreign key to the other lookup tables, and dbo.Users has no foreign keys at
    all (Staff_ID is a plain VARCHAR column, and only User_Type 3 users carry one).
    Smallest first simply makes a publish log read sensibly, and the account that
    can log in is created last, once the data it will look at is in place.
    -------------------------------------------------------------------------------
*/

:r .\Seed_Lookups.sql
:r .\Seed_Location.sql
:r .\Seed_Users.sql
