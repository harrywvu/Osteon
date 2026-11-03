param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$accountOutput = Join-Path $PSScriptRoot 'dist'
$accountPackage = Join-Path $accountOutput ('package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $accountPackage -Force | Out-Null
& $Python -m pip install -r (Join-Path $PSScriptRoot 'requirements.txt') `
    --platform manylinux_2_28_x86_64 --platform manylinux2014_x86_64 `
    --python-version 3.12 --implementation cp --only-binary=:all: `
    --target $accountPackage --disable-pip-version-check
if ($LASTEXITCODE -ne 0) { throw 'Linux dependency installation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'lambda_function.py') -Destination $accountPackage
$accountZip = Join-Path $accountOutput 'osteon-accounts-lambda.zip'
# Python zipfile includes native bindings and dotfiles without PowerShell archive filtering.
& $Python -c 'import pathlib,sys,zipfile; p=pathlib.Path(sys.argv[1]); z=zipfile.ZipFile(sys.argv[2],"w",zipfile.ZIP_DEFLATED); [z.write(f,f.relative_to(p).as_posix()) for f in p.rglob("*") if f.is_file() and "__pycache__" not in f.parts and f.suffix!=".pyc"]; z.close()' $accountPackage $accountZip
if ($LASTEXITCODE -ne 0) { throw 'ZIP creation failed.' }
Write-Output "Created $accountZip"
Write-Output 'Runtime: Python 3.12; architecture: x86_64; handler: lambda_function.lambda_handler'
