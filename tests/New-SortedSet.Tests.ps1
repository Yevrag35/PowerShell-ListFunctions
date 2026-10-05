BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'New-SortedSet' {
	It 'passes the elements to -ComparingScript as $args[0] and $args[1]' -Tag 'Bug05' {
		$set = 5, 3, 1 | New-SortedSet [int] -ComparingScript { $args[0].CompareTo($args[1]) }
		Should-BeCollection -Expected @(1, 3, 5) -Actual ([object[]]$set)
	}

	It 'sorts in reverse when -ComparingScript swaps $args[0] and $args[1]' -Tag 'Bug05' {
		$set = 1, 3, 5 | New-SortedSet [int] -ComparingScript { $args[1].CompareTo($args[0]) }
		Should-BeCollection -Expected @(5, 3, 1) -Actual ([object[]]$set)
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
			Should-BeCollection -Expected @('1', '10', '2', '9') -Actual ([object[]]$set)
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
			@{ Name = 'ConsoleColor'; Type = [ConsoleColor]; Elements = @('Red', 'Black', 'Blue'); Expected = @([ConsoleColor]::Black, [ConsoleColor]::Blue, [ConsoleColor]::Red) }
			@{ Name = 'Nullable[int]'; Type = [Nullable[int]]; Elements = @(3, 1, 2); Expected = @(1, 2, 3) }
		) {
			$set = $Elements | New-SortedSet $Type
			Should-BeCollection -Expected $Expected -Actual ([object[]]$set)
		}

		It 'keeps the elements as they are with -ComparingScript and no type' {
			# As strings, both people would have no Id, and the set would keep only one of them.
			$people = [pscustomobject]@{ Id = 2; Name = 'Bob' }, [pscustomobject]@{ Id = 1; Name = 'Ann' }
			$set = $people | New-SortedSet -ComparingScript { $x.Id - $y.Id }
			Should-HaveType -Expected ([System.Collections.Generic.SortedSet[object]]) -Actual $set
			Should-BeCollection -Expected @('Ann', 'Bob') -Actual ([object[]]$set.Name)
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

	It "writes an error when the output of -ComparingScript <Label>" -Tag 'Bug10' -ForEach @(
		@{ Label = "can't be converted to [int]"; Script = { 'x' + $x + $y } }
		@{ Label = 'is missing'; Script = { $null = $x, $y } }
		@{ Label = 'is $null'; Script = { $null = $x, $y; $null } }
	) {
		# The first element is added without a comparison. Each of the other two fails.
		$set = 5, 3, 1 | New-SortedSet [int] -ComparingScript $Script -ErrorVariable err -ErrorAction SilentlyContinue
		Should-BeCollection -Expected @(5) -Actual ([object[]]$set)
		$err.Count | Should-Be 2
		Should-HaveType -Expected ([ListFunctions.Modern.Exceptions.ComparingScriptException]) -Actual $err[0].Exception
	}

	It "doesn't end the process when -ComparingScript returns something that isn't an [int]" -Tag 'Bug14' {
		# The test passes if control comes back. Debug builds used to end the process here through Debug.Fail. Since
		# bug 10 was fixed, the command writes an error for this output instead of converting it to 0.
		try {
			$null = 5, 3, 1 | New-SortedSet [int] -ComparingScript { 'x' + $x + $y } -ErrorAction SilentlyContinue
		}
		catch {
			# Any error is fine. The test checks only that the process survives.
		}
	}
}
