<#
    New-SeedLocation.ps1 — generates CRC.Database/Scripts/Seed_Location.sql.

    WHY THIS SCRIPT EXISTS
      So the LU_LOCATION seed is REPRODUCIBLE. Seed_Location.sql carries 3,242
      INSERT rows; nobody should ever edit those by hand. If the postcode list is
      ever revised, the owner replaces reformatted_Malaysia_Postcode-postcodes.csv
      in the repository root and RE-RUNS THIS SCRIPT — the generated file is
      overwritten from the CSV, and the CSV stays the single source of truth.

      Run it from anywhere:

          pwsh -File CRC.Database/Scripts/Tools/New-SeedLocation.ps1

      Paths default to positions relative to this script, so nothing here depends
      on where the repository is checked out. Regenerating from an unchanged CSV
      reproduces the file byte for byte, so `git diff` after a run is the honest
      test of whether the seed still matches the CSV.

    WHAT IT GUARANTEES ABOUT THE OUTPUT
      * Rows are emitted in ascending LocationID order. Combined with the ParentID
        assertion below, this is what lets a plain ordered insert satisfy the
        self-referencing foreign key FK_LU_LOCATION_Parent — a row's parent is
        always already inserted by the time the row itself arrives.
      * [Name] is emitted as an N'...' literal (the column is NVARCHAR(150)) with
        any single quote doubled.
      * An empty ParentID becomes the keyword NULL — never 0, never ''.
      * The file is written as UTF-8 with LF line endings, matching the other
        scripts in CRC.Database/Scripts.

    WHAT IT REFUSES TO DO
      It throws rather than writing a broken seed if either of the two properties
      the ordered insert depends on is violated by the CSV:
        (a) no row's ParentID may be greater than its own LocationID;
        (b) SortOrder must equal LocationID on every row.
      It also throws on duplicate ids, a missing Name, or unexpected columns.

    This file is registered in CRC.Database.sqlproj as a <None> item purely so it
    stays visible in Solution Explorer next to the file it generates. It is not
    compiled, not deployed, and never runs during a publish.
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    # Source CSV. Defaults to reformatted_Malaysia_Postcode-postcodes.csv in the repository root.
    [string] $CsvPath,

    # Generated seed. Defaults to CRC.Database/Scripts/Seed_Location.sql.
    [string] $OutputPath,

    # Rows per INSERT ... VALUES statement. SQL Server's hard cap is 1000.
    [ValidateRange(1, 1000)]
    [int] $BatchSize = 500
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# 1. Resolve paths relative to this script — never hard-coded absolutes.
#    Tools/ -> Scripts/ -> CRC.Database/ -> repository root.
# ---------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($CsvPath)) {
    $CsvPath = Join-Path $PSScriptRoot '..\..\..\reformatted_Malaysia_Postcode-postcodes.csv'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot '..\Seed_Location.sql'
}
$CsvPath = [System.IO.Path]::GetFullPath($CsvPath)
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)

if (-not (Test-Path -LiteralPath $CsvPath -PathType Leaf)) {
    throw "Source CSV not found: $CsvPath"
}

Write-Verbose "Reading  $CsvPath"

# ---------------------------------------------------------------------------
# 2. Read and shape-check the CSV.
# ---------------------------------------------------------------------------
$csvRows = @(Import-Csv -LiteralPath $CsvPath -Encoding UTF8)
if ($csvRows.Count -eq 0) {
    throw "Source CSV holds no data rows: $CsvPath"
}

$expectedColumns = @('LocationID', 'LocationType', 'ParentID', 'Name', 'SortOrder')
$actualColumns = @($csvRows[0].PSObject.Properties.Name)
if (@(Compare-Object -ReferenceObject $expectedColumns -DifferenceObject $actualColumns -SyncWindow 0).Count -gt 0) {
    throw ("Unexpected CSV columns. Expected '{0}' but found '{1}'." -f ($expectedColumns -join ','), ($actualColumns -join ','))
}

