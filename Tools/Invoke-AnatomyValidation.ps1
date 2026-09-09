param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.2f1/Editor/Unity.exe',
    [switch]$Pilot,
    [switch]$Capture
)

$ErrorActionPreference = 'Stop'
$validationProject = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw "Unity editor not found: $UnityEditor" }
if ((Get-Content -LiteralPath (Join-Path $validationProject 'ProjectSettings/ProjectVersion.txt') -Raw) -notmatch 'm_EditorVersion: 6000\.3\.2f1') {
    throw 'This validation requires Unity 6000.3.2f1.'
}
$validationLogs = Join-Path $validationProject 'Logs'
New-Item -ItemType Directory -Path $validationLogs -Force | Out-Null
$validationLog = Join-Path $validationLogs 'G3-validation-editor.log'
$validationReport = Join-Path $validationLogs 'G3-validation.json'
$validationStart = Get-Date
$validationMethod = if ($Pilot) { 'AnatomyValidation.RunPilot' } else { 'AnatomyValidation.RunAll' }
$validationArgs = @('-batchmode', '-projectPath', ('"' + $validationProject + '"'),
    '-executeMethod', $validationMethod, '-logFile', ('"' + $validationLog + '"'))
if (-not $Capture) { $validationArgs += '-nographics' }
$validationProcess = Start-Process -FilePath $UnityEditor -ArgumentList $validationArgs -WindowStyle Hidden -PassThru -Wait
if ($validationProcess.ExitCode -ne 0) { throw "Unity validation failed. See $validationLog" }
if (-not (Test-Path -LiteralPath $validationReport) -or
    (Get-Item -LiteralPath $validationReport).LastWriteTime -lt $validationStart) {
    throw "Unity did not produce a fresh validation report. See $validationLog"
}
$validationResult = Get-Content -LiteralPath $validationReport -Raw | ConvertFrom-Json
if (-not $validationResult.passed) { throw $validationResult.error }
$validationResult
