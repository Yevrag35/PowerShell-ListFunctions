BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Invoke-InNewRunspace.ps1"
}

Describe 'Test-AllObject' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 3 | Test-AllObject { $args[0] -gt 0 } | Should-BeTrue
		1, 2, 3 | Test-AllObject { $args[0] -gt 1 } | Should-BeFalse
	}

	Context 'Condition' {
		# $null is the same as leaving -Condition out, which the command requires, so it's rejected the way Find-IndexOf
		# rejects it. It used to be an error only when an element reached the condition, so empty input gave $true.
		It 'rejects -Condition $null when it binds the parameter, even when there is no input' {
			{ @() | Test-AllObject -Condition $null } | Should-Throw -FullyQualifiedErrorId 'ParameterArgumentValidationErrorNullNotAllowed,*'
		}
	}

	Context 'Pipeline input' {
		It 'passes a piped <Name> to -Condition as one element' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null) }
			@{ Name = 'empty array'; Items = @(1, @()) }
		) {
			# The condition is always true, so the result is True as long as the element reaches it.
			$Items | Test-AllObject { $true -or $_ } | Should-BeTrue
		}

		It 'passes a piped array to -Condition as one element' -Tag 'Bug06' {
			@(1, @(2, 3)) | Test-AllObject { $_ -is [int] } | Should-BeFalse
		}

		It 'tests each element of an array passed to -InputObject' -Tag 'Bug06' {
			Test-AllObject -InputObject @(1, 2) { $_ -is [int] } | Should-BeTrue
		}
	}

	Context 'Empty input' {
		# Every element of an empty input passes, as List[T].TrueForAll and LINQ's All decide.
		It 'returns $true for <Label>' -Tag 'Bug13' -ForEach @(
			@{ Label = 'an empty pipeline'; Command = { @() | Test-AllObject { $_ -is [int] } } }
			@{ Label = '-InputObject @()'; Command = { Test-AllObject -InputObject @() -Condition { $_ -is [int] } } }
			@{ Label = '-InputObject $null'; Command = { Test-AllObject -InputObject $null -Condition { $_ -is [int] } } }
		) {
			& $Command | Should-BeTrue
		}

		It 'tests a piped $null as an element' -Tag 'Bug13' {
			$null | Test-AllObject { $_ -is [int] } | Should-BeFalse
		}
	}

	Context 'ScriptBlockErrorAction' {
		It 'accepts -ScriptErrorAction as an alias' -Tag 'Bug16' {
			# Stop turns the error that the condition writes into a terminating error, so the alias reached the parameter.
			{ 1 | Test-AllObject { if ($_) { Write-Error 'oops' } } -ScriptErrorAction Stop } | Should-Throw -ExceptionMessage '*oops*'
		}
	}

	# With -ScriptBlockErrorAction Stop or Continue, errors from -Condition reach PowerShell unchanged, so each result is
	# what ForEach-Object gives for the same script block in both editions. Those scripts run in a new runspace, because
	# Pester's try block would catch both kinds of error. With SilentlyContinue, the default, the command writes each error
	# as a warning instead, which no try block changes.
	Context 'Errors in Condition' {
		# The element whose condition fails with an error doesn't satisfy it, so not every element does.
		It 'writes an error from -Condition as a warning, and returns $false' {
			1, 2 | Test-AllObject { if ($_ -eq 2) { throw 'boom' }; $true } -WarningVariable warnings -WarningAction SilentlyContinue |
				Should-BeFalse
			$warnings.Count | Should-Be 1
			$warnings[0].Message | Should-Be 'boom'
		}

		It 'ends the script when -Condition <Label>' -Tag 'Bug21' -ForEach @(
			@{ Label = 'writes an error under -ScriptBlockErrorAction Stop'; Condition = "{ if (`$_) { Write-Error 'oops' } }"; Action = 'Stop'; ErrorId = 'Microsoft.PowerShell.Commands.WriteErrorException' }
			@{ Label = 'throws'; Condition = "{ if (`$_) { throw 'boom' } }"; Action = 'Continue'; ErrorId = 'boom' }
		) {
			$result = Invoke-InNewRunspace "1 | Test-AllObject $Condition -ScriptBlockErrorAction $Action; 'still running'"
			$result.StoppedBy.FullyQualifiedErrorId | Should-Be $ErrorId
		}

		It 'ends only the statement when a method call in -Condition fails' -Tag 'Bug21' {
			$result = Invoke-InNewRunspace "1 | Test-AllObject { if (`$_) { `$null.Foo() } } -ScriptBlockErrorAction Stop; 'still running'"
			$result.StoppedBy | Should-BeNull
			Should-BeCollection -Expected @('still running') -Actual $result.Output
			$result.Errors.Count | Should-Be 1
			# PowerShell keeps the error ID and category of the failed call, and adds the command.
			$result.Errors[0].FullyQualifiedErrorId | Should-Be 'InvokeMethodOnNull,ListFunctions.Cmdlets.Assertions.TestAllObjectCmdlet'
			$result.Errors[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidOperation)
		}

		It 'leaves the enclosing loop when -Condition runs break' -Tag 'Bug21' {
			$result = Invoke-InNewRunspace "foreach (`$i in 1..2) { `$i; 1 | Test-AllObject { if (`$_) { break } } }; 'after the loop'"
			$result.Errors.Count | Should-Be 0
			Should-BeCollection -Expected @(1, 'after the loop') -Actual $result.Output
		}
	}
}
