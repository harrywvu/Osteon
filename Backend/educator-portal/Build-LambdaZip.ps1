param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$package = Join-Path $source 'dist'
New-Item -ItemType Directory -Force -Path $package | Out-Null
$zip = Join-Path $package 'osteon-educator-lambda.zip'
& $Python -c 'import pathlib,sys,zipfile; root=pathlib.Path(sys.argv[1]); out=pathlib.Path(sys.argv[2]); paths={"api.py":root/"api.py","signup_guard.py":root/"signup_guard.py","question_bank.py":root.parent/"quiz-api"/"question_bank.py","program_catalog.py":root.parent/"accounts-api"/"program_catalog.py"}; z=zipfile.ZipFile(out,"w",zipfile.ZIP_DEFLATED); [z.write(path,name) for name,path in paths.items()]; z.close()' $source $zip
if ($LASTEXITCODE -ne 0) { throw 'Educator ZIP packaging failed.' }
Write-Output "Created $zip"
