BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'New-Dictionary' {
	It "doesn't end the process when [int] keys get script block equality" -Tag 'Bug14' {
		# The test passes if control comes back. Debug builds used to end the process here through Debug.Fail.
		try {
			$null = New-Dictionary [int] -EqualityScript { $x -eq $y } -HashCodeScript { $_.GetHashCode() }
		}
		catch {
			# Whether this call should throw is bug 09.
		}
	}
}
