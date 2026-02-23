# powershell -ExecutionPolicy Bypass -File ".\Export-NucentraPortal.ps1" -InputFolder "C:\Users\MuhammadNaufalSyedNa\Documents\GitHub\CRC_Portal" -OutputFolder "C:\Users\MuhammadNaufalSyedNa\Desktop\ExportOut"

param(
    [Parameter(Mandatory=$true)][string]$InputFolder,
    [Parameter(Mandatory=$true)][string]$OutputFolder,
    [long]$MaxChars = 18000000,
    [switch]$FailOnOversize,
    [string[]]$IncludeFolders = @("CRC.Api","CRC.Data","CRC.Database","CRC.Web")
)

# Ensure output folder exists
New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null

# ---- Normalize paths (PowerShell 5.1 compatible) ----
$InputFolder = (Resolve-Path -Path $InputFolder).Path
$resolvedOut = Resolve-Path -Path $OutputFolder -ErrorAction SilentlyContinue
if ($resolvedOut) { $OutputFolder = $resolvedOut.Path }

$outIndex = 1
$buffer = ""

function Flush-Buffer {
    if ($script:buffer.Length -gt 0) {
        $outPath = Join-Path $OutputFolder ("output_{0:d3}.txt" -f $script:outIndex)
        Set-Content -Path $outPath -Value $script:buffer -Encoding UTF8
        $script:outIndex++
        $script:buffer = ""
    }
}

function Write-WholePayloadToNewFile {
    param([string]$Payload)
    $outPath = Join-Path $OutputFolder ("output_{0:d3}.txt" -f $script:outIndex)
    Set-Content -Path $outPath -Value $Payload -Encoding UTF8
    $script:outIndex++
}

# Exclude anything under these folders (at any depth)
$excludeRegex = '([\\/])(bin|obj|properties)([\\/])|([\\/])wwwroot([\\/])(lib|css|images|uploads)([\\/])'

# 1) Only pick TOP-LEVEL items starting with the prefix
$topLevelItems = Get-ChildItem -Path $InputFolder -Force |
    Where-Object { $IncludeFolders -contains $_.Name }

# 2) Build list of files (IMPORTANT: initialize before adding)
$filesToProcess = @()

# Add top-level matching files
$filesToProcess += $topLevelItems | Where-Object { -not $_.PSIsContainer }

# Add files under matching top-level directories
$topLevelDirs = $topLevelItems | Where-Object { $_.PSIsContainer }
foreach ($dir in $topLevelDirs) {
    $filesToProcess += Get-ChildItem -Path $dir.FullName -Recurse -File -Force
}

# 3) Apply exclusions and sort (de-duplicate)
$filesToProcess = $filesToProcess |
    Where-Object { $_.FullName -notmatch $excludeRegex } |
    Sort-Object FullName -Unique

# 4) Pack into output files without splitting any single file payload
foreach ($file in $filesToProcess) {

    $relPath = $file.FullName.Substring($InputFolder.Length).TrimStart("\","/")

    try {
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction Stop
    } catch {
        Write-Warning "Skipped unreadable file: $relPath"
        continue
    }

    $header  = "`n===== FILE: $relPath =====`n"
    $payload = $header + $content + "`n"

    # If a single payload exceeds MaxChars, cannot keep <= MaxChars without splitting
    if ($payload.Length -gt $MaxChars) {
        Flush-Buffer

        if ($FailOnOversize) {
            throw "File '$relPath' payload length ($($payload.Length)) exceeds MaxChars ($MaxChars). Cannot copy without splitting."
        } else {
            Write-Warning "Oversize payload ($($payload.Length) chars) for '$relPath'. Writing to its own output file (will exceed MaxChars)."
            Write-WholePayloadToNewFile -Payload $payload
            continue
        }
    }

    # If adding the whole payload would exceed MaxChars, flush first
    if (($buffer.Length + $payload.Length) -gt $MaxChars) {
        Flush-Buffer
    }

    # Append whole payload (never partial)
    $buffer += $payload
}

Flush-Buffer
"Done."
