BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Invoke-InNewRunspace.ps1"
}

Describe 'Find-LastIndexOf' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 2, 3 | Find-LastIndexOf { $args[0] -eq 2 } | Should-Be 2
	}

	Context 'Pipeline input' {
		It 'counts a piped <Name> as one element' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null, 3) }
			@{ Name = 'empty array'; Items = @(1, @(), 3) }
			@{ Name = 'array'; Items = @(1, @(2, 2), 3) }
		) {
			$Items | Find-LastIndexOf { $_ -eq 3 } | Should-Be 2
		}

		It 'passes a piped <Name> to -Condition' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null, 3); Condition = { $null -eq $_ } }
			@{ Name = 'array'; Items = @(1, @(2, 2), 3); Condition = { $_ -is [array] } }
		) {
			$Items | Find-LastIndexOf $Condition | Should-Be 1
		}

		It 'counts the elements of an array passed to -InputObject' -Tag 'Bug06' {
			Find-LastIndexOf -InputObject @(1, $null, 3) { $_ -eq 3 } | Should-Be 2
		}

		# -InputObject supplies the elements that piping the same value sends, so a queue, which isn't a list, supplies its
		# elements too, in the order that it dequeues them.
		It 'searches the elements of a queue passed to -InputObject' {
			$queue = [System.Collections.Generic.Queue[int]]::new()
			foreach ($n in 1, 2, 2, 3) { $queue.Enqueue($n) }
			Find-LastIndexOf -InputObject $queue { $_ -eq 2 } | Should-Be 2
		}
	}

	Context 'ScriptBlockErrorAction' {
		It 'accepts -ScriptErrorAction as an alias' -Tag 'Bug16' {
			# Stop turns the error that the condition writes into a terminating error, so the alias reached the parameter.
			{ 1 | Find-LastIndexOf { if ($_) { Write-Error 'oops' } } -ScriptErrorAction Stop } | Should-Throw -ExceptionMessage '*oops*'
		}
	}

	# With -ScriptBlockErrorAction Stop or Continue, errors from -Condition reach PowerShell unchanged, so each result is
	# what ForEach-Object gives for the same script block in both editions. Those scripts run in a new runspace, because
	# Pester's try block would catch both kinds of error. With SilentlyContinue, the default, the command writes each error
	# as a warning instead, which no try block changes.
	Context 'Errors in Condition' {
		# The element whose condition fails with an error doesn't match, so the search goes on with the element before it.
		It 'writes an error from -Condition as a warning, and goes on with the element before it' {
			1, 2, 3 | Find-LastIndexOf { if ($_ -eq 3) { $null.Foo() }; $true } -WarningVariable warnings -WarningAction SilentlyContinue |
				Should-Be 1
			$warnings.Count | Should-Be 1
		}

		It 'ends the script when -Condition <Label>' -Tag 'Bug21' -ForEach @(
			@{ Label = 'writes an error under -ScriptBlockErrorAction Stop'; Condition = "{ if (`$_) { Write-Error 'oops' } }"; Action = 'Stop'; ErrorId = 'Microsoft.PowerShell.Commands.WriteErrorException' }
			@{ Label = 'throws'; Condition = "{ if (`$_) { throw 'boom' } }"; Action = 'Continue'; ErrorId = 'boom' }
		) {
			$result = Invoke-InNewRunspace "1 | Find-LastIndexOf $Condition -ScriptBlockErrorAction $Action; 'still running'"
			$result.StoppedBy.FullyQualifiedErrorId | Should-Be $ErrorId
		}

		It 'ends only the statement when a method call in -Condition fails' -Tag 'Bug21' {
			$result = Invoke-InNewRunspace "1 | Find-LastIndexOf { if (`$_) { `$null.Foo() } } -ScriptBlockErrorAction Stop; 'still running'"
			$result.StoppedBy | Should-BeNull
			Should-BeCollection -Expected @('still running') -Actual $result.Output
			$result.Errors.Count | Should-Be 1
			# PowerShell keeps the error ID and category of the failed call, and adds the command.
			$result.Errors[0].FullyQualifiedErrorId | Should-Be 'InvokeMethodOnNull,ListFunctions.Cmdlets.Finds.FindLastIndexCmdlet'
			$result.Errors[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidOperation)
		}

		It 'leaves the enclosing loop when -Condition runs break' -Tag 'Bug21' {
			$result = Invoke-InNewRunspace "foreach (`$i in 1..2) { `$i; 1 | Find-LastIndexOf { if (`$_) { break } } }; 'after the loop'"
			$result.Errors.Count | Should-Be 0
			Should-BeCollection -Expected @(1, 'after the loop') -Actual $result.Output
		}
	}
}
