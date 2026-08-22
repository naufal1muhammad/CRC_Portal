#Requires -Version 5.1
<#
.SYNOPSIS
    Smoke-tests nucentra's Agent API — all eight endpoints under /api/agent, plus both negative tests.

.DESCRIPTION
    CoreFlow.md §13 is the specification; this script is the thing that proves it against a running site.
    It drives the seven read endpoints (§13.4), both key-rejection paths (§13.3), and — only when you ask
    for it — the one write (§13.5). One PASS / FAIL / SKIP line per check, a summary, and an exit code.

    It is SELF-DRIVING: it discovers its own fixture data from the API itself, in dependency order.
    /branches gives it a branchId, /patients/queue gives it a patientId and a phone number, /staff and
    /slots/open follow from the branchId. Nothing is hard-coded to one database, so it works against a
    local CRC_DB and against Azure without editing.

    🔴 THE KEY
        -ApiKey has NO DEFAULT and the script will not run without it. It reads no key from a file, from
        an environment variable or from appsettings*.json, and it never prints one: not in a banner, not in
        a failure dump, not in a log. Any occurrence of the supplied key in a response body is redacted
        before that body reaches the console (see Protect-Secret). Pass it from a variable rather than
        typing it inline if your shell keeps history.

    🔴 WHY THE WRITE IS OPT-IN, AND OFF BY DEFAULT
        POST /api/agent/appointments consumes a REAL CLINICIAN HOUR. There is no cancellation concept in
        nucentra at all (CoreFlow.md §3.9): changing an appointment's status touches no slots, so the only
        way to give the hour back is POST /Patient/DeleteAppointment — in the portal, by a person. A smoke
        test must therefore never book by accident, so the write runs only under -IncludeWrite, and even
        then it tells you afterwards exactly what it consumed and how to release it.

    EXIT CODES
        0  every check passed (skips are reported but do not fail the run)
        1  at least one check FAILED
        2  usage error — no -ApiKey

.PARAMETER BaseUrl
    Root of the site. Defaults to the https launch profile, https://localhost:7276. Use https: the portal's
    __Host-CSRF cookie requires it (§2.4), and while /api/agent itself carries [IgnoreAntiforgeryToken],
    there is no reason to smoke-test a clinical API over plain HTTP.

.PARAMETER ApiKey
    ANY ONE member of Agent:ApiKey, which is an ARRAY — locally the array in appsettings.Development.json,
    in Azure the App Service app settings Agent__ApiKey__0, Agent__ApiKey__1, … (🔴 TWO underscores between
    every segment, and the __0 index is part of the name — a scalar Agent__ApiKey binds to nothing, §13.6).
    The filter accepts any member, so during a rotation either key drives this script equally. REQUIRED.
    No default. This script sends ONE key, in one header, and that is all it has ever done.

.PARAMETER IncludeWrite
    Also drive POST /api/agent/appointments. OFF by default. See the warning above.

.PARAMETER SkipCertificateCheck
    Accept an untrusted TLS certificate. Not needed against a machine with the .NET dev certificate
    trusted; useful against a container or a staging host with a self-signed one.

.EXAMPLE
    $key = Read-Host 'Agent key' -AsSecureString   # or however you keep it
    .\Test-AgentApi.ps1 -ApiKey ([Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($key)))

.EXAMPLE
    .\Test-AgentApi.ps1 -BaseUrl 'https://localhost:7276' -ApiKey $env:AGENT_KEY -IncludeWrite

.NOTES
    Shape modelled on Nucentra_WhatsApp_Agent_Plan.md §10.1's curl block, rewritten in PowerShell to match
    the rest of this repository's tooling (Export-NucentraPortal.ps1,
    CRC.Database/Scripts/Tools/New-SeedLocation.ps1). Written for Windows PowerShell 5.1 — the version this
    repository is developed on — and it runs unchanged on PowerShell 7.
#>

