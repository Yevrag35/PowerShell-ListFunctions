BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Invoke-InNewRunspace.ps1"
}

Describe 'Assert-AnyObject' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 3 | Assert-AnyObject { $args[0] -gt 2 } | Should-BeTrue
		1, 2, 3 | Assert-AnyObject { $args[0] -gt 5 } | Should-BeFalse
	}

	Context 'Pipeline input' {
		It 'passes a piped <Name> to -Condition as one element' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null); Condition = { $null -eq $_ } }
			@{ Name = 'array'; Items = @(1, @(2, 3)); Condition = { $_ -is [array] } }
		) {
			$Items | Assert-AnyObject $Condition | Should-BeTrue
		}

		It 'tests each element of an array passed to -InputObject' -Tag 'Bug06' {
			Assert-AnyObject -InputObject @(1, $null) { $null -eq $_ } | Should-BeTrue
		}
	}

	Context 'ScriptBlockErrorAction' {
		It 'accepts -ScriptErrorAction as an alias' -Tag 'Bug16' {
			# Stop turns the error that the condition writes into a terminating error, so the alias reached the parameter.
			{ 1 | Assert-AnyObject { if ($_) { Write-Error 'oops' } } -ScriptErrorAction Stop } | Should-Throw -ExceptionMessage '*oops*'
		}
	}

	# Errors from -Condition reach PowerShell unchanged, so each result is what ForEach-Object gives for the same script
	# block in both editions. The scripts run in a new runspace, because Pester's try block would catch both kinds of
	# error.
	Context 'Errors in Condition' {
		It 'ends the script when -Condition <Label>' -Tag 'Bug21' -ForEach @(
			@{ Label = 'writes an error under -ScriptBlockErrorAction Stop'; Condition = "{ if (`$_) { Write-Error 'oops' } }"; Action = 'Stop'; ErrorId = 'Microsoft.PowerShell.Commands.WriteErrorException' }
			@{ Label = 'throws'; Condition = "{ if (`$_) { throw 'boom' } }"; Action = 'Continue'; ErrorId = 'boom' }
		) {
			$result = Invoke-InNewRunspace "1 | Assert-AnyObject $Condition -ScriptBlockErrorAction $Action; 'still running'"
			$result.StoppedBy.FullyQualifiedErrorId | Should-Be $ErrorId
		}

		It 'ends only the statement when a method call in -Condition fails' -Tag 'Bug21' {
			$result = Invoke-InNewRunspace "1 | Assert-AnyObject { if (`$_) { `$null.Foo() } } -ScriptBlockErrorAction Stop; 'still running'"
			$result.StoppedBy | Should-BeNull
			Should-BeCollection -Expected @('still running') -Actual $result.Output
			$result.Errors.Count | Should-Be 1
			# PowerShell keeps the error ID and category of the failed call, and adds the command.
			$result.Errors[0].FullyQualifiedErrorId | Should-Be 'InvokeMethodOnNull,ListFunctions.Cmdlets.Assertions.AssertAnyObjectCmdlet'
			$result.Errors[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidOperation)
		}

		It 'leaves the enclosing loop when -Condition runs break' -Tag 'Bug21' {
			$result = Invoke-InNewRunspace "foreach (`$i in 1..2) { `$i; 1 | Assert-AnyObject { if (`$_) { break } } }; 'after the loop'"
			$result.Errors.Count | Should-Be 0
			Should-BeCollection -Expected @(1, 'after the loop') -Actual $result.Output
		}
	}
}
