param([string]$ConfigFile)
. (Join-Path $PSScriptRoot 'Common.ps1')
$settings=Read-LocalPaths $ConfigFile
$originals=@(Get-VerifiedOriginals $settings)
if(!(Get-Command cl.exe -ErrorAction SilentlyContinue) -or $env:VSCMD_ARG_TGT_ARCH -ne 'x64'){throw 'Run PowerShell from x64 Native Tools Command Prompt for VS 2022 (C++ and Windows SDK installed).'}
$sdk=& dotnet --version
if($LASTEXITCODE -ne 0 -or $sdk -notmatch '^10\.'){throw 'The build requires .NET SDK 10.x'}
$mdk=Join-Path $settings.DeadCellsGameDir 'coremod\core\mdk'
if(!(Test-Path -LiteralPath (Join-Path $mdk 'build\build.props'))){throw 'DCCM MDK is missing; install MDK through your tested DCCM setup.'}
$out=Join-Path $RepositoryRoot 'out';New-Item -ItemType Directory -Path $out -Force|Out-Null
Start-Transcript -Path (Join-Path $out ('build_'+[datetime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'.log'))|Out-Null
$oldMdk=$env:DCCM_MDK_ROOT;$oldRoot=$env:DCCM_ROOT;$oldGame=$env:DEAD_CELLS_GAME_PATH
try{
 $env:DCCM_MDK_ROOT=$mdk;$env:DCCM_ROOT=(Join-Path $settings.DeadCellsGameDir 'coremod')+[IO.Path]::DirectorySeparatorChar;$env:DEAD_CELLS_GAME_PATH=$settings.DeadCellsGameDir
 & dotnet build (Join-Path $RepositoryRoot 'src\icey\InputAction.csproj') -c Release -p:Platform=AnyCPU "-p:IceyGameDir=$($settings.IceyGameDir)"
 if($LASTEXITCODE -ne 0){throw 'ICEY build failed; no deployment performed'}
 & dotnet build (Join-Path $RepositoryRoot 'src\dc\DcInput.csproj') -c Release -p:Platform=AnyCPU
 if($LASTEXITCODE -ne 0){throw 'DC build failed; no deployment performed'}
 $native=Join-Path $out 'native';New-Item -ItemType Directory -Path $native -Force|Out-Null
 Push-Location $native
 try{foreach($name in @('ep4_cpu','ep5_hud','ep6_dash')){
  & cl.exe /nologo /LD /MT /O2 /EHsc (Join-Path $RepositoryRoot "src\native\$name.cpp") /link "/OUT:$name.dll"
  if($LASTEXITCODE -ne 0){throw "Native build failed: $name"}
 }}finally{Pop-Location}
 $artifacts=@('src\icey\bin\Release\IceyEarlyPlaytestAction.dll','src\dc\bin\Release\net10.0\output\IceyEarlyPlaytestInput\IceyEarlyPlaytestInput.dll','out\native\ep4_cpu.dll','out\native\ep5_hud.dll','out\native\ep6_dash.dll')
 $hashes=@();foreach($relative in $artifacts){$hashes+=@{path=$relative;sha256=(Get-FileHash -LiteralPath (Join-Path $RepositoryRoot $relative)).Hash.ToLowerInvariant()}}
 @{utc=[datetime]::UtcNow.ToString('o');version='1.0.3-playtest';originalFiles=$originals;artifacts=$hashes}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $out 'build-manifest.json') -Encoding UTF8
 Write-Output 'Build completed; five self-authored DLLs ready; nine original fingerprints 9/9. No game deployment.'
}finally{$env:DCCM_MDK_ROOT=$oldMdk;$env:DCCM_ROOT=$oldRoot;$env:DEAD_CELLS_GAME_PATH=$oldGame;Stop-Transcript|Out-Null}
