$ErrorActionPreference='Stop'
$base=$PSScriptRoot;$marker=Join-Path $base 'active_session.txt'
if(!(Test-Path -LiteralPath $marker)){Write-Output 'No active manual playtest session.';return}
$out=(Get-Content -LiteralPath $marker -Raw).Trim();$root=[IO.Path]::GetFullPath((Join-Path $base 'sessions'))+'\'
if(![IO.Path]::GetFullPath($out).StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected session path; no process or file changed'}
$state=Get-Content -LiteralPath (Join-Path $out 'state.json') -Raw|ConvertFrom-Json
$icey=$state.installRoots.ICEY;$dc=$state.installRoots.DC
'stop'|Set-Content -LiteralPath (Join-Path $out 'control.txt')
$closeObservations=@()
foreach($record in $state.owned){$p=Get-Process -Id $record.pid -ErrorAction SilentlyContinue;$matched=$p -and $p.ProcessName -eq $record.name -and $p.StartTime.ToUniversalTime().ToString('o') -eq $record.startUtc;$sent=$false;if($matched){$sent=$p.CloseMainWindow()};$closeObservations+=@{pid=$record.pid;name=$record.name;runningAndIdentityMatchedBeforeClose=[bool]$matched;closeRequestSent=$sent}}
for($i=0;$i -lt 20;$i++){if(!(Get-Process ICEY,DeadCellsModding -ErrorAction SilentlyContinue)){break};Start-Sleep -Milliseconds 500}
if(Get-Process ICEY,DeadCellsModding -ErrorAction SilentlyContinue){throw 'A game remains open. Close it normally and run Stop-Playtest again; no force termination was used.'}
$archive=Join-Path $out ('closed_'+[datetime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'));New-Item -ItemType Directory -Path $archive|Out-Null
@{utc=[datetime]::UtcNow.ToString('o');observations=$closeObservations;note='An already absent process is not evidence of a normal exit; inspect retained logs for crashes.'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $archive 'close_observations.json')
foreach($log in @(@{path=(Join-Path $icey 'BepInEx\LogOutput.log');name='icey_final.log'},@{path=(Join-Path $dc 'coremod\logs\log_latest.log');name='dc_final.log'})){if(Test-Path -LiteralPath $log.path){Copy-Item -LiteralPath $log.path -Destination (Join-Path $archive $log.name)}}
$relay=Get-CimInstance Win32_Process -Filter "ProcessId=$($state.relayPid)" -ErrorAction SilentlyContinue
if($relay -and $relay.CommandLine.Contains((Join-Path $base 'relay.py')) -and $relay.CommandLine.Contains($out)){Stop-Process -Id $state.relayPid}
$rows=@();foreach($f in (Get-Content -LiteralPath (Join-Path $base 'manifest.json') -Raw|ConvertFrom-Json).originalFiles){$f|Add-Member -Force -NotePropertyName path -NotePropertyValue (Join-Path $state.installRoots.($f.game) $f.relativePath);$actual=(Get-FileHash -LiteralPath $f.path -Algorithm SHA256).Hash;$rows+=[pscustomobject]@{path=$f.path;expected=$f.expected;actual=$actual;matches=$actual -eq $f.expected}}
$rows|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $archive 'original_hashes_end.json')
if($rows.Count -ne 9 -or @($rows|Where-Object {!$_.matches}).Count){throw 'Original fingerprint mismatch; evidence retained'}
Move-Item -LiteralPath $marker -Destination (Join-Path $archive 'active_session.txt')
Write-Output "Session archived; originals 9/9; logs/backups and close observations retained. Playtest plugins remain installed. $out"
