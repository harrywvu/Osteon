param(
    [string]$Python = "python",
    [string]$IndexUrl = "https://pypi.org/simple"
)

$ErrorActionPreference = "Stop"
$source = $PSScriptRoot
$output = Join-Path $source "dist"
$package = Join-Path ([System.IO.Path]::GetTempPath()) ("osteon-quiz-" + [guid]::NewGuid().ToString("N"))
$zip = Join-Path $output "osteon-quiz-lambda.zip"

New-Item -ItemType Directory -Path $package, $output -Force | Out-Null

# Lambda Python 3.12 uses Amazon Linux 2023. Install Linux x86-64 wheels even
# when this packaging script runs on Windows; Windows wheels cannot run there.
& $Python -m pip install -r (Join-Path $source "requirements.txt") `
    --index-url $IndexUrl `
    --platform manylinux_2_28_x86_64 `
    --platform manylinux2014_x86_64 `
    --python-version 3.12 `
    --implementation cp `
    --only-binary=:all: `
    --target $package
if ($LASTEXITCODE -ne 0) { throw "Dependency installation failed." }

Copy-Item (Join-Path $source "handler.py") $package
Copy-Item (Join-Path $source "quiz_engine.py") $package
Copy-Item (Join-Path $source "quiz_service.py") $package
Copy-Item (Join-Path $source "dynamodb_store.py") $package
Copy-Item (Join-Path $source "assets") $package -Recurse

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip }
Compress-Archive -Path (Join-Path $package "*") -DestinationPath $zip -CompressionLevel Optimal
Write-Output "Created $zip"
Write-Output "ZIP size: $([math]::Round((Get-Item $zip).Length / 1MB, 2)) MiB"
Write-Output "Lambda handler: handler.lambda_handler; runtime: Python 3.12; architecture: x86_64"