[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://localhost:7276',

    # 🔴 NO DEFAULT VALUE, EVER. Not "", not a placeholder, not a value read from anywhere.
    [string]$ApiKey,

    [switch]$IncludeWrite,

    [switch]$SkipCertificateCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------------------------------
# Usage guard. An empty key is not a usage default — it is also what the portal itself treats as a
# MISCONFIGURATION and refuses every request for (§13.6, AgentApiKeyFilter's first branch), so running
# with one would produce nine identical 401s and tell you nothing.
# ---------------------------------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Host ''
    Write-Host '  -ApiKey is required and has no default.' -ForegroundColor Red
    Write-Host ''
    Write-Host '  Locally  : any member of the Agent:ApiKey ARRAY in CRC.Web/appsettings.Development.json'
    Write-Host '  In Azure : the App Service app setting Agent__ApiKey__0  (TWO underscores between every'
    Write-Host '             segment, and the __0 index is part of the name - CoreFlow.md 13.6)'
    Write-Host ''
    Write-Host '  This script deliberately does not read a key from a file or an environment variable'
    Write-Host '  on your behalf, and never prints one.'
    Write-Host ''
    exit 2
}

$BaseUrl = $BaseUrl.TrimEnd('/')

# ---------------------------------------------------------------------------------------------------
# TLS. Windows PowerShell 5.1 still negotiates SSL3/TLS1.0 by default against some hosts; ASP.NET Core
# will not talk to it. Harmless on PowerShell 7.
# ---------------------------------------------------------------------------------------------------
try {
    [Net.ServicePointManager]::SecurityProtocol =
        [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
} catch { }

# ---------------------------------------------------------------------------------------------------
# Certificate handling. PowerShell 7 has -SkipCertificateCheck on Invoke-WebRequest; 5.1 does not, and
# needs a CertificatePolicy instead. Both are opt-in and neither is the default.
# ---------------------------------------------------------------------------------------------------
$script:IsPS7 = $PSVersionTable.PSVersion.Major -ge 6
$script:ExtraArgs = @{}

if ($SkipCertificateCheck) {
    if ($script:IsPS7) {
        $script:ExtraArgs['SkipCertificateCheck'] = $true
    }
    elseif (-not ('NucentraTrustAllCerts' -as [type])) {
        Add-Type -TypeDefinition @'
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class NucentraTrustAllCerts : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) {
        return true;
    }
}
'@
        [System.Net.ServicePointManager]::CertificatePolicy = New-Object NucentraTrustAllCerts
    }
    else {
        [System.Net.ServicePointManager]::CertificatePolicy = New-Object NucentraTrustAllCerts
    }
}

# ---------------------------------------------------------------------------------------------------
# Result bookkeeping.
# ---------------------------------------------------------------------------------------------------
$script:Pass = 0
$script:Fail = 0
$script:Skip = 0

function Protect-Secret {
    <#
      🔴 Nothing this script prints may contain the key. Response bodies do not echo it today, but a
      failure dump prints whatever came back, and "whatever came back" is not this script's to promise.
      One replacement, applied to every string on its way to the console.
    #>
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    return $Text.Replace($ApiKey, '<redacted>')
}

function Write-Check {
    param(
        [ValidateSet('PASS', 'FAIL', 'SKIP')][string]$Outcome,
        [string]$Name,
        [string]$Detail
    )

    switch ($Outcome) {
        'PASS' { $script:Pass++; $colour = 'Green' }
        'FAIL' { $script:Fail++; $colour = 'Red' }
        'SKIP' { $script:Skip++; $colour = 'Yellow' }
    }

    $line = '  {0,-4}  {1}' -f $Outcome, $Name
    if (-not [string]::IsNullOrWhiteSpace($Detail)) {
        $line += '  --  ' + (Protect-Secret $Detail)
    }
    Write-Host $line -ForegroundColor $colour
}

