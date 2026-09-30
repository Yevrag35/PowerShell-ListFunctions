BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Get-BucketCount.ps1"
}

Describe 'New-HashSet' {
	Context 'Script block equality' {
		It 'uses the output of -HashCodeScript as the hash code' -Tag 'Bug01' {
			$set = New-HashSet -EqualityScript { $x -eq $y } -HashCodeScript { $_.Length }
			$set.Comparer.GetHashCode('abcd') | Should-Be 4
		}

		It 'converts the output of -HashCodeScript to [int]' -Tag 'Bug01' {
			$set = New-HashSet -EqualityScript { $x -eq $y } -HashCodeScript { [string]$_.Length }
			$set.Comparer.GetHashCode('abcd') | Should-Be 4
		}

		It 'treats elements as equal when -EqualityScript matches them and -HashCodeScript gives them the same hash code' -Tag 'Bug01' {
			$set = New-HashSet -EqualityScript { [string]::Equals($x, $y, 'OrdinalIgnoreCase') } -HashCodeScript { $_.ToUpperInvariant().GetHashCode() }
			$set.Add('abc') | Should-BeTrue
			$set.Add('ABC') | Should-BeFalse
		}

		It 'drops piped elements that -EqualityScript and -HashCodeScript treat as equal' -Tag 'Bug01' {
			$set = 'abc', 'ABC', 'def' | New-HashSet -EqualityScript { [string]::Equals($x, $y, 'OrdinalIgnoreCase') } -HashCodeScript { $_.ToUpperInvariant().GetHashCode() }
			$set.Count | Should-Be 2
		}

		It "fails when the output of -HashCodeScript can't be converted to [int]" -Tag 'Bug01' {
			# The script leaves out GetHashCode(), so it returns a string.
			$set = New-HashSet -EqualityScript { $x -eq $y } -HashCodeScript { $_.ToUpperInvariant() }
			$err = { $set.Add('abc') } | Should-Throw
			Should-HaveType -Expected ([ListFunctions.Modern.Exceptions.HashCodeScriptException]) -Actual $err.Exception.InnerException
		}

		It 'fails when -HashCodeScript has no output' -Tag 'Bug01' {
			$set = New-HashSet -EqualityScript { $x -eq $y } -HashCodeScript { $null = $_ }
			$err = { $set.Add('abc') } | Should-Throw
			Should-HaveType -Expected ([ListFunctions.Modern.Exceptions.HashCodeScriptException]) -Actual $err.Exception.InnerException
		}
	}

	Context 'Capacity' {
		It 'passes -Capacity to a <Description>' -Tag 'Bug02' -ForEach @(
			@{ Description = 'HashSet[object]'; Parameters = @{} }
			@{ Description = 'HashSet[int]'; Parameters = @{ GenericType = '[int]' } }
			@{ Description = 'HashSet[string]'; Parameters = @{ GenericType = '[string]' } }
			@{ Description = 'set with script block equality'; Parameters = @{ EqualityScript = { $x -eq $y }; HashCodeScript = { $_.GetHashCode() } } }
		) {
			$set = New-HashSet @Parameters -Capacity 1000
			Get-BucketCount $set | Should-BeGreaterThanOrEqual 1000
		}
	}

	Context 'CaseSensitive' {
		It 'accepts -CaseSensitive with a [string] element type' -Tag 'Bug03' {
			$set = New-HashSet [string] -CaseSensitive
			Should-HaveType -Expected ([System.Collections.Generic.HashSet[string]]) -Actual $set
			$set.Add('a') | Should-BeTrue
			$set.Add('A') | Should-BeTrue
		}

		It 'accepts -CaseSensitive with an [object] element type' -Tag 'Bug03' {
			$set = New-HashSet ([object]) -CaseSensitive
			Should-HaveType -Expected ([System.Collections.Generic.HashSet[object]]) -Actual $set
			$set.Add('a') | Should-BeTrue
			$set.Add('A') | Should-BeTrue
		}

		It 'accepts -CaseSensitive with a [string] element type and pipeline input' -Tag 'Bug03' {
			$set = 'a', 'A', 'a' | New-HashSet [string] -CaseSensitive
			$set.Count | Should-Be 2
		}

		It 'compares [string] elements without regard to case when -CaseSensitive is absent' -Tag 'Bug03' {
			$set = 'a', 'A' | New-HashSet [string]
			$set.Count | Should-Be 1
		}

		It "doesn't offer -CaseSensitive for other element types" -Tag 'Bug03' {
			{ New-HashSet [int] -CaseSensitive } | Should-Throw -FullyQualifiedErrorId 'NamedParameterNotFound,*'
		}
	}
}
