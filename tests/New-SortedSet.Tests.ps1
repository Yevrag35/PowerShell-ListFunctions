BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Invoke-InNewRunspace.ps1"
}

# Should-BeCollection doesn't compare the order of the elements, so the tests that check an order compare the joined
# elements instead.
Describe 'New-SortedSet' {
	It 'passes the elements to -ComparingScript as $args[0] and $args[1]' -Tag 'Bug05' {
		$set = 5, 3, 1 | New-SortedSet [int] -ComparingScript { $args[0].CompareTo($args[1]) }
		($set -join ', ') | Should-Be '1, 3, 5'
	}

	It 'sorts in reverse when -ComparingScript swaps $args[0] and $args[1]' -Tag 'Bug05' {
		$set = 1, 3, 5 | New-SortedSet [int] -ComparingScript { $args[1].CompareTo($args[0]) }
		($set -join ', ') | Should-Be '5, 3, 1'
	}

	It 'treats a piped array as one element' -Tag 'Bug06' {
		# The array can't be converted to [int], so only 3 is added.
		$set = @(3, @(1, 2)) | New-SortedSet [int] -ErrorAction SilentlyContinue
		Should-BeCollection -Expected @(3) -Actual ([object[]]$set)
	}

	Context 'Element type' {
		# Elements of different types used to sort in a different order for each input order, and the set sometimes
		# couldn't find its own elements.
		It 'converts the elements to [string] when no type is given, so <Order> gives the same set' -ForEach @(
			@{ Order = "1, '10', 9, '2'"; Elements = @(1, '10', 9, '2') }
			@{ Order = "'10', 9, '2', 1"; Elements = @('10', 9, '2', 1) }
			@{ Order = "9, '2', 1, '10'"; Elements = @(9, '2', 1, '10') }
		) {
			$set = $Elements | New-SortedSet
			Should-HaveType -Expected ([System.Collections.Generic.SortedSet[string]]) -Actual $set
			($set -join ', ') | Should-Be '1, 10, 2, 9'
			$set.Contains('10') | Should-BeTrue
		}

		It 'rejects [<Name>] elements without -ComparingScript, before it reads any input' -ForEach @(
			@{ Name = 'object'; Type = [object] }
			@{ Name = 'psobject'; Type = [psobject] }
			# Tuple implements only the non-generic IComparable.
			@{ Name = 'Tuple[int, string]'; Type = [Tuple[int, string]] }
		) {
			$read = [System.Collections.Generic.List[object]]::new()
			{ 1, 2 | ForEach-Object { $read.Add($_); $_ } | New-SortedSet $Type } | Should-Throw -FullyQualifiedErrorId 'System.ArgumentException,*' -ExceptionMessage '*-ComparingScript*'
			$read.Count | Should-Be 0
		}

		# Comparer[T].Default sorts these types by value, although they don't implement IComparable[T] of themselves.
		It 'sorts [<Name>] elements without -ComparingScript' -ForEach @(
			@{ Name = 'ConsoleColor'; Type = [ConsoleColor]; Elements = @('Red', 'Black', 'Blue'); Expected = 'Black, Blue, Red' }
			@{ Name = 'Nullable[int]'; Type = [Nullable[int]]; Elements = @(3, 1, 2); Expected = '1, 2, 3' }
		) {
			$set = $Elements | New-SortedSet $Type
			($set -join ', ') | Should-Be $Expected
		}

		It 'keeps the elements as they are with -ComparingScript and no type' {
			# As strings, both people would have no Id, and the set would keep only one of them.
			$people = [pscustomobject]@{ Id = 2; Name = 'Bob' }, [pscustomobject]@{ Id = 1; Name = 'Ann' }
			$set = $people | New-SortedSet -ComparingScript { $x.Id - $y.Id }
			Should-HaveType -Expected ([System.Collections.Generic.SortedSet[object]]) -Actual $set
			($set.Name -join ', ') | Should-Be 'Ann, Bob'
		}
	}

	# [string] elements compare with OrdinalIgnoreCase, as if they were uppercase, by character code. So '_' and letters
	# outside ASCII sort after 'Z', and the order is the same in every culture and in both editions. They used to sort by
	# the invariant culture, which puts '_' first and the accented e among the other e's.
	Context 'String order' {
		It 'sorts [string] elements by character code without regard to case' {
			$set = 'b', '_x', 'a', ([string][char]0xE9), 'Z' | New-SortedSet
			($set -join ', ') | Should-Be ('a, b, Z, _x, ' + [char]0xE9)
		}

		It "treats 'a' and 'A' as the same element" {
			$set = 'a', 'A' | New-SortedSet
			$set.Count | Should-Be 1
		}
	}

	Context 'Conversion' {
		It "writes an error for an element that can't be converted, and adds the others, when the input is <Label>" -Tag 'Bug17' -ForEach @(
			@{ Label = 'piped'; Piped = $true }
			@{ Label = 'passed to -InputObject'; Piped = $false }
		) {
			if ($Piped) {
				$set = 1, 'abc', 2 | New-SortedSet [int] -ErrorVariable err -ErrorAction SilentlyContinue
			}
			else {
				$set = New-SortedSet [int] -InputObject 1, 'abc', 2 -ErrorVariable err -ErrorAction SilentlyContinue
			}
			Should-BeCollection -Expected @(1, 2) -Actual ([object[]]$set)
			$err.Count | Should-Be 1
			# New-List writes the same error. Should-HaveType would print the whole exception on failure, which takes
			# minutes. A type name prints quickly.
			$err[0].Exception.GetType().FullName | Should-Be 'ListFunctions.Exceptions.LFInvalidCastException'
			$err[0].TargetObject | Should-Be 'abc'
		}

		It 'skips a $null element without an error' -Tag 'Bug17' {
			$set = 1, $null, 2 | New-SortedSet [int] -ErrorVariable err -ErrorAction SilentlyContinue
			Should-BeCollection -Expected @(1, 2) -Actual ([object[]]$set)
			$err.Count | Should-Be 0
		}
	}

	# The output used to give a non-terminating error for each comparison, and a set without those elements.
	It "ends only the statement when the output of -ComparingScript <Label>" -Tag 'Bug10' -ForEach @(
		@{ Label = "can't be converted to [int]"; Script = "{ 'x' + `$x + `$y }" }
		@{ Label = 'is missing'; Script = "{ `$null = `$x, `$y }" }
		@{ Label = 'is $null'; Script = "{ `$null = `$x, `$y; `$null }" }
	) {
		# The first element is added without a comparison, so the error comes from adding the second one.
		$result = Invoke-InNewRunspace "5, 3, 1 | New-SortedSet [int] -ComparingScript $Script; 'still running'"
		$result.StoppedBy | Should-BeNull
		Should-BeCollection -Expected @('still running') -Actual $result.Output
		$result.Errors.Count | Should-Be 1
		# Should-HaveType would print the whole exception on failure, which takes minutes. A type name prints quickly.
		$result.Errors[0].Exception.GetType().FullName | Should-Be 'ListFunctions.Modern.Exceptions.ComparingScriptException'
	}

	It "doesn't end the process when -ComparingScript returns something that isn't an [int]" -Tag 'Bug14' {
		# The test passes if control comes back. Debug builds used to end the process here through Debug.Fail. Since
		# bug 10 was fixed, this output is an error that ends the statement, instead of 0.
		try {
			$null = 5, 3, 1 | New-SortedSet [int] -ComparingScript { 'x' + $x + $y }
		}
		catch {
			# Any error is fine. The test checks only that the process survives.
		}
	}

	# ScriptBlock.InvokeWithContext, which runs -ComparingScript, refuses a script block that has a begin block. The
	# command used to accept one, and wrote an error for each comparison.
	It 'rejects a -ComparingScript that has a begin block' {
		{ 5, 3 | New-SortedSet -ComparingScript { begin { } process { $x - $y } } } | Should-Throw -FullyQualifiedErrorId 'ParameterArgumentValidationError,*'
	}

	# Errors from -ComparingScript reach PowerShell unchanged, so each result is what ForEach-Object gives for the same
	# script block in both editions, and the command writes no set. They used to be non-terminating errors. The scripts
	# run in a new runspace, because Pester's try block would catch both kinds of error.
	Context 'Errors in ComparingScript' {
		It 'ends the script when -ComparingScript <Label>' -ForEach @(
			@{ Label = 'writes an error under -ScriptBlockErrorAction Stop'; Script = "{ if (`$x -or `$y) { Write-Error 'oops' }; `$x.CompareTo(`$y) }"; Action = 'Stop'; ErrorId = 'Microsoft.PowerShell.Commands.WriteErrorException' }
			@{ Label = 'throws'; Script = "{ if (`$x -or `$y) { throw 'boom' }; `$x.CompareTo(`$y) }"; Action = 'Continue'; ErrorId = 'boom' }
		) {
			$result = Invoke-InNewRunspace "5, 3 | New-SortedSet [int] -ComparingScript $Script -ScriptBlockErrorAction $Action; 'still running'"
			$result.StoppedBy.FullyQualifiedErrorId | Should-Be $ErrorId
		}

		It 'ends only the statement when a method call in -ComparingScript fails' {
			$result = Invoke-InNewRunspace "5, 3 | New-SortedSet [int] -ComparingScript { if (`$x -or `$y) { `$null.Foo() }; `$x.CompareTo(`$y) }; 'still running'"
			$result.StoppedBy | Should-BeNull
			Should-BeCollection -Expected @('still running') -Actual $result.Output
			$result.Errors.Count | Should-Be 1
			# PowerShell keeps the error ID and category of the failed call, and adds the command.
			$result.Errors[0].FullyQualifiedErrorId | Should-Be 'InvokeMethodOnNull,ListFunctions.Cmdlets.Constructs.NewSortedSetCmdlet'
			$result.Errors[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidOperation)
		}

		It 'leaves the enclosing loop when -ComparingScript runs break' {
			$result = Invoke-InNewRunspace "foreach (`$i in 1..2) { `$i; 5, 3 | New-SortedSet [int] -ComparingScript { if (`$x -or `$y) { break }; `$x.CompareTo(`$y) } }; 'after the loop'"
			$result.Errors.Count | Should-Be 0
			Should-BeCollection -Expected @(1, 'after the loop') -Actual $result.Output
		}
	}
}
