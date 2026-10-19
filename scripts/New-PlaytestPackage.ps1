param([string]$ConfigFile,[string]$PythonRuntime,[string]$OutputRoot)
. (Join-Path $PSScriptRoot 'Common.ps1')
$settings=Read-LocalPaths $ConfigFile
$originals=@(Get-VerifiedOriginals $settings)
$buildPath=Join-Path $RepositoryRoot 'out\build-manifest.json'
if(!(Test-Path -LiteralPath $buildPath)){throw 'Run scripts/Build.ps1 first'}
$build=Get-Content -LiteralPath $buildPath -Raw|ConvertFrom-Json
foreach($entry in $build.artifacts){if((Get-FileHash -LiteralPath (Join-Path $RepositoryRoot $entry.path)).Hash -ne $entry.sha256){throw 'Build output changed; rebuild before packaging'}}
if(!$OutputRoot){$OutputRoot=Join-Path $RepositoryRoot 'dist'}
$package=Join-Path $OutputRoot 'v1.0.10-playtest-candidate';$zip=$package+'.zip'
if((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $zip)){throw 'Output already exists. Archive the previous output first; no overwrite is performed.'}
if(!$PythonRuntime){
 $runtimeSource=Get-Content -LiteralPath (Join-Path $RepositoryRoot 'config\python-runtime.json') -Raw|ConvertFrom-Json
 $cache=Join-Path $RepositoryRoot 'out\python-3.14.0';$archive=$cache+'.zip'
 if(!(Test-Path -LiteralPath $archive)){Invoke-WebRequest -UseBasicParsing -Uri $runtimeSource.url -OutFile $archive}
 if((Get-FileHash -LiteralPath $archive).Hash -ne $runtimeSource.sha256){throw 'Python runtime archive SHA256 mismatch; preserve the file and inspect before retrying.'}
 if(!(Test-Path -LiteralPath $cache)){Expand-Archive -LiteralPath $archive -DestinationPath $cache}
 $PythonRuntime=$cache
}
& (Join-Path $PythonRuntime 'python.exe') -I -c 'import sys,socket,threading,json; assert sys.version_info[:3] == (3,14,0); print(314)'
if($LASTEXITCODE -ne 0){throw 'Expected Python 3.14.0 x64 embeddable runtime'}
New-Item -ItemType Directory -Path (Join-Path $package 'payload\icey'),(Join-Path $package 'payload\dc\IceyEarlyPlaytestInput'),(Join-Path $package 'payload\native') -Force|Out-Null
Copy-Item -LiteralPath $PythonRuntime -Destination (Join-Path $package 'runtime') -Recurse
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'config\python-runtime.json') -Destination (Join-Path $package 'runtime\source.json')
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'src\icey\bin\Release\IceyEarlyPlaytestAction.dll') -Destination (Join-Path $package 'payload\icey\IceyEarlyPlaytestAction.dll')
foreach($name in @('IceyEarlyPlaytestInput.dll','modinfo.json')){Copy-Item -LiteralPath (Join-Path $RepositoryRoot ('src\dc\bin\Release\net10.0\output\IceyEarlyPlaytestInput\'+$name)) -Destination (Join-Path $package ('payload\dc\IceyEarlyPlaytestInput\'+$name))}
foreach($name in @('ep4_cpu.dll','ep5_hud.dll','ep6_dash.dll')){Copy-Item -LiteralPath (Join-Path $RepositoryRoot ('out\native\'+$name)) -Destination (Join-Path $package ('payload\native\'+$name))}
foreach($name in @('Run-Playtest.cmd','Run-Playtest.ps1','Stop-Playtest.cmd','Stop-Playtest.ps1','Open-Guide.cmd')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $package $name)}
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'src\relay.py') -Destination (Join-Path $package 'relay.py')
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'docs') -Destination (Join-Path $package 'docs') -Recurse
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'LICENSE.md'),(Join-Path $RepositoryRoot 'NOTICE.md') -Destination $package
$configText="@{`r`n    IceyGameDir = '"+$settings.IceyGameDir.Replace("'","''")+"'`r`n    DeadCellsGameDir = '"+$settings.DeadCellsGameDir.Replace("'","''")+"'`r`n    SaveDirectories = @(`r`n"
foreach($save in $settings.SaveDirectories){$configText+="        '"+$save.Replace("'","''")+"'`r`n"}
$configText+="    )`r`n}`r`n";$configText|Set-Content -LiteralPath (Join-Path $package 'paths.psd1') -Encoding UTF8
@'
# ICEY-DC 1.0.10-playtest-candidate

Compiled manual playtest package. Edit paths.psd1 for your game and save locations, close both games, then use Run-Playtest.cmd. Enter a playable ICEY level and DC level; keep DC foreground. Use Stop-Playtest.cmd to close/archive this session.

WASD/Space: movement/jump; S+Space: drop through one-way platform; J/hold J/I: attacks; O/L/U/Q/E: native abilities; F/Enter: interaction; Esc: pause; F7: Hero/ICEY; F8: native HP recovery; lethal native damage now restores full HP in place.

Full usage and limitations: docs/USAGE.md and docs/KNOWN_LIMITATIONS.md. Source and build documentation: https://github.com/ZHIZHU4410/ICEY-DC
'@ | Set-Content -LiteralPath (Join-Path $package 'README.md') -Encoding UTF8
@'
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>ICEY-DC 1.0.10</title><style>body{font:18px/1.8 sans-serif;max-width:850px;margin:40px auto;padding:0 20px;background:#101923;color:#e7edf3}a{color:#69dbef}code{color:#ffd37d}li{margin:12px 0}</style><h1>ICEY-DC 1.0.10 试用教程</h1><ol><li>核对 <code>paths.psd1</code> 的游戏路径与存档备份目录，关闭两游戏。</li><li>双击 <code>Run-Playtest.cmd</code>。分别进入 ICEY 和 DC 关卡；DC 菜单选择“游玩 → 继续”，保持 DC 前台。</li><li>WASD/空格移动跳跃，S+空格下台阶，J/长按J/I攻击，O/L/U/Q/E原生能力；F/Enter互动，Esc暂停，F7切Hero/ICEY。</li><li><strong>F8 无消耗回满ICEY生命</strong>，长按单次、满血无操作，致命伤自动原地回满血；不补能量/护盾或冲刺次数。</li><li>结束运行同包 <code>Stop-Playtest.cmd</code>；保留 sessions 里的日志和备份。</li></ol><p>ICEY模式去掉整屏相机雾膜，保留深度雾/灯光/真实烟雾。技能窗口、资源与解锁条件仍由原生逻辑决定。完整战斗/换关/复杂延迟尚未全部验收。</p><p><a href="docs/USAGE.md">完整说明</a> · <a href="docs/KNOWN_LIMITATIONS.md">已知限制</a> · <a href="https://github.com/ZHIZHU4410/ICEY-DC">源码</a></p></html>
'@ | Set-Content -LiteralPath (Join-Path $package 'Usage-Guide.html') -Encoding UTF8
$payload=@();foreach($file in Get-ChildItem -LiteralPath $package -File -Recurse){$relative=$file.FullName.Substring($package.Length+1);if($relative -eq 'paths.psd1'){continue};$payload+=@{path=$relative;sha256=(Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant();bytes=$file.Length}}
@{version='1.0.10-playtest-candidate';status='build_passed_user_gameplay_test_pending';originalFiles=$originals;payload=$payload}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $package 'manifest.json') -Encoding UTF8
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
((Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant()+'  '+(Split-Path -Leaf $zip))|Set-Content -LiteralPath ($zip+'.sha256') -Encoding ASCII
Write-Output "Package ready: $package. Configure paths.psd1, then Run-Playtest.cmd. No game deployment performed."