$records = [System.Collections.Generic.List[psobject]]::new()
$lineNumber = 1   # the header
foreach ($csvRow in $csvRows) {
    $lineNumber++

    $parentText = "$($csvRow.ParentID)".Trim()
    $nameText = "$($csvRow.Name)"

    if ([string]::IsNullOrWhiteSpace($nameText)) {
        throw "CSV line ${lineNumber}: [Name] is empty. LU_LOCATION.[Name] is NOT NULL."
    }

    $records.Add([pscustomobject]@{
            LocationId   = [int] $csvRow.LocationID
            LocationType = [int] $csvRow.LocationType
            ParentId     = if ($parentText -eq '') { $null } else { [int] $parentText }
            Name         = $nameText
            SortOrder    = [int] $csvRow.SortOrder
            LineNumber   = $lineNumber
        })
}

# ---------------------------------------------------------------------------
# 3. Sort ascending by LocationId, then assert the two properties the ordered
#    insert depends on. A violation is fatal: writing the file anyway would
#    produce a seed that fails mid-publish, which is far worse than not writing
#    one at all.
# ---------------------------------------------------------------------------
$records = @($records | Sort-Object -Property LocationId)

$duplicateIds = @($records | Group-Object -Property LocationId | Where-Object { $_.Count -gt 1 })
if ($duplicateIds.Count -gt 0) {
    $sample = (@($duplicateIds | Select-Object -First 5 | ForEach-Object { $_.Name })) -join ', '
    throw ("ABORTED — LocationID is not unique ({0} duplicated id(s), first: {1}). It is the primary key." -f $duplicateIds.Count, $sample)
}

# (a) No row's ParentID may exceed its own LocationID, or the row would be inserted
#     before its parent exists and FK_LU_LOCATION_Parent would reject it.
$forwardParents = @($records | Where-Object { $null -ne $_.ParentId -and $_.ParentId -gt $_.LocationId })
if ($forwardParents.Count -gt 0) {
    $sample = ($forwardParents | Select-Object -First 5 | ForEach-Object { "line $($_.LineNumber): LocationID $($_.LocationId) -> ParentID $($_.ParentId)" }) -join '; '
    throw ("ABORTED — {0} row(s) have a ParentID GREATER than their own LocationID: {1}. " -f $forwardParents.Count, $sample) +
    'An ordered insert cannot satisfy FK_LU_LOCATION_Parent for these rows. Re-number the CSV so every parent precedes its children.'
}

# (b) SortOrder must equal LocationID. The seed emits SortOrder from the CSV, but
#     the header of the generated file states this identity as fact, so verify it.
$sortMismatches = @($records | Where-Object { $_.SortOrder -ne $_.LocationId })
if ($sortMismatches.Count -gt 0) {
    $sample = ($sortMismatches | Select-Object -First 5 | ForEach-Object { "line $($_.LineNumber): LocationID $($_.LocationId) -> SortOrder $($_.SortOrder)" }) -join '; '
    throw ("ABORTED — {0} row(s) have SortOrder <> LocationID: {1}. " -f $sortMismatches.Count, $sample) +
    'The generated file documents SortOrder = LocationId as an invariant; fix the CSV or update this script and the header it writes.'
}

# Orphan check: every non-null ParentID must name a row that actually exists.
$knownIds = [System.Collections.Generic.HashSet[int]]::new()
foreach ($record in $records) { [void] $knownIds.Add($record.LocationId) }
$orphans = @($records | Where-Object { $null -ne $_.ParentId -and -not $knownIds.Contains($_.ParentId) })
if ($orphans.Count -gt 0) {
    $sample = ($orphans | Select-Object -First 5 | ForEach-Object { "line $($_.LineNumber): LocationID $($_.LocationId) -> ParentID $($_.ParentId)" }) -join '; '
    throw ("ABORTED — {0} row(s) name a ParentID that does not exist in the CSV: {1}." -f $orphans.Count, $sample)
}

