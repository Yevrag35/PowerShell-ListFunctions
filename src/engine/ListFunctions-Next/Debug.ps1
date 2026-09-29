[CmdletBinding(SupportsShouldProcess=$true, PositionalBinding = $false)]
param  (
	[Parameter(Mandatory=$false)]
	[string] $LibraryName = 'ListFunctions.Next'
)

$myDesktop = [System.Environment]::GetFolderPath("Desktop")
$otherDll = "$PSScriptRoot\ListFunctions.Engine.dll"
if (-not (Test-Path -Path $otherDll -PathType Leaf)) {
	throw "The specified module DLL was not found -> `"$otherDll`""
}
Import-Module $otherDll -ErrorAction Stop -Force

$dllPath = "$PSScriptRoot\$LibraryName.dll"
if (-not (Test-Path -Path $dllPath -PathType Leaf)) {
	throw "The specified module DLL was not found -> `"$dllPath`""
}

if ($PSCmdlet.ShouldProcess($dllPath, "Importing Module")) {

	Import-Module $dllPath -ErrorAction Stop -Force
	Push-Location $myDesktop
}