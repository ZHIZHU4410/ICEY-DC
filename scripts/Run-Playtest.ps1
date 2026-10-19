param([switch]$VerifyOnly,[switch]$PreflightOnly)
$ErrorActionPreference='Stop'
function Get-BridgeFileHash([string]$LiteralPath,[string]$Algorithm='SHA256'){
 $stream=[IO.File]::OpenRead($LiteralPath);$sha=[Security.Cryptography.SHA256]::Create()
 try{[pscustomobject]@{Hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}}
 finally{$sha.Dispose();$stream.Dispose()}
}
$base=$PSScriptRoot
$settings=Import-PowerShellDataFile (Join-Path $base 'paths.psd1')
$icey=$settings.IceyGameDir;$dc=$settings.DeadCellsGameDir
$gameRoots=@{ICEY=$icey;DC=$dc}
$startupMutex=$null;$startupMutexOwned=$false
function Clear-BridgeStaleSessions {
 # Games own live sessions. Never interrupt one just to make another launcher work.
 if(Get-Process ICEY,DeadCellsModding -ErrorAction SilentlyContinue){throw 'A game is still running. Close ICEY and Dead Cells normally, then retry; no live session was cleaned.'}
 $packages=@(Get-ChildItem -LiteralPath (Split-Path -Parent $base) -Directory)
 $relayProcesses=@(Get-CimInstance Win32_Process -Filter "Name='python.exe'" -ErrorAction Stop)
 foreach($package in $packages){
  $packagePath=$package.FullName;$marker=Join-Path $packagePath 'active_session.txt'
  if(!(Test-Path -LiteralPath (Join-Path $packagePath 'manifest.json')) -or !(Test-Path -LiteralPath (Join-Path $packagePath 'relay.py'))){continue}
  $sessions=@()
  if(Test-Path -LiteralPath $marker){$sessions+=([IO.Path]::GetFullPath((Get-Content -LiteralPath $marker -Raw).Trim()))}
  # Also find relays orphaned by a failed launch or a missing active-session marker.
  $exe=Join-Path $packagePath 'runtime\python.exe';$script=Join-Path $packagePath 'relay.py'
  $orphanPattern='^\s*"'+[regex]::Escape($exe)+'"\s+-I\s+"'+[regex]::Escape($script)+'"\s+"(?<session>[^"]+)"\s*$'
  foreach($candidate in $relayProcesses){if([string]::Equals($candidate.ExecutablePath,$exe,[StringComparison]::OrdinalIgnoreCase) -and $candidate.CommandLine -match $orphanPattern){$sessions+=([IO.Path]::GetFullPath($Matches.session))}}
  foreach($session in @($sessions|Select-Object -Unique)){
  $sessionRoot=[IO.Path]::GetFullPath((Join-Path $packagePath 'sessions'))+'\'
  if(!$session.StartsWith($sessionRoot,[StringComparison]::OrdinalIgnoreCase)){throw "Invalid bridge session marker: $marker; no cleanup attempted"}
  $statePath=Join-Path $session 'state.json'
  if(!(Test-Path -LiteralPath $statePath)){throw "Missing bridge session state: $statePath; preserve for inspection"}
  $state=Get-Content -LiteralPath $statePath -Raw|ConvertFrom-Json
  if(![string]::Equals([IO.Path]::GetFullPath($state.out),$session,[StringComparison]::OrdinalIgnoreCase)){throw 'Bridge state/marker disagreement; no cleanup attempted'}
  $relayId=[int]$state.relayPid;$result='relay already absent';$matched=$false
  $processInfo=if($relayId -gt 0){Get-CimInstance Win32_Process -Filter "ProcessId=$relayId" -ErrorAction Stop}else{$null}
  if($processInfo){
   $exe=Join-Path $packagePath 'runtime\python.exe';$script=Join-Path $packagePath 'relay.py'
   $expected='^\s*"'+[regex]::Escape($exe)+'"\s+-I\s+"'+[regex]::Escape($script)+'"\s+"'+[regex]::Escape($session)+'"\s*$'
   $pidFile=Join-Path $session 'relay_pid.txt'
   $matched=$processInfo.Name -eq 'python.exe' -and [string]::Equals($processInfo.ExecutablePath,$exe,[StringComparison]::OrdinalIgnoreCase) -and $processInfo.CommandLine -match $expected -and (Test-Path -LiteralPath $pidFile) -and (Get-Content -LiteralPath $pidFile -Raw).Trim() -eq [string]$relayId
   if(!$matched){$result='recorded PID belongs to another process; left untouched'}
  }
  'stop'|Set-Content -LiteralPath (Join-Path $session 'control.txt')
  if($matched){
   $deadline=[datetime]::UtcNow.AddSeconds(2)
   do{Start-Sleep -Milliseconds 100;$live=Get-CimInstance Win32_Process -Filter "ProcessId=$relayId" -ErrorAction Stop}while($live -and $live.CreationDate -eq $processInfo.CreationDate -and [datetime]::UtcNow -lt $deadline)
   if($live -and $live.CreationDate -eq $processInfo.CreationDate){
    # Legacy relays ignore control.txt. Re-check identity immediately before exact-PID termination.
    if($live.CommandLine -ne $processInfo.CommandLine -or $live.ExecutablePath -ne $processInfo.ExecutablePath){throw 'Relay identity changed during cleanup; process left untouched'}
    Stop-Process -Id $relayId -ErrorAction Stop
    $result='stale legacy relay stopped after control timeout'
   }else{$result='relay exited on stop control'}
   for($attempt=0;$attempt -lt 20;$attempt++){$remaining=Get-CimInstance Win32_Process -Filter "ProcessId=$relayId" -ErrorAction Stop;if(!$remaining -or $remaining.CreationDate -ne $processInfo.CreationDate){break};Start-Sleep -Milliseconds 100}
   if($remaining -and $remaining.CreationDate -eq $processInfo.CreationDate){throw "Stale bridge relay $relayId did not exit; session preserved"}
  }
  $archive=Join-Path $session ('startup_cleanup_'+[datetime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'_'+[guid]::NewGuid().ToString('N'))
  if(![IO.Path]::GetFullPath($archive).StartsWith($sessionRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid cleanup archive path'}
  New-Item -ItemType Directory -Path $archive|Out-Null
  @{utc=[datetime]::UtcNow.ToString('o');relayPid=$relayId;identityMatched=$matched;result=$result;session=$session}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $archive 'cleanup.json') -Encoding UTF8
  if((Test-Path -LiteralPath $marker) -and [string]::Equals([IO.Path]::GetFullPath((Get-Content -LiteralPath $marker -Raw).Trim()),$session,[StringComparison]::OrdinalIgnoreCase)){Move-Item -LiteralPath $marker -Destination (Join-Path $archive 'active_session.txt')}
  Write-Output "Previous bridge session cleaned: $($package.Name); $result"
  }
 }
}
New-Item -ItemType Directory -Path (Join-Path $base 'logs') -Force|Out-Null
Start-Transcript -Path (Join-Path $base ('logs\preflight_'+[datetime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'.log'))|Out-Null
try {
 $manifest=Get-Content -LiteralPath (Join-Path $base 'manifest.json') -Raw|ConvertFrom-Json
 foreach($original in $manifest.originalFiles){$original|Add-Member -Force -NotePropertyName path -NotePropertyValue (Join-Path $gameRoots[$original.game] $original.relativePath)}
 foreach($f in $manifest.payload){$p=Join-Path $base $f.path;if(!(Test-Path -LiteralPath $p) -or (Get-BridgeFileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $f.sha256){throw "Package mismatch: $p. Extract the complete manual playtest package again."}}
 foreach($f in $manifest.originalFiles){if(!(Test-Path -LiteralPath $f.path) -or (Get-BridgeFileHash -LiteralPath $f.path -Algorithm SHA256).Hash -ne $f.expected){throw "Unsupported game/loader build: $($f.path). Expected SHA256 $($f.expected). Use the tested ICEY Unity 5.4.0f3 / DCCM 35.13.9.0 / DC 35 builds or repeat compatibility validation. No original game file was changed."}}
 & (Join-Path $base 'runtime\python.exe') -I -c 'import socket,threading,json'
 if($LASTEXITCODE -ne 0){throw 'Bundled runtime failed'}
 Write-Output 'Bridge runtime OK'
 if($VerifyOnly){Write-Output 'Manual package verified; nine original fingerprints 9/9; no launch or deployment.';return}
 $startupMutex=[Threading.Mutex]::new($false,'Local\IceyDCBridgeLaunch42250')
 try{$startupMutexOwned=$startupMutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$startupMutexOwned=$true}
 if(!$startupMutexOwned){throw 'Another bridge launcher is starting. Wait for it to finish; no cleanup or deployment performed.'}
 Clear-BridgeStaleSessions
 # Check after cleaning known bridge leftovers, before any deployment.
 $portProbe=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,42250)
 try{$portProbe.Server.ExclusiveAddressUse=$true;$portProbe.Start()}
 catch{throw 'Port 127.0.0.1:42250 remains occupied by an unverified process. Known bridge leftovers were checked; unrelated processes were left untouched. No new session or plugin deployment occurred.'}
 finally{$portProbe.Stop()}
 if($PreflightOnly){Write-Output 'Startup cleanup passed; port 42250 free; no game launch or deployment.';return}
 if(Get-Process ICEY,DeadCellsModding -ErrorAction SilentlyContinue){throw 'Close both games normally before starting this playtest.'}
 $marker=Join-Path $base 'active_session.txt'
 if(Test-Path -LiteralPath $marker){throw 'Finish the previous session with Stop-Playtest.cmd first.'}

 $out=Join-Path $base ('sessions\'+[datetime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
 New-Item -ItemType Directory -Path (Join-Path $out 'backup')|Out-Null
 $entries=@(
  @{target=(Join-Path $icey 'BepInEx\plugins\IceyEarlyPlaytestAction.dll');source='payload\icey\IceyEarlyPlaytestAction.dll';name='icey_plugin'},
  @{target=(Join-Path $icey 'BepInEx\plugins\ep4_session.txt');source=$null;name='icey_marker'},
  @{target=(Join-Path $icey 'ep4_cpu.dll');source='payload\native\ep4_cpu.dll';name='native_cpu'},
  @{target=(Join-Path $icey 'ep5_hud.dll');source='payload\native\ep5_hud.dll';name='native_hud'},
  @{target=(Join-Path $icey 'ep6_dash.dll');source='payload\native\ep6_dash.dll';name='native_dash'},
  @{target=(Join-Path $dc 'coremod\mods\IceyEarlyPlaytestInput');source='payload\dc\IceyEarlyPlaytestInput';name='dc_mod'}
 )
 $state=[pscustomobject]@{utc=[datetime]::UtcNow.ToString('o');out=$out;entries=$entries;relayPid=0;iceyPid=0;dcPid=0;owned=@();installRoots=$gameRoots}
 $state|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $out 'state.json') -Encoding UTF8
 $out|Set-Content -LiteralPath $marker -Encoding UTF8
 $saveIndex=0
 foreach($savePath in $settings.SaveDirectories){if(!(Test-Path -LiteralPath $savePath)){throw "Configured save directory not found: $savePath"};$saveIndex++;Copy-Item -LiteralPath $savePath -Destination (Join-Path $out ('backup\save_'+$saveIndex)) -Recurse}
 @($settings.SaveDirectories)|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'save_backup_sources.json') -Encoding UTF8
 foreach($previous in @(@{path=(Join-Path $icey 'BepInEx\LogOutput.log');name='previous_icey.log'},@{path=(Join-Path $dc 'coremod\logs\log_latest.log');name='previous_dc.log'})){if(Test-Path -LiteralPath $previous.path){Copy-Item -LiteralPath $previous.path -Destination (Join-Path $out ('backup\'+$previous.name))}}
 foreach($entry in $entries){
  if(Test-Path -LiteralPath $entry.target){$resolved=(Resolve-Path -LiteralPath $entry.target).Path;if(![string]::Equals($resolved,$entry.target,[StringComparison]::OrdinalIgnoreCase)){throw "Unexpected resolved install target: $resolved"};Move-Item -LiteralPath $entry.target -Destination (Join-Path $out ('backup\'+$entry.name))}
  if($entry.source){Copy-Item -LiteralPath (Join-Path $base $entry.source) -Destination $entry.target -Recurse}
 }
 'run'|Set-Content -LiteralPath (Join-Path $out 'control.txt')
 'native DC terrain'|Set-Content -LiteralPath (Join-Path $out 'world.flag')
 'persistent native arena'|Set-Content -LiteralPath (Join-Path $out 'arena.flag')
 'manual combat; native colliders only'|Set-Content -LiteralPath (Join-Path $out 'combat.flag')
 'manual user test; real native defense pipeline'|Set-Content -LiteralPath (Join-Path $out 'defense.flag')
 $out|Set-Content -LiteralPath (Join-Path $icey 'BepInEx\plugins\ep4_session.txt') -Encoding UTF8
 $out|Set-Content -LiteralPath (Join-Path $dc 'coremod\mods\IceyEarlyPlaytestInput\ep4_session.txt') -Encoding UTF8
 try {
  $relay=Start-Process -FilePath (Join-Path $base 'runtime\python.exe') -ArgumentList ('-I "'+(Join-Path $base 'relay.py')+'" "'+$out+'"') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'relay_stdout.log') -RedirectStandardError (Join-Path $out 'relay_stderr.log');$state.relayPid=$relay.Id
  $relayReady=$false
  for($attempt=0;$attempt -lt 30;$attempt++){
   Start-Sleep -Milliseconds 100;$relay.Refresh()
   if($relay.HasExited){$relayError=Get-Content -LiteralPath (Join-Path $out 'relay_stderr.log') -Raw -ErrorAction SilentlyContinue;throw "Relay exited: $relayError"}
   $pidMarker=Join-Path $out 'relay_pid.txt'
   if((Test-Path -LiteralPath $pidMarker) -and (Get-Content -LiteralPath $pidMarker -Raw).Trim() -eq [string]$relay.Id){$relayReady=$true;break}
  }
  if(!$relayReady){throw 'Relay did not report readiness within 3 seconds; inspect relay_stderr.log'}
  Start-Process 'steam://rungameid/553640'
  $hostGame=Start-Process -FilePath (Join-Path $dc 'coremod\core\host\startup\DeadCellsModding.exe') -WorkingDirectory $dc -PassThru;$state.dcPid=$hostGame.Id
  for($i=0;$i -lt 30;$i++){$guestGame=Get-Process ICEY -ErrorAction SilentlyContinue;if($guestGame){$state.iceyPid=$guestGame.Id;break};Start-Sleep -Milliseconds 500}
  if(!$state.iceyPid){throw 'ICEY did not start through Steam; inspect Steam and preserve this session.'}
  foreach($ownedId in @($state.iceyPid,$state.dcPid)){$p=Get-Process -Id $ownedId;$state.owned+=@{pid=$p.Id;name=$p.ProcessName;startUtc=$p.StartTime.ToUniversalTime().ToString('o')}}
  Write-Output "Manual playtest ready. Enter a level in ICEY and DC, keep DC foreground. F7 switches ICEY / native Hero. Native bindings are written to native_keymap.csv. Combat remains experimental; see README. Logs: $out"
 } catch {
  $_|Out-String|Set-Content -LiteralPath (Join-Path $out 'startup_failure.txt')
  if($state.iceyPid -eq 0 -and $state.dcPid -eq 0){
   'stop'|Set-Content -LiteralPath (Join-Path $out 'control.txt')
   if($relay -and !$relay.HasExited){$null=$relay.WaitForExit(1500)}
   if(Test-Path -LiteralPath $marker){Move-Item -LiteralPath $marker -Destination (Join-Path $out 'failed_active_session.txt')}
  }
  throw
 } finally {$state|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $out 'state.json') -Encoding UTF8;Copy-Item -LiteralPath (Join-Path $out 'state.json') -Destination (Join-Path $out 'launch.json')}
} finally {if($startupMutexOwned){$startupMutex.ReleaseMutex()};if($startupMutex){$startupMutex.Dispose()};Stop-Transcript|Out-Null}
