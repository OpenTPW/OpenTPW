# Captures a bounded run of renderer statistics without injecting keyboard or pointer input.
param(
    [string]$Repository = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory)][string]$Out,
    [ValidateRange(20,600)][int]$Seconds = 75,
    [string]$Application = '',
    [string]$View = ''
)
$ErrorActionPreference = 'Stop'
$Repository = (Resolve-Path -LiteralPath $Repository).Path
if (-not $Application) {
    [xml]$project = Get-Content -Raw -LiteralPath (Join-Path $Repository 'source/OpenTPW/OpenTPW.csproj')
    $framework = [string]$project.Project.PropertyGroup.TargetFramework
    $Application = Join-Path $Repository "source/OpenTPW/bin/Debug/$framework/OpenTPW.dll"
}
$Application = (Resolve-Path -LiteralPath $Application).Path
$Out = [IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Force (Split-Path -Parent $Out) | Out-Null
$arguments = '"' + $Application + '"'
if ($View) { $arguments += ' --lobby-view "' + (Resolve-Path -LiteralPath $View).Path + '"' }
try {
    $process = Start-Process dotnet -ArgumentList $arguments -WorkingDirectory $Repository -WindowStyle Hidden -PassThru -RedirectStandardOutput $Out -RedirectStandardError ([IO.Path]::ChangeExtension($Out, '.err.log'))
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.Elapsed.TotalSeconds -lt $Seconds) {
        if ($process.WaitForExit(1000)) { throw "Renderer exited before sampling completed: $($process.ExitCode)" }
    }
}
finally {
    # Only the child started by this invocation is stopped; retail and other renderer processes are untouched.
    if ($null -ne $process -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit(10000) | Out-Null }
}
$samples = @(Select-String -LiteralPath $Out -Pattern 'Frame stats' | ForEach-Object { $_.Line })
if ($samples.Count -lt 3) { throw "Fewer than three statistics samples: $Out" }
$samples
