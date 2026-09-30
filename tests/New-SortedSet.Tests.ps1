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

	It "doesn't end the process when -ComparingScript returns something that isn't an [int]" -Tag 'Bug14' {
		# The test passes if control comes back. Debug builds used to end the process here through Debug.Fail.
		try {
			$null = 5, 3, 1 | New-SortedSet [int] -ComparingScript { 'x' + $x + $y }
		}
		catch {
			# What the command should do with this output is bug 10.
		}
	}
}
