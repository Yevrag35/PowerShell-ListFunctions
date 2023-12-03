$curDir = Split-Path -Parent $MyInvocation.MyCommand.Definition;
$myDesktop = [System.Environment]::GetFolderPath("Desktop")

Import-Module "$curDir\ListFunctions.NETFramework.dll" -ErrorAction Stop

Push-Location $([System.Environment]::GetFolderPath("Desktop"))
$psAll = @(
	[pscustomobject] @{ Name = "mike" },
	[pscustomobject] @{ Name = "frank"},
	$null
)