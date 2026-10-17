param([switch]$VerifyOnly)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
$base=$PSScriptRoot
$settings=Import-PowerShellDataFile (Join-Path $base 'paths.psd1')
$icey=$settings.IceyGameDir;$dc=$settings.DeadCellsGameDir
$gameRoots=@{ICEY=$icey;DC=$dc}
New-Item -ItemType Directory -Path (Join-Path $base 'logs') -Force|Out-Null
Start-Transcript -Path (Join-Path $base ('logs\preflight_'+[datetime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'.log'))|Out-Null
try {
 $manifest=Get-Content -LiteralPath (Join-Path $base 'manifest.json') -Raw|ConvertFrom-Json
 foreach($original in $manifest.originalFiles){$original|Add-Member -Force -NotePropertyName path -NotePropertyValue (Join-Path $gameRoots[$original.game] $original.relativePath)}
 foreach($f in $manifest.payload){$p=Join-Path $base $f.path;if(!(Test-Path -LiteralPath $p) -or (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $f.sha256){throw "Package mismatch: $p. Extract the complete manual playtest package again."}}
 foreach($f in $manifest.originalFiles){if(!(Test-Path -LiteralPath $f.path) -or (Get-FileHash -LiteralPath $f.path -Algorithm SHA256).Hash -ne $f.expected){throw "Unsupported game/loader build: $($f.path). Expected SHA256 $($f.expected). Use the tested ICEY Unity 5.4.0f3 / DCCM 35.13.8.0 / DC 35 builds or repeat compatibility validation. No original game file was changed."}}
 & (Join-Path $base 'runtime\python.exe') -I -c 'import socket,threading,json; print(314)'
 if($LASTEXITCODE -ne 0){throw 'Bundled runtime failed'}
 if($VerifyOnly){Write-Output 'Manual package verified; nine original fingerprints 9/9; no launch or deployment.';return}
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
  Start-Sleep -Milliseconds 400;if($relay.HasExited){throw 'Relay exited; inspect relay_stderr.log'}
  Start-Process 'steam://rungameid/553640'
  $hostGame=Start-Process -FilePath (Join-Path $dc 'coremod\core\host\startup\DeadCellsModding.exe') -WorkingDirectory $dc -PassThru;$state.dcPid=$hostGame.Id
  for($i=0;$i -lt 30;$i++){$guestGame=Get-Process ICEY -ErrorAction SilentlyContinue;if($guestGame){$state.iceyPid=$guestGame.Id;break};Start-Sleep -Milliseconds 500}
  if(!$state.iceyPid){throw 'ICEY did not start through Steam; inspect Steam and preserve this session.'}
  foreach($ownedId in @($state.iceyPid,$state.dcPid)){$p=Get-Process -Id $ownedId;$state.owned+=@{pid=$p.Id;name=$p.ProcessName;startUtc=$p.StartTime.ToUniversalTime().ToString('o')}}
  Write-Output "Manual playtest ready. Enter a level in ICEY and DC, keep DC foreground. F7 switches ICEY / native Hero. Native bindings are written to native_keymap.csv. Combat remains experimental; see README. Logs: $out"
 } catch {$_|Out-String|Set-Content -LiteralPath (Join-Path $out 'startup_failure.txt');throw} finally {$state|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $out 'state.json') -Encoding UTF8;Copy-Item -LiteralPath (Join-Path $out 'state.json') -Destination (Join-Path $out 'launch.json')}
} finally {Stop-Transcript|Out-Null}
