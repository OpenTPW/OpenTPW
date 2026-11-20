param([string]$Compiler = 'gcc')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'runtimes/win-x64/native/OpenTPW.Audio.Native.dll'
New-Item -ItemType Directory -Force (Split-Path -Parent $out) | Out-Null
& $Compiler -std=c11 -O2 -Wall -Wextra -Werror -shared -static-libgcc '-Wl,--no-insert-timestamp' (Join-Path $PSScriptRoot 'audio.c') -o $out
if ($LASTEXITCODE) { throw "Native audio build failed: $LASTEXITCODE" }
$version = (& $Compiler --version | Select-Object -First 1)
$files = @('audio.c','minimp3/minimp3.h','minimp3/LICENSE','../runtimes/win-x64/native/OpenTPW.Audio.Native.dll')
[ordered]@{ compiler=$version; flags='-std=c11 -O2 -Wall -Wextra -Werror -shared -static-libgcc -Wl,--no-insert-timestamp'; files=@($files | ForEach-Object {
    [ordered]@{ path=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $_) -Algorithm SHA256).Hash }
}) } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'build.json') -Encoding utf8
