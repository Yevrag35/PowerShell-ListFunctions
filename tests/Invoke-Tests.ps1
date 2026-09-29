<#
.SYNOPSIS
Runs the Pester tests against a ListFunctions build in Windows PowerShell 5.1 and PowerShell 7.

.DESCRIPTION
Each edition runs the tests in a new process, which imports the module from the build output under src/engine. The
DLLs unload when that process exits, so they don't block the next build.

This script doesn't build anything. Build src/engine/ListFunctions.Engine.slnx in the same configuration first.

The script exits with 0 when every test passes in every edition, and with 1 otherwise.

.PARAMETER Configuration
Selects the build configuration to test.

.PARAMETER Edition
Selects the PowerShell editions to test in: Desktop (Windows PowerShell 5.1) and Core (PowerShell 7).

.PARAMETER Path
Specifies the test files or folders to run. The default is this folder.

.PARAMETER Tag
Runs only the tests that have one of these tags, such as Bug06.

.PARAMETER ExcludeTag
Skips the tests that have one of these tags.

.PARAMETER Output
Sets Pester's output verbosity.

.PARAMETER InProcess
Runs the tests in the current process instead of starting one per edition. The current process then keeps the
module's DLLs loaded until it exits.

.EXAMPLE
./tests/Invoke-Tests.ps1

Runs every test against the Debug build in both editions.

.EXAMPLE
./tests/Invoke-Tests.ps1 -Tag Bug06 -Edition Core -Output Detailed

Runs the tests for bug 06 in PowerShell 7 and lists each test.
#>
[CmdletBinding(DefaultParameterSetName = 'NewProcess')]
param(
	[ValidateSet('Debug', 'Release')]
	[string] $Configuration = 'Debug',

	[Parameter(ParameterSetName = 'NewProcess')]
	[ValidateSet('Desktop', 'Core')]
	[string[]] $Edition = @('Desktop', 'Core'),

	[string[]] $Path = @($PSScriptRoot),

	[string[]] $Tag,

	[string[]] $ExcludeTag,

	[ValidateSet('None', 'Normal', 'Detailed', 'Diagnostic')]
	[string] $Output = 'Normal',

	[Parameter(ParameterSetName = 'InProcess')]
	[switch] $InProcess
)

$ErrorActionPreference = 'Stop'

if ($InProcess) {
	Import-Module -Name Pester -MinimumVersion 6.0

	$config = New-PesterConfiguration
	$config.Run.Path = $Path
	$config.Run.PassThru = $true
	$config.Output.Verbosity = $Output
	if ($Tag) {
		$config.Filter.Tag = $Tag
	}
	if ($ExcludeTag) {
		$config.Filter.ExcludeTag = $ExcludeTag
	}

	Write-Host "PowerShell $($PSVersionTable.PSVersion) ($($PSVersionTable.PSEdition)), $Configuration build"

	$previousConfiguration = $env:LISTFUNCTIONS_TEST_CONFIGURATION
	$env:LISTFUNCTIONS_TEST_CONFIGURATION = $Configuration
	try {
		$result = Invoke-Pester -Configuration $config
	}
	finally {
		$env:LISTFUNCTIONS_TEST_CONFIGURATION = $previousConfiguration
	}

	if ($result.Result -eq 'Passed') {
		exit 0
	}
	exit 1
}

# Quotes each value as a PowerShell string literal and joins them into an array literal.
function ConvertTo-Literal {
	param(
		[string[]] $Value
	)

	$literals = foreach ($item in $Value) {
		"'" + $item.Replace("'", "''") + "'"
	}
	$literals -join ','
}

$resolvedPath = foreach ($item in $Path) {
	(Resolve-Path -LiteralPath $item).ProviderPath
}

$arguments = @(
	'-InProcess'
	'-Configuration', (ConvertTo-Literal $Configuration)
	'-Path', (ConvertTo-Literal $resolvedPath)
	'-Output', (ConvertTo-Literal $Output)
)
if ($Tag) {
	$arguments += '-Tag', (ConvertTo-Literal $Tag)
}
if ($ExcludeTag) {
	$arguments += '-ExcludeTag', (ConvertTo-Literal $ExcludeTag)
}

$command = '& {0} {1}; exit $LASTEXITCODE' -f (ConvertTo-Literal $PSCommandPath), ($arguments -join ' ')

$failedEditions = @()
foreach ($name in $Edition) {
	if ($name -eq 'Core') {
		$executable = 'pwsh'
	}
	else {
		$executable = 'powershell.exe'
	}

	# Not -EncodedCommand: when this process's output is redirected, a child started with it also writes its host
	# output to stderr as CLIXML.
	& $executable -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command $command
	if ($LASTEXITCODE -ne 0) {
		$failedEditions += $name
	}
}

if ($failedEditions.Count -gt 0) {
	Write-Host "Tests failed in: $($failedEditions -join ', ')"
	exit 1
}
exit 0
