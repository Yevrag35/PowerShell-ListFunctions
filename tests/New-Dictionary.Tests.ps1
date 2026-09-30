BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Get-BucketCount.ps1"
}

Describe 'New-Dictionary' {
	It 'treats keys as equal when -EqualityScript matches them and -HashCodeScript gives them the same hash code' -Tag 'Bug01' {
		$dict = New-Dictionary -EqualityScript { [string]::Equals($x, $y, 'OrdinalIgnoreCase') } -HashCodeScript { $_.ToUpperInvariant().GetHashCode() }
		$dict.Add('abc', 1)
		$dict.ContainsKey('ABC') | Should-BeTrue
	}

	It 'passes -Capacity to a <Description>' -Tag 'Bug02' -ForEach @(
		@{ Description = 'Hashtable'; Parameters = @{} }
		@{ Description = 'Dictionary[string, int]'; Parameters = @{ KeyType = '[string]'; ValueType = '[int]' } }
		@{ Description = 'dictionary with script block key equality'; Parameters = @{ EqualityScript = { $x -eq $y }; HashCodeScript = { $_.GetHashCode() } } }
	) {
		$dict = New-Dictionary @Parameters -Capacity 1000
		Get-BucketCount $dict | Should-BeGreaterThanOrEqual 1000
	}

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