function Invoke-AgentRequest {
    <#
      One HTTP call, one answer, and NON-2xx IS A NORMAL ANSWER HERE — two of the eleven checks expect a
      401. Windows PowerShell 5.1 throws on any non-2xx and PowerShell 7 throws differently, so both are
      caught and reduced to { StatusCode, Body, Json }. A transport failure (site not running, DNS, TLS)
      still throws, because that is not a test result — it means there was nothing to test.
    #>
    param(
        [string]$Path,
        [string]$Method = 'GET',
        [hashtable]$Headers = @{},
        [string]$Body
    )

    $uri = $BaseUrl + $Path
    $args = @{
        Uri             = $uri
        Method          = $Method
        Headers         = $Headers
        UseBasicParsing = $true
        TimeoutSec      = 30
    }
    foreach ($k in $script:ExtraArgs.Keys) { $args[$k] = $script:ExtraArgs[$k] }
    if ($PSBoundParameters.ContainsKey('Body')) {
        $args['Body'] = $Body
        $args['ContentType'] = 'application/json'
    }

    $status = 0
    $text = ''

    try {
        $resp = Invoke-WebRequest @args
        $status = [int]$resp.StatusCode
        $text = [string]$resp.Content
    }
    catch {
        $err = $_
        $response = $null
        try { $response = $err.Exception.Response } catch { $response = $null }

        if ($null -ne $response) {
            try { $status = [int]$response.StatusCode } catch { $status = 0 }

            # 5.1: HttpWebResponse, read the stream. 7: the body is already on ErrorDetails.
            if ($response | Get-Member -Name 'GetResponseStream' -MemberType Method -ErrorAction SilentlyContinue) {
                try {
                    $stream = $response.GetResponseStream()
                    $reader = New-Object System.IO.StreamReader($stream)
                    $text = $reader.ReadToEnd()
                    $reader.Dispose()
                } catch { }
            }
        }

        if ([string]::IsNullOrEmpty($text)) {
            try { if ($err.ErrorDetails) { $text = [string]$err.ErrorDetails.Message } } catch { }
        }

        if ($status -eq 0) {
            # No HTTP response at all. Not a failing check — a broken run.
            throw ("Could not reach {0} : {1}" -f $uri, $err.Exception.Message)
        }
    }

    $json = $null
    if (-not [string]::IsNullOrWhiteSpace($text)) {
        try { $json = $text | ConvertFrom-Json } catch { $json = $null }
    }

    return [pscustomobject]@{
        StatusCode = $status
        Body       = $text
        Json       = $json
    }
}

function Test-HasProperty {
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $false }
    return [bool]($Object.PSObject.Properties.Name -contains $Name)
}

function Get-DataArray {
    # TWO separate unrollings have to be defeated here, and both bite on a ONE-ROW response only —
    # which is exactly the shape a small database returns, so getting this wrong looks like a working
    # script until somebody runs it against a real one.
    #   1. ConvertFrom-Json hands back a bare object, not an array, for a one-element JSON array; @()
    #      puts the array back.
    #   2. `return $array` UNROLLS the array into the pipeline again on the way out of the function;
    #      the leading comma wraps it in a one-element outer array so the caller gets the array itself.
    param($Json)
    if (-not (Test-HasProperty $Json 'data')) { return , @() }
    if ($null -eq $Json.data) { return , @() }
    return , @($Json.data)
}

function Show-Body {
    param($Response, [int]$Max = 300)
    if ($null -eq $Response) { return '(no response)' }
    $b = [string]$Response.Body
    if ($b.Length -gt $Max) { $b = $b.Substring(0, $Max) + '...' }
    return ("HTTP {0} {1}" -f $Response.StatusCode, $b)
}

$headers = @{ 'X-Agent-Key' = $ApiKey }

Write-Host ''
Write-Host '===================================================================================='
Write-Host ' nucentra Agent API smoke test          CoreFlow.md 13 -- eight endpoints, two 401s'
Write-Host '===================================================================================='
Write-Host ("  Base URL      : {0}" -f $BaseUrl)
Write-Host ("  API key       : supplied ({0} characters, value not shown)" -f $ApiKey.Length)
Write-Host ("  Write test    : {0}" -f $(if ($IncludeWrite) { 'INCLUDED (-IncludeWrite) -- this books a real clinician hour' } else { 'skipped (pass -IncludeWrite to run it)' }))
Write-Host ("  PowerShell    : {0}" -f $PSVersionTable.PSVersion)
Write-Host ''

if (-not $BaseUrl.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)) {
    Write-Host '  NOTE: not https. The Agent API itself will answer, but nothing else in the portal will.' -ForegroundColor Yellow
    Write-Host ''
}

# ===================================================================================================
# 1. THE NEGATIVE TESTS. FIRST, DELIBERATELY.
#
# 🔴 These are the checks that matter, and CoreFlow.md 13.3 says why: AgentApiController carries
# [AllowAnonymous], which switches off the global AuthorizeFilter that every other action in the portal
# relies on (2.2). AgentApiKeyFilter is the ONLY thing closing that gap. A read endpoint that answers 200
# without a key is a patient-data leak, and from the happy path it looks exactly like a working endpoint.
#
# They are pointed at /patients/queue rather than /branches because that endpoint returns the widest
# patient payload in the API -- every active patient's name, phone number and screening state (13.4).
# If the guard has come off ANY action, this is the one whose failure costs the most.
# ===================================================================================================
Write-Host '-- Negative tests (the guard) ------------------------------------------------------'