$stateCount = @($records | Where-Object { $_.LocationType -eq 1 }).Count
$cityCount = @($records | Where-Object { $_.LocationType -eq 2 }).Count
$postcodeCount = @($records | Where-Object { $_.LocationType -eq 3 }).Count
$totalCount = $records.Count
$maxLocationId = ($records | Select-Object -Last 1).LocationId

$csvName = [System.IO.Path]::GetFileName($CsvPath)
$totalText = $totalCount.ToString('N0')
$stateText = $stateCount.ToString('N0')
$cityText = $cityCount.ToString('N0')
$postcodeText = $postcodeCount.ToString('N0')

# Right-align the counts in the header's little table so it stays readable
# whatever the numbers grow to.
$countWidth = (@($totalText, $stateText, $cityText, $postcodeText) | Measure-Object -Property Length -Maximum).Maximum
$totalPadded = $totalText.PadLeft($countWidth)
$statePadded = $stateText.PadLeft($countWidth)
$cityPadded = $cityText.PadLeft($countWidth)
$postcodePadded = $postcodeText.PadLeft($countWidth)

# ---------------------------------------------------------------------------
# 4. Build the file.
# ---------------------------------------------------------------------------
$sb = [System.Text.StringBuilder]::new()
[void] $sb.Append(@"
/*
    dbo.LU_LOCATION — the Malaysian STATE -> CITY -> POSTCODE tree, $totalText rows.

    ===========================================================================
    GENERATED FILE — DO NOT HAND-EDIT
    ===========================================================================
      Generator : CRC.Database/Scripts/Tools/New-SeedLocation.ps1
      Source    : $csvName (repository root)

      To revise the postcode list, replace the CSV and re-run the generator:

          pwsh -File CRC.Database/Scripts/Tools/New-SeedLocation.ps1

      Editing the $totalText INSERT rows below by hand puts this file out of step
      with the CSV that produced it, and the next regeneration silently throws the
      edit away. Regenerating from an unchanged CSV reproduces this file byte for
      byte, so an empty "git diff" after a run means seed and CSV still agree.

    WHAT THIS TABLE IS
      One self-referencing table holding the three-level tree behind every address
      dropdown in the portal:

          LocationType 1 = STATE      $statePadded rows   ParentId IS NULL
          LocationType 2 = CITY       $cityPadded rows   ParentId -> a STATE
          LocationType 3 = POSTCODE   $postcodePadded rows   ParentId -> a CITY
          --------------------------------------------------------
                                      $totalPadded rows total

      It is read by exactly three procedures — spLU_LOCATION_ListStates,
      spLU_LOCATION_ListCityByState and spLU_LOCATION_ListPostcodesByCity — each
      filtering on [LocationType] and, for the lower two levels, [ParentId]. An
      unseeded table is not a subtle failure: all three dropdowns come back empty
      and no address can be captured.

    WHY THE IDS ARE EXPLICIT
      [ParentId] is a foreign key onto [LocationId] IN THE SAME TABLE
      (FK_LU_LOCATION_Parent): every city names its state by id and every postcode
      names its city by id. The ids are therefore LOAD-BEARING DATA, not surrogate
      noise — they cannot be left for IDENTITY to invent, or every parent link in
      the file would point at the wrong row. So the rows go in under
      SET IDENTITY_INSERT with their ids spelled out.

      Inserting in ascending LocationId order is what keeps the self-FK satisfied:
      no row in the source data has a ParentId greater than its own LocationId, so
      a row's parent is always already present when the row arrives. [SortOrder]
      equals [LocationId] on every row. The generator asserts both properties
      against the CSV and refuses to write this file if either fails.

      Explicit ids under IDENTITY_INSERT advance the identity's current value on
      their own, so IDENT_CURRENT ends up at $maxLocationId and the next row created
      through the UI gets $($maxLocationId + 1). No DBCC CHECKIDENT is needed.

    HOW IT IS GUARDED, AND WHAT THAT COSTS
      ONE whole-table guard wraps the entire seed:

          IF NOT EXISTS (SELECT 1 FROM [dbo].[LU_LOCATION])

      not the per-row WHERE NOT EXISTS used in Seed_Lookups.sql. This is static
      national reference data that is either loaded or not, and $totalText individual
      NOT EXISTS sub-queries would slow down every single publish to protect
      against a state that should not arise.

      THE CONSEQUENCE, PLAINLY: a PARTIALLY populated LU_LOCATION is never
      repaired by a publish. If the table holds even one row, the whole seed is
      skipped. To reload it, run

          DELETE FROM [dbo].[LU_LOCATION];

      and publish again — or use the commented-out import block below. (Deleting
      fails while any row is still referenced, which is the correct outcome: a
      patient address pointing at a LocationId must not be orphaned.)
*/
SET NOCOUNT ON;

--=============================================================================
-- ALTERNATIVE IMPORT ROUTE — DELIBERATELY COMMENTED OUT. NOT PART OF A PUBLISH.
--=============================================================================
-- WHEN TO USE IT: you are re-importing a REVISED postcode list into a database
-- that is ALREADY LIVE, by hand in SSMS, without re-publishing the project. A
-- normal publish needs none of this — the seed further down does the whole job.
--
-- WHY IT IS NOT THE PUBLISH PATH:
--   * BULK INSERT reads the file from the SQL SERVER MACHINE, not from the
--     client running SSMS. The CSV has to be copied somewhere the SQL Server
--     SERVICE ACCOUNT can read (C:\Temp on the server itself, say), or it fails
--     with "Cannot bulk load because the file could not be opened".
--   * It does NOT work on Azure SQL Database at all — there is no local
--     filesystem for the engine to read. That is precisely why the publish path
--     below is $totalText inline INSERT rows instead: the one post-deployment script
--     has to run unchanged against SQL Server and Azure SQL alike.
--
-- Copy the block between the rules into SSMS, fix the path, run it as one script.
--
-- ---------------------------------------------------------------------------
-- -- Step 1. Empty the table first. This FAILS if any row is still referenced:
-- --         LU_LOCATION points at itself (FK_LU_LOCATION_Parent), and any
-- --         application row holding a LocationId blocks the delete as well.
-- DELETE FROM [dbo].[LU_LOCATION];
--
-- -- Step 2. Land the raw CSV in a staging table. Every column is text so that
-- --         an empty ParentID arrives as '' instead of failing the conversion.
-- CREATE TABLE #Staging
-- (
--     [LocationID]   VARCHAR(20)   NULL,
--     [LocationType] VARCHAR(10)   NULL,
--     [ParentID]     VARCHAR(20)   NULL,
--     [Name]         NVARCHAR(150) NULL,
--     [SortOrder]    VARCHAR(20)   NULL
-- );
--
-- BULK INSERT #Staging
-- FROM 'C:\Temp\$csvName'
-- WITH
-- (
--     FORMAT     = 'CSV',
--     FIRSTROW   = 2,          -- skip the header line
--     FIELDQUOTE = '"',
--     CODEPAGE   = '65001'     -- UTF-8
-- );
--
-- -- Step 3. Copy staging into the real table, keeping the ids. Ordered by
-- --         LocationId so every parent lands before its children and the
-- --         self-FK never trips. NULLIF turns an empty ParentID into NULL --
-- --         never 0, which would point at a row that cannot exist.
-- SET IDENTITY_INSERT [dbo].[LU_LOCATION] ON;
--
-- INSERT INTO [dbo].[LU_LOCATION] ([LocationId], [LocationType], [ParentId], [Name], [SortOrder])
-- SELECT CONVERT(INT, s.[LocationID]),
--        CONVERT(TINYINT, s.[LocationType]),
--        CONVERT(INT, NULLIF(LTRIM(RTRIM(s.[ParentID])), '')),
--        s.[Name],
--        CONVERT(INT, s.[SortOrder])
-- FROM #Staging s
-- ORDER BY CONVERT(INT, s.[LocationID]);
--
-- SET IDENTITY_INSERT [dbo].[LU_LOCATION] OFF;
--
-- DROP TABLE #Staging;
-- ---------------------------------------------------------------------------
--
-- SECOND ALTERNATIVE — bcp, from a command prompt, into that same staging table
-- (created as a permanent table first, since bcp gets its own session and cannot
-- see a #temp one). Same "the file must be reachable" caveat applies:
--
--   bcp CRC_DB.dbo.LU_LOCATION_Staging in "C:\Temp\$csvName" -S localhost -T -c -t "," -F 2
--
-- THIRD ALTERNATIVE — SSMS's own Import Flat File wizard (right-click the
-- database > Tasks > Import Flat File). It reads the CSV from YOUR machine, so it
-- is the one route that works when you cannot put a file on the server at all.
--
-- AFTERWARDS: re-run the row-count checks at the foot of this file before
-- trusting the import.
--=============================================================================


"@.Replace("`r`n", "`n"))

[void] $sb.Append("IF NOT EXISTS (SELECT 1 FROM [dbo].[LU_LOCATION])`nBEGIN`n")
[void] $sb.Append("    SET IDENTITY_INSERT [dbo].[LU_LOCATION] ON;`n")

$columnList = '[LocationId], [LocationType], [ParentId], [Name], [SortOrder]'
for ($offset = 0; $offset -lt $records.Count; $offset += $BatchSize) {
    $batch = @($records[$offset..([Math]::Min($offset + $BatchSize, $records.Count) - 1)])
    $firstId = $batch[0].LocationId
    $lastId = $batch[-1].LocationId

    [void] $sb.Append("`n    -- LocationId $firstId - $lastId ($($batch.Count) rows)`n")
    [void] $sb.Append("    INSERT INTO [dbo].[LU_LOCATION] ($columnList) VALUES`n")

    for ($i = 0; $i -lt $batch.Count; $i++) {
        $record = $batch[$i]
        $parentLiteral = if ($null -eq $record.ParentId) { 'NULL' } else { "$($record.ParentId)" }
        $nameLiteral = "N'" + $record.Name.Replace("'", "''") + "'"
        $terminator = if ($i -eq $batch.Count - 1) { ';' } else { ',' }

        [void] $sb.Append("    ($($record.LocationId), $($record.LocationType), $parentLiteral, $nameLiteral, $($record.SortOrder))$terminator`n")
    }
}

[void] $sb.Append("`n    SET IDENTITY_INSERT [dbo].[LU_LOCATION] OFF;`nEND`n")

[void] $sb.Append(@"

-------------------------------------------------------------------------------
-- VERIFICATION — run this by hand after publishing (it is commented out so the
-- publish itself stays silent).
--
--   SELECT [LocationType], COUNT(*) AS [Rows]
--   FROM [dbo].[LU_LOCATION]
--   GROUP BY [LocationType]
--   ORDER BY [LocationType];
--
-- Expected:  1 (STATE) -> $stateCount    2 (CITY) -> $cityCount    3 (POSTCODE) -> $postcodeCount
--            $totalCount rows in total, and IDENT_CURRENT('dbo.LU_LOCATION') = $maxLocationId.
--
-- Anything less means the table was already partly populated when the publish
-- ran and the whole-table guard skipped the seed. See the header: DELETE the
-- table and publish again.
-------------------------------------------------------------------------------
GO
"@.Replace("`r`n", "`n"))

# ---------------------------------------------------------------------------
# 5. Write UTF-8, no BOM, LF line endings — matching the other Scripts files.
# ---------------------------------------------------------------------------
$outputDirectory = [System.IO.Path]::GetDirectoryName($OutputPath)
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    [void] (New-Item -ItemType Directory -Path $outputDirectory -Force)
}

[System.IO.File]::WriteAllText($OutputPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))

Write-Host "Wrote $OutputPath"
Write-Host ("  {0} rows: {1} states, {2} cities, {3} postcodes" -f $totalCount, $stateCount, $cityCount, $postcodeCount)
Write-Host ("  {0} INSERT batches of at most {1} rows" -f [Math]::Ceiling($totalCount / $BatchSize), $BatchSize)
Write-Host ("  {0:N0} bytes" -f (Get-Item -LiteralPath $OutputPath).Length)
