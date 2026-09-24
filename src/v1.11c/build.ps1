param([switch]$Test)
$ErrorActionPreference = 'Stop'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msbuild)) { throw 'Visual Studio MSBuild is required.' }
$temporary = Join-Path $PSScriptRoot '..\..\work\temp'
New-Item -ItemType Directory -Path $temporary -Force | Out-Null
$env:TEMP = (Resolve-Path $temporary).Path
$env:TMP = $env:TEMP
& $msbuild (Join-Path $PSScriptRoot 'HIKI-Notifier.csproj') /p:Configuration=Release /p:Platform=x64 /verbosity:minimal /nologo /nodeReuse:false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Test) {
  & $msbuild (Join-Path $PSScriptRoot 'Tests\Tests.csproj') /p:Configuration=Release /verbosity:minimal /nologo /nodeReuse:false
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  & (Join-Path $PSScriptRoot 'Tests\bin\Release\HIKI Logic Checks.exe')
  exit $LASTEXITCODE
}