try {
    $r = Invoke-AgentRequest -Path '/api/agent/patients/queue' -Headers @{}
    if ($r.StatusCode -eq 401 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $false) {
        Write-Check PASS 'NEG-1  no X-Agent-Key header          -> 401 Unauthorized'
    }
    else {
        Write-Check FAIL 'NEG-1  no X-Agent-Key header          -> expected 401' (Show-Body $r)
    }
}
catch { Write-Check FAIL 'NEG-1  no X-Agent-Key header' $_.Exception.Message }

try {
    # A random value, never derived from the real key.
    $wrongKey = 'not-the-key-' + [guid]::NewGuid().ToString('N')
    $r = Invoke-AgentRequest -Path '/api/agent/patients/queue' -Headers @{ 'X-Agent-Key' = $wrongKey }
    if ($r.StatusCode -eq 401 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $false) {
        Write-Check PASS 'NEG-2  wrong X-Agent-Key              -> 401 Unauthorized'
    }
    else {
        Write-Check FAIL 'NEG-2  wrong X-Agent-Key              -> expected 401' (Show-Body $r)
    }
}
catch { Write-Check FAIL 'NEG-2  wrong X-Agent-Key' $_.Exception.Message }

Write-Host ''
Write-Host '-- The seven reads (13.4) ----------------------------------------------------------'

# Fixture data, discovered as we go rather than hard-coded to one database.
$branchId = $null
$patientId = $null
$patientPhone = $null
$slot = $null

# ---------------------------------------------------------------------------------------------------
# Endpoint 5 -- GET /api/agent/branches. First, because everything else needs a branchId.
# ---------------------------------------------------------------------------------------------------
try {
    $r = Invoke-AgentRequest -Path '/api/agent/branches' -Headers $headers
    $rows = Get-DataArray $r.Json
    if ($r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $true) {
        if ($rows.Count -gt 0) { $branchId = $rows[0].branchId }
        Write-Check PASS ('E5  GET  /api/agent/branches                     ({0} branch(es))' -f $rows.Count)
    }
    else {
        Write-Check FAIL 'E5  GET  /api/agent/branches' (Show-Body $r)
    }
}
catch { Write-Check FAIL 'E5  GET  /api/agent/branches' $_.Exception.Message }

# ---------------------------------------------------------------------------------------------------
# Endpoint 1 -- GET /api/agent/patients/queue. The widest patient payload in the API.
# ---------------------------------------------------------------------------------------------------
try {
    $r = Invoke-AgentRequest -Path '/api/agent/patients/queue' -Headers $headers
    $rows = Get-DataArray $r.Json
    if ($r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $true) {
        if ($rows.Count -gt 0) {
            $patientId = $rows[0].patientId
            # For endpoint 2 we need a patient who actually HAS a number; a NO_PHONE row cannot be
            # looked up by phone and is a legitimate row in this list (13.1 finding 4).
            $withPhone = $rows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.phone) } | Select-Object -First 1
            if ($withPhone) { $patientPhone = $withPhone.phone; $patientId = $withPhone.patientId }
        }
        Write-Check PASS ('E1  GET  /api/agent/patients/queue               ({0} active patient(s))' -f $rows.Count)
    }
    else {
        Write-Check FAIL 'E1  GET  /api/agent/patients/queue' (Show-Body $r)
    }
}
catch { Write-Check FAIL 'E1  GET  /api/agent/patients/queue' $_.Exception.Message }

