BeforeAll {
	$module = & "$PSScriptRoot/Import-ListFunctions.ps1" -PassThru
	$manifest = Import-PowerShellDataFile -LiteralPath "$PSScriptRoot/../ListFunctions/ListFunctions.psd1"
}

Describe 'ListFunctions module' {
	It 'exports the cmdlets that the manifest lists' {
		$exported = @($module.ExportedCmdlets.Keys | Sort-Object)
		Should-BeCollection -Expected @($manifest.CmdletsToExport | Sort-Object) -Actual $exported
	}

	It 'exports the aliases that the manifest lists' {
		$exported = @($module.ExportedAliases.Keys | Sort-Object)
		Should-BeCollection -Expected @($manifest.AliasesToExport | Sort-Object) -Actual $exported
	}

	It 'runs every cmdlet from the build under test' {
		# An installed ListFunctions 3.x exports the same cmdlet names.
		foreach ($name in $manifest.CmdletsToExport) {
			(Get-Command -Name $name).Module.Path | Should-Be $module.Path -Because "$name should come from the build under test"
		}
	}

	It 'creates a typed list' {
		$list = New-List [int] -InputObject 1, 2, 3
		Should-HaveType -Expected ([System.Collections.Generic.List[int]]) -Actual $list
		# Should-BeCollection can't copy a collection of value types, so pass it an object[].
		Should-BeCollection -Expected @(1, 2, 3) -Actual @($list)
	}
}
