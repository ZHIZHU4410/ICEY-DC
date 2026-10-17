$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
$RepositoryRoot=Split-Path -Parent $PSScriptRoot
function Read-LocalPaths([string]$ConfigFile){
 if(!$ConfigFile){$ConfigFile=Join-Path $RepositoryRoot 'config\paths.local.psd1'}
 if(!(Test-Path -LiteralPath $ConfigFile)){throw 'Copy config/paths.example.psd1 to config/paths.local.psd1 and fill in your installation and save paths.'}
 $settings=Import-PowerShellDataFile -LiteralPath $ConfigFile
 foreach($key in @('IceyGameDir','DeadCellsGameDir')){if(!$settings[$key] -or !(Test-Path -LiteralPath $settings[$key] -PathType Container)){throw "Game directory missing: $key. Edit your local configuration."};$settings[$key]=(Resolve-Path -LiteralPath $settings[$key]).Path}
 if(!$settings.ContainsKey('SaveDirectories')){$settings.SaveDirectories=@()}
 return $settings
}
function Get-VerifiedOriginals($Settings){
 $roots=@{ICEY=$Settings.IceyGameDir;DC=$Settings.DeadCellsGameDir}
 $baseline=Get-Content -LiteralPath (Join-Path $RepositoryRoot 'config\tested-original-hashes.json') -Raw|ConvertFrom-Json
 $rows=@()
 foreach($entry in $baseline.originalFiles){
  $file=Join-Path $roots[$entry.game] $entry.relativePath
  if(!(Test-Path -LiteralPath $file -PathType Leaf)){throw "Required tested file missing: $file. Install the tested loaders/MDK and run them once to generate their cache."}
  $actual=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
  if($actual -ne $entry.expected){throw "Unsupported game/loader build: $file. Expected $($entry.expected); found $actual. Use ICEY Unity 5.4.0f3 / BepInEx 5.4.23.2 / DC 35 8b5231c / DCCM 35.13.8.0, or perform compatibility validation. Do not rewrite the baseline to bypass this check."}
  $rows+=@{game=$entry.game;relativePath=$entry.relativePath;expected=$entry.expected}
 }
 if($rows.Count -ne 9){throw 'Expected exactly nine original fingerprints'}
 return $rows
}
