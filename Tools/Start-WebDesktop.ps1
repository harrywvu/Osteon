param(
    [int]$Port = 8000,
    [string]$BuildPath = (Join-Path $PSScriptRoot '../Builds/Web Desktop Optimized')
)

$ErrorActionPreference = 'Stop'
if ($Port -lt 1024 -or $Port -gt 65535) { throw 'Use a port between 1024 and 65535.' }
$webRoot = (Resolve-Path -LiteralPath $BuildPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $webRoot 'index.html'))) {
    throw 'Build the Unity WebGL player into the selected folder first.'
}
# shortcut: current output is uncompressed; configure encoding headers before serving gzip/Brotli builds.
if (Get-ChildItem -LiteralPath (Join-Path $webRoot 'Build') -File | Where-Object { $_.Name -match '\.(gz|br)$' }) {
    throw 'This simple server requires an uncompressed build or Unity Decompression Fallback.'
}
$pythonPath = (Get-Command python -CommandType Application | Select-Object -First 1).Source
$webLogs = Join-Path $PSScriptRoot '../Logs'
New-Item -ItemType Directory -Path $webLogs -Force | Out-Null
$webProcess = Start-Process -FilePath $pythonPath -ArgumentList @(
    '-m', 'http.server', [string]$Port, '--bind', '127.0.0.1', '--directory', ('"{0}"' -f $webRoot)
) -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $webLogs "web-desktop-$Port.out.log") `
    -RedirectStandardError (Join-Path $webLogs "web-desktop-$Port.err.log")
if ($webProcess.WaitForExit(500)) { throw "Web server exited. Check Logs/web-desktop-$Port.err.log (port may already be in use)." }
[pscustomobject]@{ URL = "http://localhost:$Port"; ProcessId = $webProcess.Id; Directory = $webRoot }
