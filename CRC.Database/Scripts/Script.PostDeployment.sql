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
    -------------------------------------------------------------------------------
*/

:r .\Seed_Lookups.sql
