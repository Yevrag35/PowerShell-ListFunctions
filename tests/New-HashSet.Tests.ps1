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

		It 'passes the elements to -EqualityScript as $args[0] and $args[1]' -Tag 'Bug05' {
			# Every element has the same Id, so each Add after the first runs -EqualityScript.
			$set = New-HashSet -EqualityScript { $args[0].Name -eq $args[1].Name } -HashCodeScript { $_.Id.GetHashCode() }
			$set.Add([pscustomobject]@{ Id = 1; Name = 'a' }) | Should-BeTrue
			$set.Add([pscustomobject]@{ Id = 1; Name = 'b' }) | Should-BeTrue
			$set.Add([pscustomobject]@{ Id = 1; Name = 'a' }) | Should-BeFalse
		}

		It 'passes the element to -HashCodeScript as $args[0]' -Tag 'Bug05' {
			$set = New-HashSet -EqualityScript { $x -eq $y } -HashCodeScript { $args[0].Length }
			$set.Comparer.GetHashCode('abcd') | Should-Be 4
		}

		# ScriptBlock.InvokeWithContext, which runs the script blocks, runs the process block when there is one. It refuses
		# a script block that has a begin block, or both a process block and an end block.
		It 'accepts a <Parameter> that has only a process block' -ForEach @(
			@{
				Parameter = '-EqualityScript'
				EqualityScript = { process { [string]::Equals($x, $y, 'OrdinalIgnoreCase') } }
				HashCodeScript = { $_.ToUpperInvariant().GetHashCode() }
			}
			@{
				Parameter = '-HashCodeScript'
				EqualityScript = { [string]::Equals($x, $y, 'OrdinalIgnoreCase') }
				HashCodeScript = { process { $_.ToUpperInvariant().GetHashCode() } }
			}
		) {
			$set = New-HashSet -EqualityScript $EqualityScript -HashCodeScript $HashCodeScript
			$set.Add('abc') | Should-BeTrue
			$set.Add('ABC') | Should-BeFalse
		}

		It 'rejects a <Parameter> that has a begin block' -ForEach @(
			@{ Parameter = '-EqualityScript'; EqualityScript = { begin { } process { $x -eq $y } }; HashCodeScript = { $_.GetHashCode() } }
			@{ Parameter = '-HashCodeScript'; EqualityScript = { $x -eq $y }; HashCodeScript = { begin { } process { $_.GetHashCode() } } }
		) {
			{ New-HashSet -EqualityScript $EqualityScript -HashCodeScript $HashCodeScript } | Should-Throw -FullyQualifiedErrorId 'ParameterArgumentValidationError,*'
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

	Context 'Pipeline input' {
		It 'adds a piped array to an [object] set as one element' -Tag 'Bug06' {
			$set = @(1, @(2, 3)) | New-HashSet
			$set.Count | Should-Be 2
		}

		It "writes an error for a piped array that can't be converted to the element type" -Tag 'Bug06' {
			$set = @(1, @(2, 3)) | New-HashSet [int] -ErrorVariable err -ErrorAction SilentlyContinue
			Should-BeCollection -Expected @(1) -Actual ([object[]]$set)
			$err.Count | Should-Be 1
		}

		It 'adds a piped $null to an [object] set' -Tag 'Bug06' {
			# -InputObject 1, $null adds $null to an [object] set too.
			$set = 1, $null | New-HashSet
			$set.Count | Should-Be 2
			$set.Contains($null) | Should-BeTrue
		}
	}

	Context 'Conversion' {
		It "writes the error that New-List writes for an element that can't be converted, when the input is <Label>" -ForEach @(
			@{ Label = 'piped'; Piped = $true }
			@{ Label = 'passed to -InputObject'; Piped = $false }
		) {
			if ($Piped) {
				$set = 1, 'abc', 2 | New-HashSet [int] -ErrorVariable err -ErrorAction SilentlyContinue
			}
			else {
				$set = New-HashSet [int] -InputObject 1, 'abc', 2 -ErrorVariable err -ErrorAction SilentlyContinue
			}
			Should-BeCollection -Expected @(1, 2) -Actual ([object[]]$set)
			$err.Count | Should-Be 1
			# Should-HaveType would print the whole exception on failure, which takes minutes. A type name prints quickly.
			$err[0].Exception.GetType().FullName | Should-Be 'ListFunctions.Exceptions.LFInvalidCastException'
			$err[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidType)
			$err[0].TargetObject | Should-Be 'abc'
		}
	}
}
