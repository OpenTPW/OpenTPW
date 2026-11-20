<#
.SYNOPSIS
Compiles every HLSL shader stage to SDL GPU SPIR-V and DXIL with DXC.
.DESCRIPTION
Use -Dxc to select a compiler outside PATH. -Check rebuilds in a fresh directory
and compares every byte with OutputDirectory without changing checked-in files.
#>
[CmdletBinding()]
param(
	[string] $Dxc = 'dxc',
	[string] $OutputDirectory,
	[switch] $Check
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$shaderDirectory = Join-Path $repo 'content/shaders'
if ( !$OutputDirectory ) { $OutputDirectory = Join-Path $shaderDirectory 'compiled' }
$OutputDirectory = [IO.Path]::GetFullPath( $OutputDirectory )
$compiler = (Get-Command $Dxc -ErrorAction Stop).Source
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('opentpw-shaders-' + [Guid]::NewGuid().ToString('N'))
$shaders = @(Get-ChildItem -LiteralPath $shaderDirectory -Filter '*.hlsl' | Sort-Object Name)
$legacy = @(Get-ChildItem -LiteralPath $shaderDirectory -Filter '*.shader' | ForEach-Object BaseName | Sort-Object)
if ( !$shaders.Count -or (Compare-Object $legacy @($shaders.BaseName | Sort-Object)) ) {
	throw 'HLSL sources must cover every legacy .shader exactly once.'
}
# native/ holds shaders without original GLSL (ImGui); they are compiled the same way but not matched to .shader files.
$native = @(Get-ChildItem -LiteralPath (Join-Path $shaderDirectory 'native') -Filter '*.hlsl' | Sort-Object Name)
$sources = @($shaders | ForEach-Object { @{ Source = $_.Name; Name = $_.BaseName } }) + @($native | ForEach-Object { @{ Source = 'native/' + $_.Name; Name = $_.BaseName } })
[IO.Directory]::CreateDirectory( $temporary ) | Out-Null

Push-Location $shaderDirectory
try {
	foreach ( $shader in $sources ) {
		foreach ( $stage in @(
			@{ Suffix = 'vert'; Profile = 'vs_6_0'; Entry = 'VSMain'; Define = 'STAGE_VERTEX=1' },
			@{ Suffix = 'frag'; Profile = 'ps_6_0'; Entry = 'PSMain'; Define = 'STAGE_FRAGMENT=1' }
		) ) {
			foreach ( $format in @('spv', 'dxil') ) {
				$output = Join-Path $temporary ($shader.Name + '.' + $stage.Suffix + '.' + $format)
				$arguments = @($shader.Source, '-E', $stage.Entry, '-T', $stage.Profile,
					'-D', $stage.Define, '-HV', '2021', '-Ges', '-WX', '-O3', '-Zpc', '-Fo', $output)
				if ( $format -eq 'spv' ) {
					$arguments += @('-spirv', '-D', 'SPIRV=1', '-fspv-target-env=vulkan1.0', '-fvk-use-gl-layout')
				}
				& $compiler @arguments
				if ( $LASTEXITCODE -ne 0 ) { throw "DXC failed for $($shader.Source) $($stage.Entry) $format" }
			}
		}
	}
	$files = @(Get-ChildItem -LiteralPath $temporary -File | Sort-Object Name)
	$checksums = foreach ( $file in $files ) {
		'{0}  {1}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $file.Name
	}
	[IO.File]::WriteAllText( (Join-Path $temporary 'SHA256SUMS'), ($checksums -join "`n") + "`n" )
	$files = @(Get-ChildItem -LiteralPath $temporary -File | Sort-Object Name)
	if ( $Check ) {
		$existing = @(Get-ChildItem -LiteralPath $OutputDirectory -File | Sort-Object Name)
		if ( Compare-Object @($files.Name) @($existing.Name) ) { throw 'Compiled shader file sets differ.' }
		foreach ( $file in $files ) {
			$expected = Join-Path $OutputDirectory $file.Name
			if ( (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $expected).Hash ) {
				throw "Compiled shader differs: $($file.Name)"
			}
		}
		Write-Host "Verified $($sources.Count * 4) shader binaries and SHA256SUMS against a clean rebuild."
	} else {
		[IO.Directory]::CreateDirectory( $OutputDirectory ) | Out-Null
		foreach ( $file in $files ) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $OutputDirectory $file.Name) -Force }
		Write-Host "Compiled $($sources.Count) shaders, $($sources.Count * 4) binaries to $OutputDirectory"
	}
} finally {
	Pop-Location
	# Only the fresh directory created by this invocation can be removed here.
	$resolved = [IO.Path]::GetFullPath( $temporary )
	$temporaryRoot = [IO.Path]::GetFullPath( [IO.Path]::GetTempPath() ).TrimEnd( [IO.Path]::DirectorySeparatorChar )
	if ( (Split-Path -Parent $resolved) -ne $temporaryRoot -or (Split-Path -Leaf $resolved) -notmatch '^opentpw-shaders-[a-f0-9]{32}$' ) {
		throw 'Unexpected temporary shader directory.'
	}
	Remove-Item -LiteralPath $resolved -Recurse -Force
}
