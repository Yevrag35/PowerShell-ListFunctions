BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'New-SortedSet' {
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
