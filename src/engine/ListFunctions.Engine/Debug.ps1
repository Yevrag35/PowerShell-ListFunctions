param  (
	[Parameter(Mandatory=$false)]
	[string] $LibraryName = 'ListFunctions.Engine',

	[Parameter(Mandatory=$false)]
	[string] $RuntimeTarget,

	[Parameter(Mandatory=$false)]
	[string[]] $CopyToOutput = @()
)

$depFile = "$PSScriptRoot\$LibraryName.deps.json"
$json = Get-Content -Path $depFile -Raw | ConvertFrom-Json

if (-not $PSBoundParameters.ContainsKey("RuntimeTarget")) {

	$RuntimeTarget = $json.runtimeTarget.name

	if ([string]::IsNullOrEmpty($RuntimeTarget)) {

		throw "No RuntimeTarget supplied or detected."
	}
}

$targets = $json.targets."$RuntimeTarget"

foreach ($toCopy in $CopyToOutput)
{
	$dependency = $targets.psobject.Properties.Where({$_.Name -like "$toCopy*"}) | Select -First 1

	if ($null -eq $dependency) {
		Write-Warning "'$toCopy' was not found in the list of dependencies."
		continue
	}

	$name, $version = $dependency.Name -split '\/'
	if ([string]::IsNullOrEmpty($name) -or [string]::IsNullOrEmpty($version)) {

		Write-Warning "Unable to parse name and version from '$toCopy'."
		continue
	}

	$pso = $targets."$($dependency.Name)".runtime
	if ($null -eq $pso) {

		Write-Warning "No runtime target was found in '$($dependency.Name)'."
		continue;
	}

	$mems = $pso | Get-Member -MemberType NoteProperty | Where { $_.Name -clike "lib/*" }
	foreach ($mem in $mems)
	{
		$fileName = [System.IO.Path]::GetFileName($mem.Name)
		if (-not (Test-Path -Path "$PSScriptRoot\$fileName" -PathType Leaf))
		{
			$file = "$env:nuget\$name\$version\$($mem.Name)"
			Copy-Item -Path $file -Destination "$PSScriptRoot"
		}
		else
		{
			Write-Host "`"$fileName`" already copied..." -f Yellow
		}
	}
}

Import-Module "$PSScriptRoot\$LibraryName.dll" -ErrorAction Stop -Verbose
$myDesktop = [System.Environment]::GetFolderPath("Desktop")

Push-Location $myDesktop

$o1 = [pscustomobject]@{
	Hi = "asdf"
	Bye = "jkl;"
}
$o2 = [pscustomobject]@{
	Hi = "asdf"
	Bye = "jkl;"
}
$o3 = [pscustomobject]@{
	Hi = "1234"
	Bye = "too late"
}
$o4 = [pscustomobject]@{
	Hi = ""
	Bye = "too late again"
}
$o5 = [pscustomobject]@{
	Hi = $null
	Bye = "too late"
}

$eqScr = { $x.Hi -eq $y.Hi }
#$hashScr = {  }
$compar = { $x.Hi.CompareTo($y.Hi) }

$eq = New-Object "ListFunctions.ScriptBlockEqualityComparer[object]"($eqScr, $hashScr)
$cm = New-Object "ListFunctions.ScriptBlockComparer[object]"($compar)