param(
    [ValidateSet('Setup', 'DemoSetup', 'Validate', 'Build', 'Smoke', 'Open')]
    [string]$Action = 'Validate',
    [string]$EditorPath = 'D:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
if ($Action -eq 'Smoke') {
    $player = Join-Path $project 'Builds\Windows\FoundrySiege.exe'
    if (!(Test-Path -LiteralPath $player)) { throw 'Run Build first.' }
    $log = Join-Path $logs 'player-smoke.log'
    $process = Start-Process -FilePath $player -ArgumentList @('-batchmode', '-nographics', '-smokeTest', '-logFile', ('"' + $log + '"')) -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(90000)) { Stop-Process -Id $process.Id; throw "Smoke test timed out. See $log" }
    if ($process.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern 'DEMO_SMOKE_SUCCESS' -Quiet)) { throw "Smoke test failed. See $log" }
    Write-Output "Physics smoke test passed. Log: $log"
    exit 0
}
if (!(Test-Path -LiteralPath $EditorPath)) { throw "Unity editor not found: $EditorPath" }
$busy = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like ('*' + $project + '*') }
if ($busy) { throw 'Close the Unity Editor for this project before running another Unity command.' }
if ($Action -eq 'Open') {
    Start-Process -FilePath $EditorPath -ArgumentList @('-projectPath', ('"' + $project + '"'), '-openfile', ('"' + (Join-Path $project 'Assets\Scenes\FoundryDemo.unity') + '"')) -WindowStyle Hidden
    exit 0
}
$methods = @{ Setup='BirdReplica.EditorTools.ProjectSetup.Configure'; DemoSetup='DemoSceneBuilder.Configure'; Validate='DemoSceneBuilder.Validate'; Build='DemoSceneBuilder.BuildWindows' }
$log = Join-Path $logs ($Action.ToLowerInvariant() + '.log')
$arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $project + '"'), '-executeMethod', $methods[$Action], '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $log -Tail 60; throw "Unity $Action failed with exit code $($process.ExitCode)." }
$markers = @{ Setup='PROJECT_SETUP_SUCCESS'; DemoSetup='DEMO_SETUP_SUCCESS'; Validate='PROJECT_VALIDATION_SUCCESS'; Build='WINDOWS_BUILD_SUCCESS' }
if (!(Select-String -LiteralPath $log -Pattern $markers[$Action] -Quiet)) { throw "Unity did not complete $Action. See $log" }
Write-Output "Unity $Action succeeded. Log: $log"
