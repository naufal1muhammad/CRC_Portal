/*
    Post-deployment seed for the ELEVEN small LU_* lookup tables — 77 rows in total.

    These are the lists behind the portal's dropdowns: race, religion, marital status,
    occupation, referral source and organisation on a patient record; the patient and
    staff document catalogues; the patient-journey appointment types; the discharge
    outcomes; and the staff types.

    WHERE THE VALUES CAME FROM
      Transcribed verbatim from the reference CRC_DB installation — ids, leading zeros
      and upper-casing included. They are the product's SHIPPED DEFAULTS, not one site's
      data, so every fresh publish should carry them. (Note that LU_PATDOCUMENTTYPE '03'
      is 'iFOBT RESULTS' with a deliberate lowercase leading i.)

    SHAPE
      Every one of these tables is a two-column VARCHAR(100) (_ID, _Name) table with a
      single-column primary key and no foreign keys between them, so the order of the
      sections below is alphabetical for readability only — nothing here depends on it.
      The columns are VARCHAR, not NVARCHAR, and every value is plain ASCII, so the
      literals are ordinary '...' rather than N'...'.
      The ids are VARCHAR keys, not numbers: '01' is not 1, and the leading zero is part
      of the value stored in dbo.PatientBasic, dbo.Staff and friends.

    WHY IT IS SAFE TO RE-RUN
      SSDT runs the post-deployment script on EVERY publish. Every block here is guarded
      by WHERE NOT EXISTS on the primary key, so a publish against a database that
      already holds these rows is a no-op. Nothing is ever truncated, deleted or updated.

    ADDING A VALUE LATER
      A publish only ever INSERTS rows that are missing; it never updates or deletes an
      existing one. So adding a lookup value to a live installation means BOTH adding a
      line here (so future publishes carry it) AND inserting it into the live database by
      hand. Likewise, correcting the spelling of a name that is already in a live
      database has to be done by hand — changing it here alone will not change it there.
*/
SET NOCOUNT ON;

-------------------------------------------------------------------------------
-- 1. LU_DISCHARGETYPE — the outcome a patient is discharged with. Also drives the
--    per-outcome document rules in dbo.PatientDocumentSettings.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT '01', 'NORMAL'
    UNION ALL SELECT '02', 'BENIGN POLYPS'
    UNION ALL SELECT '03', 'PRECANCEROUS POLYPS'
    UNION ALL SELECT '04', 'CANCER'
)
INSERT INTO [dbo].[LU_DISCHARGETYPE] ([DischargeType_ID], [DischargeType_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_DISCHARGETYPE] x WHERE x.[DischargeType_ID] = s.[Id]);

-------------------------------------------------------------------------------
-- 2. LU_MARITALSTATUS — marital status on a patient record.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT '01', 'SINGLE'
    UNION ALL SELECT '02', 'MARRIED'
    UNION ALL SELECT '03', 'DIVORCED'
)
INSERT INTO [dbo].[LU_MARITALSTATUS] ([MaritalStatus_ID], [MaritalStatus_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_MARITALSTATUS] x WHERE x.[MaritalStatus_ID] = s.[Id]);

-------------------------------------------------------------------------------
-- 3. LU_OCCUPATION — broad occupation bands on a patient record.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT '01', 'TECHNOLOGY / FINANCE'
    UNION ALL SELECT '02', 'MANUFACTURING / CONSTRUCTION'
    UNION ALL SELECT '03', 'HEALTHCARE / EDUCATION'
    UNION ALL SELECT '04', 'RETAIL / SERVICE INDUSTRY'
    UNION ALL SELECT '05', 'AGRICULTURE / MINING / UTILITIES'
    UNION ALL SELECT '06', 'GOVERNMENT / MILITARY'
    UNION ALL SELECT '07', 'UNEMPLOYED / STUDENT / RETIRED'
    UNION ALL SELECT '08', 'OTHERS'
)
INSERT INTO [dbo].[LU_OCCUPATION] ([Occupation_ID], [Occupation_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_OCCUPATION] x WHERE x.[Occupation_ID] = s.[Id]);

