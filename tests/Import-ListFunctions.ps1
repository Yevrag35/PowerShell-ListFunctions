<#
.SYNOPSIS
Imports the ListFunctions build for the current PowerShell edition.

.DESCRIPTION
PowerShell 7 imports ListFunctions.Next.dll from the net10.0 output of ListFunctions-Next, and Windows PowerShell 5.1
imports ListFunctions.NETFramework.dll from the net48 output of ListFunctions-NETFramework. That's the same split that
ListFunctions.psm1 makes, but it skips the DLLs committed under ListFunctions/.

The LISTFUNCTIONS_TEST_CONFIGURATION environment variable selects the build configuration and defaults to Debug.
Invoke-Tests.ps1 sets it.

Every test file calls this script in a top-level BeforeAll block.

.PARAMETER PassThru
Returns the imported module.
#>
[CmdletBinding()]
param(
	[switch] $PassThru
)

$configuration = $env:LISTFUNCTIONS_TEST_CONFIGURATION
if (-not $configuration) {
	$configuration = 'Debug'
}

$engineRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\src\engine")
if ($PSVersionTable.PSEdition -eq 'Core') {
	$modulePath = "$engineRoot\ListFunctions-Next\bin\$configuration\net10.0\ListFunctions.Next.dll"
}
else {
	$modulePath = "$engineRoot\ListFunctions-NETFramework\bin\$configuration\net48\ListFunctions.NETFramework.dll"
}

if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
	throw "There's no $configuration build for PowerShell $($PSVersionTable.PSEdition) at '$modulePath'. Build src/engine/ListFunctions.Engine.slnx first."
}

Import-Module -Name $modulePath -PassThru:$PassThru -ErrorAction Stop