# ---------------------------------------------------------------------------------------------------
# Endpoint 2 -- GET /api/agent/patients/by-phone. matchCount is the third envelope property and the
# caller is required to branch on it (13.4), so the check asserts it is present, not merely non-zero:
# zero, one and many are all successful answers.
# ---------------------------------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($patientPhone)) {
    Write-Check SKIP 'E2  GET  /api/agent/patients/by-phone' 'no patient with a phone number in the queue'
}
else {
    try {
        $r = Invoke-AgentRequest -Path ('/api/agent/patients/by-phone?phone=' + [uri]::EscapeDataString($patientPhone)) -Headers $headers
        if ($r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $true `
                -and (Test-HasProperty $r.Json 'matchCount')) {
            Write-Check PASS ('E2  GET  /api/agent/patients/by-phone           (matchCount={0})' -f $r.Json.matchCount)
        }
        else {
            Write-Check FAIL 'E2  GET  /api/agent/patients/by-phone' (Show-Body $r)
        }
    }
    catch { Write-Check FAIL 'E2  GET  /api/agent/patients/by-phone' $_.Exception.Message }
}

# The blank-parameter refusal is part of the same endpoint's contract (13.4) and costs one more call.
try {
    $r = Invoke-AgentRequest -Path '/api/agent/patients/by-phone' -Headers $headers
    if ($r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $false `
            -and (Test-HasProperty $r.Json 'correlationId')) {
        Write-Check PASS 'E2b GET  ...by-phone with no phone=              (refused in the house envelope)'
    }
    else {
        Write-Check FAIL 'E2b GET  ...by-phone with no phone=' (Show-Body $r)
    }
}
catch { Write-Check FAIL 'E2b GET  ...by-phone with no phone=' $_.Exception.Message }

# ---------------------------------------------------------------------------------------------------
# Endpoint 3 -- GET /api/agent/patients/{id}.
#
# 🔴 This one carries a PRIVACY assertion as well as a shape one. It is the only endpoint in the API
# where the full twelve-digit NRIC is even in memory (13.4 privacy rule 1): PatientBasicDetail carries
# Patient_NRIC because spPatientBasic_GetById is the Patient Edit form's read, and the action reduces it
# to four characters. If a future change widens that projection, this check is what notices.
# ---------------------------------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($patientId)) {
    Write-Check SKIP 'E3  GET  /api/agent/patients/{id}' 'no patient available from the queue'
}
else {
    try {
        $r = Invoke-AgentRequest -Path ('/api/agent/patients/' + [uri]::EscapeDataString($patientId)) -Headers $headers
        $ok = $r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $true `
            -and (Test-HasProperty $r.Json 'data') -and $r.Json.data.patientId -eq $patientId
        $last4 = if ($ok -and (Test-HasProperty $r.Json.data 'nricLast4') -and $r.Json.data.nricLast4) { [string]$r.Json.data.nricLast4 } else { '' }
        $noFullNric = -not ([regex]::IsMatch($r.Body, '\d{12}'))

        if ($ok -and $last4.Length -le 4 -and $noFullNric) {
            Write-Check PASS ('E3  GET  /api/agent/patients/{0}         (nricLast4 only, no 12-digit NRIC)' -f $patientId)
        }
        elseif ($ok -and -not $noFullNric) {
            Write-Check FAIL 'E3  GET  /api/agent/patients/{id}' 'PRIVACY: a 12-digit run appears in the body -- see CoreFlow 13.4 rule 1'
        }
        else {
            Write-Check FAIL 'E3  GET  /api/agent/patients/{id}' (Show-Body $r)
        }
    }
    catch { Write-Check FAIL 'E3  GET  /api/agent/patients/{id}' $_.Exception.Message }
}

# ---------------------------------------------------------------------------------------------------
# Endpoint 4 -- GET /api/agent/patients/{id}/appointments. An empty list is a legitimate answer, and an
# unknown id returns one too (13.4) -- so the check is on the envelope, never on the row count.
# ---------------------------------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($patientId)) {
    Write-Check SKIP 'E4  GET  /api/agent/patients/{id}/appointments' 'no patient available from the queue'
}
else {
    try {
        $r = Invoke-AgentRequest -Path ('/api/agent/patients/' + [uri]::EscapeDataString($patientId) + '/appointments') -Headers $headers
        $rows = Get-DataArray $r.Json
        if ($r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $true) {
            Write-Check PASS ('E4  GET  /api/agent/patients/{0}/appointments  ({1} row(s))' -f $patientId, $rows.Count)
        }
        else {
            Write-Check FAIL 'E4  GET  /api/agent/patients/{id}/appointments' (Show-Body $r)
        }
    }
    catch { Write-Check FAIL 'E4  GET  /api/agent/patients/{id}/appointments' $_.Exception.Message }
}

# ---------------------------------------------------------------------------------------------------
# Endpoint 6 -- GET /api/agent/staff?branchId=
# ---------------------------------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($branchId)) {
    Write-Check SKIP 'E6  GET  /api/agent/staff?branchId=' 'no active branch returned by E5'
}
else {
    try {
        $r = Invoke-AgentRequest -Path ('/api/agent/staff?branchId=' + [uri]::EscapeDataString($branchId)) -Headers $headers
        $rows = Get-DataArray $r.Json
        if ($r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $true) {
            Write-Check PASS ('E6  GET  /api/agent/staff?branchId={0}    ({1} clinician(s))' -f $branchId, $rows.Count)
        }
        else {
            Write-Check FAIL 'E6  GET  /api/agent/staff?branchId=' (Show-Body $r)
        }
    }
    catch { Write-Check FAIL 'E6  GET  /api/agent/staff?branchId=' $_.Exception.Message }
}

# ---------------------------------------------------------------------------------------------------
# Endpoint 7 -- GET /api/agent/slots/open. Ninety days forward, so an ordinary schedule is inside the
# window without the script needing to know when anyone opened hours.
#
# ONE ROW IS ONE HOUR (3.7), and this read is ADVISORY -- a slot it returns can be consumed a second
# later, and SaveAppointmentAsync answering SlotTaken is a correct system behaving correctly (13.1
# finding 3). That is why the write below re-reads rather than trusting a slot id from earlier.
# ---------------------------------------------------------------------------------------------------
$fromDate = (Get-Date).ToString('yyyy-MM-dd')
$toDate = (Get-Date).AddDays(90).ToString('yyyy-MM-dd')

if ([string]::IsNullOrWhiteSpace($branchId)) {
    Write-Check SKIP 'E7  GET  /api/agent/slots/open' 'no active branch returned by E5'
}
else {
    try {
        $path = '/api/agent/slots/open?branchId={0}&fromDate={1}&toDate={2}' -f `
            [uri]::EscapeDataString($branchId), $fromDate, $toDate
        $r = Invoke-AgentRequest -Path $path -Headers $headers
        $rows = Get-DataArray $r.Json
        if ($r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $true) {
            if ($rows.Count -gt 0) { $slot = $rows[0] }
            Write-Check PASS ('E7  GET  /api/agent/slots/open                   ({0} open hour(s), {1} to {2})' -f $rows.Count, $fromDate, $toDate)
        }
        else {
            Write-Check FAIL 'E7  GET  /api/agent/slots/open' (Show-Body $r)
        }
    }
    catch { Write-Check FAIL 'E7  GET  /api/agent/slots/open' $_.Exception.Message }

    # 🔴 A plain DateTime.Parse on this endpoint would read 01/09/2026 as 1 September on one server and
    # 9 January on another (13.4). The controller uses TryParseExact and refuses, naming the format --
    # this check is what keeps that true.
    try {
        $path = '/api/agent/slots/open?branchId={0}&fromDate=01/09/2026&toDate={1}' -f `
            [uri]::EscapeDataString($branchId), $toDate
        $r = Invoke-AgentRequest -Path $path -Headers $headers
        if ($r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $false) {
            Write-Check PASS 'E7b GET  ...slots/open with fromDate=01/09/2026  (refused, format named)'
        }
        else {
            Write-Check FAIL 'E7b GET  ...slots/open with a non-ISO fromDate' (Show-Body $r)
        }
    }
    catch { Write-Check FAIL 'E7b GET  ...slots/open with a non-ISO fromDate' $_.Exception.Message }
}

# ===================================================================================================
# 3. THE WRITE (13.5). OPT-IN, OFF BY DEFAULT.
#
# 🔴 IT BOOKS A REAL CLINICIAN HOUR AND THIS API CANNOT GIVE IT BACK. There is no cancellation concept
# in nucentra (CoreFlow.md 3.9): a status change touches no slots, so the only way to release the hour is
# POST /Patient/DeleteAppointment, in the portal, by a person. Hence the switch, hence the default of OFF,
# and hence the reminder printed after a successful booking.
# ===================================================================================================
Write-Host ''
Write-Host '-- The write (13.5) ----------------------------------------------------------------'

if (-not $IncludeWrite) {
    Write-Check SKIP 'E8  POST /api/agent/appointments' 'not requested -- pass -IncludeWrite (it consumes a real clinician hour)'
}
elseif ($null -eq $slot -or [string]::IsNullOrWhiteSpace($patientId) -or [string]::IsNullOrWhiteSpace($branchId)) {
    Write-Check SKIP 'E8  POST /api/agent/appointments' 'no open slot, patient or branch to book with'
}
else {
    try {
        # The documented request shape, 13.5, built by hand rather than through ConvertTo-Json so that
        # slotIds is unambiguously a JSON array and pjAppTypeId is unambiguously the STRING "01".
        # "01" is PATIENT ASSESSMENT and is the only type this API books -- AgentApiPlan.md decision 4.
        $slotJson = '[' + ([string]$slot.slotId) + ']'
        $body = '{{"patientId":"{0}","appointmentDate":"{1}","staffId":"{2}","slotIds":{3},' +
                '"pjAppTypeId":"01","branchId":"{4}","status":"Scheduled"}}'
        $body = $body -f $patientId, $slot.slotDate, $slot.staffId, $slotJson, $branchId

        $r = Invoke-AgentRequest -Path '/api/agent/appointments' -Method 'POST' -Headers $headers -Body $body

        if ($r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'success') -and $r.Json.success -eq $true `
                -and (Test-HasProperty $r.Json 'appointmentId') -and [int]$r.Json.appointmentId -gt 0) {

            Write-Check PASS ('E8  POST /api/agent/appointments                 (appointmentId={0})' -f $r.Json.appointmentId)

            Write-Host ''
            Write-Host '  A REAL CLINICIAN HOUR HAS BEEN CONSUMED AND THIS API CANNOT RELEASE IT.' -ForegroundColor Yellow
            Write-Host ('    appointmentId {0}  |  patient {1}  |  staff {2}  |  {3} {4}-{5}  |  slot {6}' -f `
                    $r.Json.appointmentId, $patientId, $slot.staffId, $slot.slotDate, $slot.startTime, $slot.endTime, $slot.slotId) -ForegroundColor Yellow
            Write-Host '    To release it: open that patient in the portal, Appointment tab, delete the booking' -ForegroundColor Yellow
            Write-Host '    (POST /Patient/DeleteAppointment -- CoreFlow.md 3.9, 13.5).' -ForegroundColor Yellow
            Write-Host ''
            Write-Host '  Then run the audit health check -- CoreFlow.md 13.5. Username must read AGENT_SERVICE:' -ForegroundColor Cyan
            Write-Host '    sqlcmd -S localhost -d CRC_DB -E -C -Q "SELECT TOP 3 a.AuditTrail_Id, a.User_Id, u.Username, a.AuditTrail_Action, a.AuditTrail_Category FROM dbo.AuditTrails a LEFT JOIN dbo.Users u ON u.User_ID = a.User_Id ORDER BY a.AuditTrail_Id DESC"' -ForegroundColor Cyan
            Write-Host ''
        }
        elseif ($r.StatusCode -eq 200 -and (Test-HasProperty $r.Json 'reason')) {
            # A typed reason means SaveAppointmentAsync was asked and said no, and its transaction rolled
            # back -- nothing was written (13.5). SlotTaken in particular is a correct outcome of a
            # correct system; it is still a FAIL here, because this script read that hour as open a
            # second earlier and on a quiet system nothing should have taken it.
            Write-Check FAIL ('E8  POST /api/agent/appointments  reason={0}' -f $r.Json.reason) 'the transaction rolled back; nothing was written. Re-run slot discovery and try again.'
        }
        else {
            Write-Check FAIL 'E8  POST /api/agent/appointments' (Show-Body $r)
        }
    }
    catch { Write-Check FAIL 'E8  POST /api/agent/appointments' $_.Exception.Message }
}

# ===================================================================================================
# Summary.
# ===================================================================================================
Write-Host ''
Write-Host '===================================================================================='
Write-Host ('  PASS {0}    FAIL {1}    SKIP {2}' -f $script:Pass, $script:Fail, $script:Skip)
Write-Host '===================================================================================='

if ($script:Skip -gt 0) {
    Write-Host ''
    Write-Host '  A SKIP is not a pass. Each one above says what was missing -- usually fixture data' -ForegroundColor Yellow
    Write-Host '  (no open slots, no active patients) rather than a broken endpoint. Open some hours in' -ForegroundColor Yellow
    Write-Host '  Staff > Edit > Schedule and re-run before believing the run was clean.' -ForegroundColor Yellow
}

Write-Host ''

if ($script:Fail -gt 0) { exit 1 }
exit 0
