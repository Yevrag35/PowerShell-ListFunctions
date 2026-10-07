BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Invoke-InNewRunspace.ps1"
}

Describe 'Find-IndexOf' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 3 | Find-IndexOf { $args[0] -eq 2 } | Should-Be 1
	}

	Context 'Pipeline input' {
		It 'counts a piped <Name> as one element' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null, 3) }
			@{ Name = 'empty array'; Items = @(1, @(), 3) }
			@{ Name = 'array'; Items = @(1, @(2, 2), 3) }
		) {
			$Items | Find-IndexOf { $_ -eq 3 } | Should-Be 2
		}

		It 'passes a piped <Name> to -Condition' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null, 3); Condition = { $null -eq $_ } }
			@{ Name = 'array'; Items = @(1, @(2, 2), 3); Condition = { $_ -is [array] } }
		) {
			$Items | Find-IndexOf $Condition | Should-Be 1
		}

		It 'counts the elements of an array passed to -InputObject' -Tag 'Bug06' {
			Find-IndexOf -InputObject @(1, $null, 3) { $_ -eq 3 } | Should-Be 2
		}

		# -InputObject supplies the elements that piping the same value sends, so a set, which isn't a list, supplies its
		# elements too, and $set | Find-IndexOf { $_ -eq 2 } is 1 as well.
		It 'searches the elements of a set passed to -InputObject' {
			$set = 1, 2, 3 | New-HashSet [int]
			Find-IndexOf -InputObject $set { $_ -eq 2 } | Should-Be 1
		}

		# The pipeline enumerates an enumerator, such as the one that a dictionary's GetEnumerator() method returns, and the
		# warning for a dictionary suggests passing one.
		It 'searches the entries that a dictionary enumerator passed to -InputObject supplies' {
			$dict = [ordered]@{ a = 1; b = 2 }
			Find-IndexOf -InputObject $dict.GetEnumerator() { $_.Key -eq 'b' } | Should-Be 1
		}

		# A value that isn't a collection is one element, as it is in the pipeline, and passing one to -InputObject writes a
		# warning. A custom object's warning names PSCustomObject, not the PSObject around it.
		It 'treats a <Name> passed to -InputObject as one element, with a warning' -ForEach @(
			@{ Name = 'number'; Value = 5; Condition = { $_ -eq 5 }; Message = "*of type 'System.Int32' isn't a collection*" }
			@{ Name = 'hashtable'; Value = @{ a = 1 }; Condition = { $_ -is [hashtable] }; Message = '*GetEnumerator()*' }
			@{ Name = 'custom object'; Value = [pscustomobject]@{ Name = 'Ann' }; Condition = { $_.Name -eq 'Ann' }; Message = "*of type 'System.Management.Automation.PSCustomObject'*" }
		) {
			Find-IndexOf -InputObject $Value $Condition -WarningVariable warnings -WarningAction SilentlyContinue | Should-Be 0
			$warnings.Count | Should-Be 1
			$warnings[0].Message | Should-BeLikeString -Expected $Message
		}

		# PowerShell never treats a string as a collection, so nobody expects one to supply its characters.
		It 'treats a string passed to -InputObject as one element, without a warning' {
			Find-IndexOf -InputObject 'abc' { $_ -eq 'abc' } -WarningVariable warnings -WarningAction SilentlyContinue | Should-Be 0
			$warnings.Count | Should-Be 0
		}

		It 'does not warn for a piped value or for a value in an array passed to -InputObject' {
			@{ a = 1 } | Find-IndexOf { $_ -is [hashtable] } -WarningVariable pipedWarnings -WarningAction SilentlyContinue | Should-Be 0
			Find-IndexOf -InputObject @(5) { $_ -eq 5 } -WarningVariable arrayWarnings -WarningAction SilentlyContinue | Should-Be 0
			$pipedWarnings.Count + $arrayWarnings.Count | Should-Be 0
		}
	}

	Context 'ScriptBlockErrorAction' {
		It 'accepts -ScriptErrorAction as an alias' -Tag 'Bug16' {
			# Stop turns the error that the condition writes into a terminating error, so the alias reached the parameter.
			{ 1 | Find-IndexOf { if ($_) { Write-Error 'oops' } } -ScriptErrorAction Stop } | Should-Throw -ExceptionMessage '*oops*'
		}
	}

	# With -ScriptBlockErrorAction Stop or Continue, errors from -Condition reach PowerShell unchanged, so each result is
	# what ForEach-Object gives for the same script block in both editions. Those scripts run in a new runspace, because
	# Pester's try block would catch both kinds of error. With SilentlyContinue, the default, the command writes each error
	# as a warning instead, which no try block changes.
	Context 'Errors in Condition' {
		# The element whose condition fails with an error doesn't match, so the search goes on with the next element.
		# Before, PowerShell hid the error, and the condition went on to match 2.
		It 'writes an error from -Condition as a warning, and goes on with the next element' {
			1, 2, 3 | Find-IndexOf { if ($_ -eq 2) { Write-Error 'oops' }; $_ -ge 2 } -WarningVariable warnings -WarningAction SilentlyContinue |
				Should-Be 2
			$warnings.Count | Should-Be 1
			$warnings[0].Message | Should-Be 'oops'
		}

		It 'ends the script when -Condition <Label>' -Tag 'Bug21' -ForEach @(
			@{ Label = 'writes an error under -ScriptBlockErrorAction Stop'; Condition = "{ if (`$_) { Write-Error 'oops' } }"; Action = 'Stop'; ErrorId = 'Microsoft.PowerShell.Commands.WriteErrorException' }
			@{ Label = 'throws'; Condition = "{ if (`$_) { throw 'boom' } }"; Action = 'Continue'; ErrorId = 'boom' }
		) {
			$result = Invoke-InNewRunspace "1 | Find-IndexOf $Condition -ScriptBlockErrorAction $Action; 'still running'"
			$result.StoppedBy.FullyQualifiedErrorId | Should-Be $ErrorId
		}

		It 'ends only the statement when a method call in -Condition fails' -Tag 'Bug21' {
			$result = Invoke-InNewRunspace "1 | Find-IndexOf { if (`$_) { `$null.Foo() } } -ScriptBlockErrorAction Stop; 'still running'"
			$result.StoppedBy | Should-BeNull
			Should-BeCollection -Expected @('still running') -Actual $result.Output
			$result.Errors.Count | Should-Be 1
			# PowerShell keeps the error ID and category of the failed call, and adds the command.
			$result.Errors[0].FullyQualifiedErrorId | Should-Be 'InvokeMethodOnNull,ListFunctions.Cmdlets.Finds.FindIndexCmdlet'
			$result.Errors[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidOperation)
		}

		It 'leaves the enclosing loop when -Condition runs break' -Tag 'Bug21' {
			$result = Invoke-InNewRunspace "foreach (`$i in 1..2) { `$i; 1 | Find-IndexOf { if (`$_) { break } } }; 'after the loop'"
			$result.Errors.Count | Should-Be 0
			Should-BeCollection -Expected @(1, 'after the loop') -Actual $result.Output
		}
	}
}
