BeforeDiscovery {
	$cmdletCases = @(
		(Import-PowerShellDataFile -LiteralPath "$PSScriptRoot/../ListFunctions/ListFunctions.psd1").CmdletsToExport |
			ForEach-Object { @{ Name = $_ } }
	)
}

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

	# 4.0 renames these cmdlets and keeps their 3.x names as aliases, so that scripts written for 3.x still run. The Assert
	# cmdlets take Test, the verb that PowerShell uses for commands that return a [bool]. The Find cmdlets swap names with
	# their 3.x aliases, Find-Index and Find-LastIndex, which match the class names in their error IDs. The manifest tests
	# don't catch a dropped alias, because it would be dropped from the manifest too.
	It 'keeps the 3.x name <Alias> as an alias of <Name>' -ForEach @(
		@{ Alias = 'Assert-AnyObject'; Name = 'Test-AnyObject' }
		@{ Alias = 'Assert-AllObject'; Name = 'Test-AllObject' }
		@{ Alias = 'Find-IndexOf'; Name = 'Find-Index' }
		@{ Alias = 'Find-LastIndexOf'; Name = 'Find-LastIndex' }
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
		@{ Name = 'Find-Index'; Command = "'a', 'b' | Find-Index { `$_ -eq 1 } -InputObject 1, 2" }
		@{ Name = 'Find-Index with an empty pipeline'; Command = "@() | Find-Index { `$_ -eq 1 } -InputObject 1, 2" }
		@{ Name = 'Find-LastIndex'; Command = "'a', 'b' | Find-LastIndex { `$_ -eq 1 } -InputObject 1, 2" }
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

	# Every parameter that takes a type completes type names with the same completer, which the Engine tests cover. These
	# tests check that each parameter has it, including when PowerShell has to work out which parameter a positional
	# argument binds to.
	It 'completes a type name for <Label>' -ForEach @(
		@{ Label = 'New-List -GenericType'; Line = 'New-List -GenericType [gu' }
		@{ Label = 'New-List -GenericType by position'; Line = 'New-List [gu' }
		@{ Label = 'New-HashSet -GenericType by position'; Line = 'New-HashSet [gu' }
		@{ Label = 'New-SortedSet -GenericType by position'; Line = 'New-SortedSet [gu' }
		@{ Label = 'New-Dictionary -KeyType by position'; Line = 'New-Dictionary [gu' }
		@{ Label = 'New-Dictionary -ValueType by position'; Line = 'New-Dictionary [string] [gu' }
		@{ Label = 'ConvertTo-Dictionary -KeyType'; Line = 'ConvertTo-Dictionary Id -KeyType [gu' }
		@{ Label = 'ConvertTo-Dictionary -ValueType'; Line = 'ConvertTo-Dictionary Id -ValueType [gu' }
	) {
		$completion = TabExpansion2 -inputScript $Line -cursorColumn $Line.Length
		Should-ContainCollection -Expected '[guid]' -Actual $completion.CompletionMatches.CompletionText
	}

	# PowerShell offers file names for an argument that has no completer, or whose completer returns $null. The tests folder
	# has files, so the test would see them.
	It 'offers nothing, instead of file names, when nothing is typed for a type' {
		Push-Location -LiteralPath $PSScriptRoot
		try {
			$completion = TabExpansion2 -inputScript 'New-List ' -cursorColumn 9
		}
		finally {
			Pop-Location
		}

		$completion.CompletionMatches.Count | Should-Be 0
	}

	# Get-Help shows the syntax blocks and parameter attributes that the help file writes, not the ones that the cmdlet
	# declares, so a parameter that changes without the help file shows the wrong usage. These tests compare the help with
	# each cmdlet. Without a help file beside the module, Get-Help makes up help from the cmdlet, whose type is
	# CmdletHelpInfo, so the first test also fails when the build doesn't copy the help file.
	Context 'Help' {
		BeforeAll {
			$commonParameters = [System.Management.Automation.Cmdlet]::CommonParameters +
				[System.Management.Automation.Cmdlet]::OptionalCommonParameters

			# The name is module-qualified, so an installed ListFunctions 3.x can't answer for a cmdlet that it has too.
			function Get-BuildHelp([string] $Name) {
				Get-Help -Name "$($module.Name)\$Name" -Full
			}

			function Get-PositionText($Parameter) {
				if ($Parameter.Position -ge 0) {
					return [string]$Parameter.Position
				}
				'named'
			}

			function Get-PipelineInputText($Parameter) {
				$kinds = @()
				if ($Parameter.ValueFromPipeline) {
					$kinds += 'ByValue'
				}
				if ($Parameter.ValueFromPipelineByPropertyName) {
					$kinds += 'ByPropertyName'
				}
				if ($kinds.Count -eq 0) {
					return 'False'
				}
				'True ({0})' -f ($kinds -join ', ')
			}

			# Formats a parameter of a syntax block. A switch has no value type.
			function Format-SyntaxParameter([string] $Name, [string] $ValueType, [string] $Required, [string] $Position) {
				$text = "-$Name"
				if ($ValueType) {
					$text += " <$ValueType>"
				}
				'{0} required={1} position={2}' -f $text, $Required, $Position
			}
		}

		It 'shows the help file for <Name>' -ForEach $cmdletCases {
			$help = Get-BuildHelp $Name
			Should-ContainCollection -Expected "MamlCommandHelpInfo#$($module.Name)#$Name" -Actual $help.PSObject.TypeNames
		}

		# A parameter is required in the help when it's mandatory in every parameter set that has it.
		It 'describes the parameters of <Name> as the cmdlet declares them' -ForEach $cmdletCases {
			$command = Get-Command -Name $Name
			$expected = foreach ($parameter in $command.Parameters.Values) {
				if ($commonParameters -contains $parameter.Name) {
					continue
				}

				$inSets = @($command.ParameterSets | ForEach-Object { $_.Parameters } | Where-Object Name -eq $parameter.Name)
				$required = @($inSets | Where-Object { -not $_.IsMandatory }).Count -eq 0
				$position = @($inSets | ForEach-Object { Get-PositionText $_ } | Sort-Object -Unique) -join ','
				$pipelineInput = @($inSets | ForEach-Object { Get-PipelineInputText $_ } | Sort-Object -Unique) -join ','
				$aliases = 'none'
				if ($parameter.Aliases.Count -gt 0) {
					$aliases = $parameter.Aliases -join ', '
				}

				'-{0} <{1}> required={2} position={3} pipelineInput={4} aliases={5}' -f $parameter.Name,
					$parameter.ParameterType.Name, $required.ToString().ToLowerInvariant(), $position, $pipelineInput, $aliases
			}

			$actual = foreach ($parameter in (Get-BuildHelp $Name).parameters.parameter) {
				'-{0} <{1}> required={2} position={3} pipelineInput={4} aliases={5}' -f $parameter.name, $parameter.type.name,
					$parameter.required, $parameter.position, $parameter.pipelineInput, $parameter.aliases
			}

			Should-BeCollection -Expected @($expected) -Actual @($actual)
		}

		It 'has a syntax block for each parameter set of <Name>' -ForEach $cmdletCases {
			$command = Get-Command -Name $Name
			$expected = foreach ($set in $command.ParameterSets) {
				$parameters = foreach ($parameter in $set.Parameters) {
					if ($commonParameters -contains $parameter.Name) {
						continue
					}

					$valueType = $parameter.ParameterType.Name
					if ($parameter.ParameterType -eq [switch]) {
						$valueType = ''
					}
					$required = $parameter.IsMandatory.ToString().ToLowerInvariant()
					Format-SyntaxParameter $parameter.Name $valueType $required (Get-PositionText $parameter)
				}
				($parameters | Sort-Object) -join '; '
			}

			$actual = foreach ($item in (Get-BuildHelp $Name).syntax.syntaxItem) {
				$parameters = foreach ($parameter in $item.parameter) {
					Format-SyntaxParameter $parameter.name $parameter.parameterValue $parameter.required $parameter.position
				}
				($parameters | Sort-Object) -join '; '
			}

			Should-BeCollection -Expected @($expected) -Actual @($actual)
		}
	}
}