-------------------------------------------------------------------------------
-- 4. LU_ORGANIZATION — the referring organisation on a patient record.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT '01', 'NATIONAL CANCER SOCIETY MALAYSIA'
    UNION ALL SELECT '02', 'MINISTRY OF HEALTH'
    UNION ALL SELECT '03', 'GENERAL PRACTITIONER CLINIC'
    UNION ALL SELECT '04', 'NON-GOVERNMENTAL ORGANIZATION'
    UNION ALL SELECT '05', 'PRIVATE HOSPITAL'
    UNION ALL SELECT '06', 'SELF OWNED'
)
INSERT INTO [dbo].[LU_ORGANIZATION] ([Organization_ID], [Organization_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_ORGANIZATION] x WHERE x.[Organization_ID] = s.[Id]);

-------------------------------------------------------------------------------
-- 5. LU_PATDOCUMENTTYPE — the patient document catalogue: what may be uploaded
--    against a patient, and what dbo.PatientDocumentSettings can require per
--    discharge type.
--    '03' is 'iFOBT RESULTS' — the lowercase leading i is deliberate.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT '01', 'PERSONAL IDENTIFICATION'
    UNION ALL SELECT '02', 'REFERRAL LETTER (IN)'
    UNION ALL SELECT '03', 'iFOBT RESULTS'
    UNION ALL SELECT '04', 'PDPA FORM'
    UNION ALL SELECT '05', 'HISTORY AND EXAMINATION FORM'
    UNION ALL SELECT '06', 'COLONOSCOPY CONSENT FORM'
    UNION ALL SELECT '07', 'SEDATION CONSENT FORM'
    UNION ALL SELECT '08', 'COLONOSCOPY FINDING FORM'
    UNION ALL SELECT '09', 'DISCHARGE SUMMARY'
    UNION ALL SELECT '10', 'HISTOPATHOLOGICAL EXAMINATION FORM'
    UNION ALL SELECT '11', 'PATIENT BILL'
    UNION ALL SELECT '12', 'ADVISE FORM'
    UNION ALL SELECT '13', 'REFERRAL LETTER (OUT)'
)
INSERT INTO [dbo].[LU_PATDOCUMENTTYPE] ([PatientDocumentType_ID], [PatientDocumentType_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_PATDOCUMENTTYPE] x WHERE x.[PatientDocumentType_ID] = s.[Id]);

-------------------------------------------------------------------------------
-- 6. LU_PJ_APP_TYPE — the patient-journey appointment types, in journey order.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT '01', 'PATIENT ASSESSMENT'
    UNION ALL SELECT '02', 'COLONOSCOPY'
    UNION ALL SELECT '03', 'FOLLOW UP'
    UNION ALL SELECT '04', 'SURVEILLANCE'
)
INSERT INTO [dbo].[LU_PJ_APP_TYPE] ([PjAppType_ID], [PjAppType_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_PJ_APP_TYPE] x WHERE x.[PjAppType_ID] = s.[Id]);

-------------------------------------------------------------------------------
-- 7. LU_RACE — race on a patient record. Also the grouping behind the dashboard's
--    patients-by-race chart.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT '01', 'MALAY'
    UNION ALL SELECT '02', 'CHINESE'
    UNION ALL SELECT '03', 'INDIAN'
    UNION ALL SELECT '04', 'IBAN'
    UNION ALL SELECT '05', 'BIDAYUH'
    UNION ALL SELECT '06', 'MELANAU'
    UNION ALL SELECT '07', 'KADAZAN-DUSUN'
    UNION ALL SELECT '08', 'BAJAU'
    UNION ALL SELECT '09', 'MURUT'
    UNION ALL SELECT '10', 'ORANG ASLI'
    UNION ALL SELECT '11', 'OTHERS'
)
INSERT INTO [dbo].[LU_RACE] ([Race_ID], [Race_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_RACE] x WHERE x.[Race_ID] = s.[Id]);

