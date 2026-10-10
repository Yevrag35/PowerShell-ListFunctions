BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Invoke-InNewRunspace.ps1"
}

Describe 'Test-AnyObject' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 3 | Test-AnyObject { $args[0] -gt 2 } | Should-BeTrue
		1, 2, 3 | Test-AnyObject { $args[0] -gt 5 } | Should-BeFalse
	}

	Context 'Condition' {
		# $null is the same as leaving -Condition out, as for every script block parameter. The commands that require a
		# condition reject it instead.
		It 'tests for an element that is not $null when -Condition is $null' {
			1, $null | Test-AnyObject -Condition $null | Should-BeTrue
			$null | Test-AnyObject -Condition $null | Should-BeFalse
		}

		# PowerShell binds the elements to the parameters of a param() block in order, the way it passes them in $args.
		# Parameter validation used to reject a condition that read the element only through a parameter.
		It 'passes each element to the first parameter of a -Condition that has a param() block' {
			1, 2, 3 | Test-AnyObject { param($n) $n -gt 2 } | Should-BeTrue
			1, 2 | Test-AnyObject { param($n) $n -gt 2 } | Should-BeFalse
		}

		# Parameter validation used to reject a function's script block, whose syntax tree is the function's definition.
		It "accepts a function's script block as -Condition" {
			function Test-Big { param($n) $n -gt 2 }
			1, 2, 3 | Test-AnyObject ${function:Test-Big} | Should-BeTrue
			1, 2 | Test-AnyObject ${function:Test-Big} | Should-BeFalse
		}
	}

	Context 'Pipeline input' {
		It 'passes a piped <Name> to -Condition as one element' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null); Condition = { $null -eq $_ } }
			@{ Name = 'array'; Items = @(1, @(2, 3)); Condition = { $_ -is [array] } }
		) {
			$Items | Test-AnyObject $Condition | Should-BeTrue
		}

		It 'tests each element of an array passed to -InputObject' -Tag 'Bug06' {
			Test-AnyObject -InputObject @(1, $null) { $null -eq $_ } | Should-BeTrue
		}
	}

	Context 'ScriptBlockErrorAction' {
		It 'accepts -ScriptErrorAction as an alias' -Tag 'Bug16' {
			# Stop turns the error that the condition writes into a terminating error, so the alias reached the parameter.
			{ 1 | Test-AnyObject { if ($_) { Write-Error 'oops' } } -ScriptErrorAction Stop } | Should-Throw -ExceptionMessage '*oops*'
		}
	}

	# With -ScriptBlockErrorAction Stop or Continue, errors from -Condition reach PowerShell unchanged, so each result is
	# what ForEach-Object gives for the same script block in both editions. Those scripts run in a new runspace, because
	# Pester's try block would catch both kinds of error. With SilentlyContinue, the default, or Ignore, the command writes
	# each error as a warning instead, which no try block changes.
	Context 'Errors in Condition' {
		# The command runs -Condition under Stop, so the rest of the condition doesn't run, and the element doesn't match.
		# Before, PowerShell hid the error, and the condition went on to output $true.
		It 'writes the error as a warning, and the element does not match, when -Condition <Label>' -ForEach @(
			@{ Label = 'writes an error'; Condition = { if ($_) { Write-Error 'oops' }; $true }; Message = 'oops' }
			@{ Label = 'throws'; Condition = { if ($_) { throw 'boom' }; $true }; Message = 'boom' }
			@{ Label = 'calls a method on $null'; Condition = { if ($_) { $null.Foo() }; $true }; Message = 'You cannot call a method on a null-valued expression.' }
		) {
			1 | Test-AnyObject $Condition -WarningVariable warnings -WarningAction SilentlyContinue | Should-BeFalse
			$warnings.Count | Should-Be 1
			$warnings[0].Message | Should-Be $Message
		}

		# Windows PowerShell 5.1 doesn't support Ignore as the value of $ErrorActionPreference, but the command runs the
		# condition under Stop for it too.
		It 'writes the error as a warning under -ScriptBlockErrorAction Ignore' {
			1 | Test-AnyObject { if ($_) { Write-Error 'oops' }; $true } -ScriptBlockErrorAction Ignore -WarningVariable warnings -WarningAction SilentlyContinue |
				Should-BeFalse
			$warnings.Count | Should-Be 1
		}

		# Only an error that would go unseen becomes a warning. PowerShell records these handled errors in $Error too, so
		# the warnings can't come from there.
		It 'does not warn for an error that -Condition handles <Label>' -ForEach @(
			@{ Label = 'in a try block'; Condition = { try { throw $_ } catch { $true } } }
			@{ Label = 'with -ErrorAction SilentlyContinue'; Condition = { $null -eq (Get-Item -LiteralPath $_ -ErrorAction SilentlyContinue) } }
			@{ Label = 'with -ErrorAction Ignore'; Condition = { $null -eq (Get-Item -LiteralPath $_ -ErrorAction Ignore) } }
		) {
			"$TestDrive/missing.txt" | Test-AnyObject $Condition -WarningVariable warnings -WarningAction SilentlyContinue | Should-BeTrue
			$warnings.Count | Should-Be 0
		}

		It 'ends the script when -Condition <Label>' -Tag 'Bug21' -ForEach @(
			@{ Label = 'writes an error under -ScriptBlockErrorAction Stop'; Condition = "{ if (`$_) { Write-Error 'oops' } }"; Action = 'Stop'; ErrorId = 'Microsoft.PowerShell.Commands.WriteErrorException' }
			@{ Label = 'throws'; Condition = "{ if (`$_) { throw 'boom' } }"; Action = 'Continue'; ErrorId = 'boom' }
		) {
			$result = Invoke-InNewRunspace "1 | Test-AnyObject $Condition -ScriptBlockErrorAction $Action; 'still running'"
			$result.StoppedBy.FullyQualifiedErrorId | Should-Be $ErrorId
		}

		It 'ends only the statement when a method call in -Condition fails' -Tag 'Bug21' {
			$result = Invoke-InNewRunspace "1 | Test-AnyObject { if (`$_) { `$null.Foo() } } -ScriptBlockErrorAction Stop; 'still running'"
			$result.StoppedBy | Should-BeNull
			Should-BeCollection -Expected @('still running') -Actual $result.Output
			$result.Errors.Count | Should-Be 1
			# PowerShell keeps the error ID and category of the failed call, and adds the command.
			$result.Errors[0].FullyQualifiedErrorId | Should-Be 'InvokeMethodOnNull,ListFunctions.Cmdlets.Assertions.TestAnyObjectCmdlet'
			$result.Errors[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidOperation)
		}

		It 'leaves the enclosing loop when -Condition runs break' -Tag 'Bug21' {
			$result = Invoke-InNewRunspace "foreach (`$i in 1..2) { `$i; 1 | Test-AnyObject { if (`$_) { break } } }; 'after the loop'"
			$result.Errors.Count | Should-Be 0
			Should-BeCollection -Expected @(1, 'after the loop') -Actual $result.Output
		}
	}
}
