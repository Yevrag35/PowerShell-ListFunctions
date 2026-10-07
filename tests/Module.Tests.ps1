BeforeAll {
	$module = & "$PSScriptRoot/Import-ListFunctions.ps1" -PassThru
	$manifest = Import-PowerShellDataFile -LiteralPath "$PSScriptRoot/../ListFunctions/ListFunctions.psd1"
	. "$PSScriptRoot/Invoke-InNewRunspace.ps1"
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

	# 3.x named these cmdlets with the Assert verb. 4.0 names them with Test, which PowerShell uses for commands that
	# return a [bool], and keeps the old names as aliases so that scripts written for 3.x still run. The manifest tests
	# don't catch a dropped alias, because it would be dropped from the manifest too.
	It 'keeps the 3.x name <Alias> as an alias of <Name>' -ForEach @(
		@{ Alias = 'Assert-AnyObject'; Name = 'Test-AnyObject' }
		@{ Alias = 'Assert-AllObject'; Name = 'Test-AllObject' }
	) {
		$module.ExportedAliases[$Alias].Definition | Should-Be $Name
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
		Should-BeCollection -Expected @(1, 2, 3) -Actual ([object[]]$list)
	}

	It 'does not answer a request for a dependency version that it does not reference' {
		# Without ListFunctions, the load fails because no System.Memory 99.0.0.0 exists. In Windows PowerShell 5.1, the
		# module's assembly resolver must not answer it with the module's own copy. PowerShell 7 has no such resolver.
		{ [System.Reflection.Assembly]::Load('System.Memory, Version=99.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51') } |
			Should-Throw -ExceptionMessage '*System.Memory, Version=99.0.0.0*'
	}

	# PowerShell can't bind a piped object to -InputObject once the command line has bound it, so without the check each
	# piped object got an InputObjectNotBound error, and the command wrote its result for no input, such as -1. The
	# script runs in a new runspace so that the test can see that the error ends only the statement, before any piped
	# object is bound, and that the command writes nothing.
	It 'rejects pipeline input together with -InputObject in <Name>' -ForEach @(
		@{ Name = 'Test-AnyObject'; Command = "1, 2 | Test-AnyObject { `$_ -eq 1 } -InputObject 1, 2" }
		@{ Name = 'Test-AllObject'; Command = "1, 2 | Test-AllObject { `$_ -eq 1 } -InputObject 1, 2" }
		@{ Name = 'Find-IndexOf'; Command = "'a', 'b' | Find-IndexOf { `$_ -eq 1 } -InputObject 1, 2" }
		@{ Name = 'Find-IndexOf with an empty pipeline'; Command = "@() | Find-IndexOf { `$_ -eq 1 } -InputObject 1, 2" }
		@{ Name = 'Find-LastIndexOf'; Command = "'a', 'b' | Find-LastIndexOf { `$_ -eq 1 } -InputObject 1, 2" }
		@{ Name = 'New-List'; Command = "1, 2 | New-List -InputObject 3" }
		@{ Name = 'New-HashSet'; Command = "1, 2 | New-HashSet -InputObject 3" }
		@{ Name = 'New-SortedSet'; Command = "1, 2 | New-SortedSet -InputObject 3" }
		@{ Name = 'New-Dictionary'; Command = "@{ a = 1 } | New-Dictionary -InputObject @{ b = 2 }" }
		@{ Name = 'ConvertTo-Dictionary'; Command = "1, 2 | ConvertTo-Dictionary -KeySelector { `$_ } -InputObject 3" }
	) {
		$result = Invoke-InNewRunspace "$Command; 'still running'"
		$result.StoppedBy | Should-BeNull
		Should-BeCollection -Expected @('still running') -Actual $result.Output
		$result.Errors.Count | Should-Be 1
		$result.Errors[0].FullyQualifiedErrorId | Should-BeLikeString -Expected 'System.ArgumentException,ListFunctions.Cmdlets.*'
		$result.Errors[0].Exception.Message | Should-BeLikeString -Expected 'Cannot use -InputObject and pipeline input together*'
	}
}