-------------------------------------------------------------------------------
-- 8. LU_RELIGION — religion on a patient record.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT '01', 'ISLAM'
    UNION ALL SELECT '02', 'HINDU'
    UNION ALL SELECT '03', 'CHRISTIAN'
    UNION ALL SELECT '04', 'BUDDHIST'
    UNION ALL SELECT '05', 'TAOISM'
    UNION ALL SELECT '06', 'OTHERS'
)
INSERT INTO [dbo].[LU_RELIGION] ([Religion_ID], [Religion_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_RELIGION] x WHERE x.[Religion_ID] = s.[Id]);

-------------------------------------------------------------------------------
-- 9. LU_SOURCE — how the patient reached the centre.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT '01', 'SELF-REFERRED / WALK-IN'
    UNION ALL SELECT '02', 'GP / PRIVATE CLINIC'
    UNION ALL SELECT '03', 'PRIVATE HOSPITAL REFERRAL'
    UNION ALL SELECT '04', 'GOVERNMENT HOSPITAL REFERRAL'
    UNION ALL SELECT '05', 'MANAGED CARE ORGANIZATION'
    UNION ALL SELECT '06', 'COMPANY / CORPORATE REFERRAL'
    UNION ALL SELECT '07', 'INTERNAL REFERRAL'
    UNION ALL SELECT '08', 'ONLINE SEARCH / SOCIAL MEDIA'
    UNION ALL SELECT '09', 'OTHERS'
)
INSERT INTO [dbo].[LU_SOURCE] ([Source_ID], [Source_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_SOURCE] x WHERE x.[Source_ID] = s.[Id]);

-------------------------------------------------------------------------------
-- 10. LU_STAFFDOCUMENTTYPE — the staff document catalogue: what may be uploaded
--     against a staff member, and what dbo.StaffDocumentSettings can require per
--     staff type.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT '01', 'CV / RESUME'
    UNION ALL SELECT '02', 'BASIC DEGREE CERTIFICATE'
    UNION ALL SELECT '03', 'HIGHER PROFESSIONAL QUALIFICATION'
    UNION ALL SELECT '04', 'MMC REGISTRATION CERTIFICATE'
    UNION ALL SELECT '05', 'NSR SPECIALIST REGISTRATION CERTIFICATE'
    UNION ALL SELECT '06', 'LATEST ANNUAL PRACTICING CERTIFICATE'
    UNION ALL SELECT '07', 'MALPRACTICE INDEMNITY MEMBERSHIP'
    UNION ALL SELECT '08', 'PERSONAL IDENTIFICATION'
)
INSERT INTO [dbo].[LU_STAFFDOCUMENTTYPE] ([StaffDocumentType_ID], [StaffDocumentType_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_STAFFDOCUMENTTYPE] x WHERE x.[StaffDocumentType_ID] = s.[Id]);

-------------------------------------------------------------------------------
-- 11. LU_STAFFTYPE — the clinical role a staff member is registered under.
--     NOTE the ids here are THREE-LETTER CODES, not numbers: dbo.Staff.Staff_Type
--     stores this StaffType_ID.
-------------------------------------------------------------------------------
;WITH [Seed] ([Id], [Name]) AS
(
              SELECT 'ANE', 'ANESTHESIA PROVIDER'
    UNION ALL SELECT 'END', 'ENDOSCOPIST'
    UNION ALL SELECT 'ENT', 'ENDOSCOPY TECHNICIAN'
    UNION ALL SELECT 'GAS', 'GASTROINTESTINAL ASSISTANT'
    UNION ALL SELECT 'NUR', 'REGISTERED NURSE'
)
INSERT INTO [dbo].[LU_STAFFTYPE] ([StaffType_ID], [StaffType_Name])
SELECT s.[Id], s.[Name]
FROM [Seed] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LU_STAFFTYPE] x WHERE x.[StaffType_ID] = s.[Id]);
GO
