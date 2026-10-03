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
		$set = @(3, @(1, 2)) | New-SortedSet [int]
		Should-BeCollection -Expected @(3) -Actual ([object[]]$set)
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
