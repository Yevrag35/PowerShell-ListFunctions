BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Get-BucketCount.ps1"
}

Describe 'New-Dictionary' {
	Context 'Script block key equality' {
		It 'treats keys as equal when -EqualityScript matches them and -HashCodeScript gives them the same hash code' -Tag 'Bug01' {
			$dict = New-Dictionary -EqualityScript { [string]::Equals($x, $y, 'OrdinalIgnoreCase') } -HashCodeScript { $_.ToUpperInvariant().GetHashCode() }
			$dict.Add('abc', 1)
			$dict.ContainsKey('ABC') | Should-BeTrue
		}

		It 'passes the keys to -EqualityScript as $args[0] and $args[1]' -Tag 'Bug05' {
			# Every key has the same Id, so each lookup runs -EqualityScript.
			$dict = New-Dictionary -EqualityScript { $args[0].Name -eq $args[1].Name } -HashCodeScript { $_.Id.GetHashCode() }
			$dict.Add([pscustomobject]@{ Id = 1; Name = 'a' }, 1)
			$dict.Add([pscustomobject]@{ Id = 1; Name = 'b' }, 2)
			$dict.Count | Should-Be 2
			$dict.ContainsKey([pscustomobject]@{ Id = 1; Name = 'a' }) | Should-BeTrue
		}

		It 'passes the key to -HashCodeScript as $args[0]' -Tag 'Bug05' {
			$dict = New-Dictionary -EqualityScript { $x -eq $y } -HashCodeScript { $args[0].Length }
			$dict.Comparer.GetHashCode('abcd') | Should-Be 4
		}

		It 'uses -EqualityScript and -HashCodeScript when it copies from -InputObject' -Tag 'Bug07' {
			# Keys of the same length are equal, which a Hashtable's default comparison doesn't do.
			$dict = @{ abc = 1 } | New-Dictionary -EqualityScript { $x.Length -eq $y.Length } -HashCodeScript { $_.Length }
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[object, object]]) -Actual $dict
			$dict['xyz'] | Should-Be 1
		}

		It 'applies -ScriptBlockErrorAction when it copies from -InputObject' -Tag 'Bug07' {
			# With the default of Stop, Write-Error would stop -HashCodeScript, and the entry wouldn't be copied.
			$dict = New-Dictionary -EqualityScript { $x -eq $y } -HashCodeScript { Write-Error 'oops'; $_.Length } -InputObject @{ abc = 1 } -ScriptBlockErrorAction SilentlyContinue
			$dict['abc'] | Should-Be 1
		}

		It 'uses -EqualityScript and -HashCodeScript with [<Name>] keys' -Tag 'Bug09' -ForEach @(
			@{ Name = 'int'; KeyType = [int]; Expected = [System.Collections.Generic.Dictionary[int, object]] }
			@{ Name = 'Nullable[int]'; KeyType = [Nullable[int]]; Expected = [System.Collections.Generic.Dictionary[Nullable[int], object]] }
		) {
			# Keys that end in the same digit are equal.
			$dict = New-Dictionary $KeyType -EqualityScript { $x % 10 -eq $y % 10 } -HashCodeScript { $_ % 10 }
			Should-HaveType -Expected $Expected -Actual $dict
			$dict.Add(1, 'a')
			$dict.ContainsKey(11) | Should-BeTrue
			$dict.ContainsKey(2) | Should-BeFalse
		}

		It 'copies -InputObject into a dictionary with [int] keys and script block equality' -Tag 'Bug09' {
			$dict = @{ 1 = 'a'; 12 = 'b' } | New-Dictionary [int] [string] -EqualityScript { $x % 10 -eq $y % 10 } -HashCodeScript { $_ % 10 }
			$dict.Count | Should-Be 2
			$dict[21] | Should-Be 'a'
			$dict[2] | Should-Be 'b'
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
			$dict = New-Dictionary -EqualityScript $EqualityScript -HashCodeScript $HashCodeScript
			$dict.Add('abc', 1)
			$dict.ContainsKey('ABC') | Should-BeTrue
		}

		# New-Dictionary checks the script blocks when it creates the dictionary, so the error ends the command.
		It 'rejects a <Parameter> that has a begin block' -ForEach @(
			@{ Parameter = '-EqualityScript'; EqualityScript = { begin { } process { $x -eq $y } }; HashCodeScript = { $_.GetHashCode() } }
			@{ Parameter = '-HashCodeScript'; EqualityScript = { $x -eq $y }; HashCodeScript = { begin { } process { $_.GetHashCode() } } }
		) {
			{ New-Dictionary -EqualityScript $EqualityScript -HashCodeScript $HashCodeScript } | Should-Throw -FullyQualifiedErrorId 'System.ArgumentException,*'
		}
	}

	Context 'Copying from InputObject' {
		It 'converts copied values to -ValueType' -Tag 'Bug08' {
			$dict = @{ a = '1' } | New-Dictionary [string] [int]
			$dict.Count | Should-Be 1
			Should-HaveType -Expected ([int]) -Actual $dict['a']
			$dict['a'] | Should-Be 1
		}

		It "writes an error for a <Label> that can't be converted, and copies the other entries" -Tag 'Bug08' -ForEach @(
			@{ Label = 'value'; Source = @{ a = '1'; b = 'x' }; KeyType = [string]; Key = 'a'; Expected = 1 }
			@{ Label = 'key'; Source = @{ x = 1; 2 = 2 }; KeyType = [int]; Key = 2; Expected = 2 }
		) {
			$dict = $Source | New-Dictionary $KeyType ([int]) -ErrorVariable err -ErrorAction SilentlyContinue
			$dict.Count | Should-Be 1
			$dict[$Key] | Should-Be $Expected
			$err.Count | Should-Be 1
			Should-HaveType -Expected ([ListFunctions.Exceptions.LFInvalidCastException]) -Actual $err[0].Exception
			$err[0].TargetObject | Should-Be 'x'
		}

		It 'writes an error that targets the key of each entry it fails to add' {
			# 1 and '1' are different hashtable keys that both convert to [int] 1, and so are 2 and '2'. A hash literal
			# can't hold both, so the indexer adds them.
			$source = @{}
			$source[1] = 'a'
			$source['1'] = 'b'
			$source[2] = 'c'
			$source['2'] = 'd'
			$dict = $source | New-Dictionary ([int]) -ErrorVariable err -ErrorAction SilentlyContinue
			$dict.Count | Should-Be 2
			$err.Count | Should-Be 2
			# The hashtable decides which entry of each pair comes second and fails, so the keys are sorted.
			Should-BeCollection -Expected @(1, 2) -Actual ([object[]]($err.TargetObject | Sort-Object))
		}

		It 'writes the exception that Add throws for an entry it fails to add' {
			$source = @{}
			$source[1] = 'a'
			$source['1'] = 'b'
			$null = $source | New-Dictionary ([int]) -ErrorVariable err -ErrorAction SilentlyContinue
			$err.Count | Should-Be 1
			# Should-HaveType would print the whole exception on failure, which takes minutes. A type name prints quickly.
			$err[0].Exception.GetType().FullName | Should-Be 'System.ArgumentException'
		}

		# Each expected value is what Add($key, $null) stores in a dictionary with the same value type, in both editions.
		# These entries used to be skipped.
		It 'copies an entry whose value is $null as <Label> when -ValueType is [<TypeName>]' -ForEach @(
			@{ TypeName = 'object'; Type = [object]; Label = '$null'; Expected = $null }
			@{ TypeName = 'string'; Type = [string]; Label = "''"; Expected = '' }
			@{ TypeName = 'int'; Type = [int]; Label = '0'; Expected = 0 }
			@{ TypeName = 'Nullable[int]'; Type = [Nullable[int]]; Label = '$null'; Expected = $null }
		) {
			$dict = @{ a = $null; b = 1 } | New-Dictionary ([string]) $Type
			$dict.Count | Should-Be 2
			Should-Be -Expected $Expected -Actual $dict['a']
		}

		It 'clones values with -CloneValues before it converts them' -Tag 'Bug08' {
			$source = @{ Items = [System.Collections.ArrayList]@(1, 2) }
			$dict = $source | New-Dictionary [string] ([System.Collections.ArrayList]) -CloneValues
			Should-BeCollection -Expected @(1, 2) -Actual ([object[]]$dict['Items'])
			[object]::ReferenceEquals($source.Items, $dict['Items']) | Should-BeFalse
		}
	}

	Context 'CaseSensitive' {
		It 'copies into a dictionary with case-sensitive [<KeyName>] keys when the input is <Label>' -Tag 'Bug18' -ForEach @(
			@{ KeyName = 'string'; KeyType = [string]; Label = 'piped'; Piped = $true }
			@{ KeyName = 'string'; KeyType = [string]; Label = 'passed to -InputObject'; Piped = $false }
			@{ KeyName = 'object'; KeyType = [object]; Label = 'piped'; Piped = $true }
			@{ KeyName = 'object'; KeyType = [object]; Label = 'passed to -InputObject'; Piped = $false }
		) {
			$source = @{ a = 1 }
			if ($Piped) {
				$dict = $source | New-Dictionary $KeyType -CaseSensitive
			}
			else {
				$dict = New-Dictionary $KeyType -CaseSensitive -InputObject $source
			}
			$dict.Count | Should-Be 1
			$dict['a'] | Should-Be 1
			$dict.ContainsKey('A') | Should-BeFalse
		}

		It 'combines -CaseSensitive with -CloneValues' -Tag 'Bug18' {
			$source = @{ Items = [System.Collections.ArrayList]@(1, 2) }
			$dict = $source | New-Dictionary [string] -CaseSensitive -CloneValues
			[object]::ReferenceEquals($source.Items, $dict['Items']) | Should-BeFalse
			$dict.ContainsKey('items') | Should-BeFalse
		}

		It 'creates an empty dictionary with -CaseSensitive and no input' -Tag 'Bug18' {
			$dict = New-Dictionary [string] -CaseSensitive
			$dict.Count | Should-Be 0
			$dict['a'] = 1
			$dict.ContainsKey('A') | Should-BeFalse
		}
	}

	# New-Dictionary used to create a Hashtable when both types were [object].
	Context 'Dictionary type' {
		It 'creates a Dictionary[object, object] by default when <Label>' -ForEach @(
			@{ Label = 'there is no input'; Piped = $false }
			@{ Label = 'it copies a piped hashtable'; Piped = $true }
		) {
			if ($Piped) {
				$dict = @{ a = 1 } | New-Dictionary
			}
			else {
				$dict = New-Dictionary
			}
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[object, object]]) -Actual $dict
		}
	}

	# [object] keys compare the same way whatever the value type is: two strings ordinally and without regard to case
	# unless -CaseSensitive is set, and any other two keys with their own Equals method.
	Context 'Object keys' {
		It 'compares string keys without regard to case when -ValueType is [<Name>]' -Tag 'Bug20' -ForEach @(
			@{ Name = 'object'; ValueType = [object] }
			@{ Name = 'int'; ValueType = [int] }
		) {
			$dict = New-Dictionary -ValueType $ValueType
			$dict['a'] = 1
			$dict['A'] = 2
			$dict.Count | Should-Be 1
			$dict['a'] | Should-Be 2
		}

		It 'compares string keys with regard to case with -CaseSensitive when -ValueType is [<Name>]' -Tag 'Bug20' -ForEach @(
			@{ Name = 'object'; ValueType = [object] }
			@{ Name = 'int'; ValueType = [int] }
		) {
			$dict = New-Dictionary -ValueType $ValueType -CaseSensitive
			$dict['a'] = 1
			$dict['A'] = 2
			$dict.Count | Should-Be 2
		}

		It "keeps 1 and '1' apart when -ValueType is [<Name>]" -Tag 'Bug20' -ForEach @(
			@{ Name = 'object'; ValueType = [object] }
			@{ Name = 'int'; ValueType = [int] }
		) {
			$dict = New-Dictionary -ValueType $ValueType
			$dict[1] = 1
			$dict['1'] = 2
			$dict.Count | Should-Be 2
		}
	}

	Context 'Capacity' {
		It 'passes -Capacity to a <Description>' -Tag 'Bug02' -ForEach @(
			@{ Description = 'Dictionary[object, object]'; Parameters = @{} }
			@{ Description = 'Dictionary[string, int]'; Parameters = @{ KeyType = '[string]'; ValueType = '[int]' } }
			@{ Description = 'dictionary with script block key equality'; Parameters = @{ EqualityScript = { $x -eq $y }; HashCodeScript = { $_.GetHashCode() } } }
		) {
			$dict = New-Dictionary @Parameters -Capacity 1000
			Get-BucketCount $dict | Should-BeGreaterThanOrEqual 1000
		}

		It "doesn't end the process when the dictionary can't be constructed" -Tag 'Bug14' {
			# The test passes if control comes back. Debug builds used to end the process here through Debug.Fail. No
			# array can hold [int]::MaxValue buckets, so the dictionary's constructor fails before it allocates anything.
			# Until bug 09 was fixed, the test ran 09's repro, which failed in the same place.
			{ New-Dictionary [string] [int] -Capacity ([int]::MaxValue) } | Should-Throw
		}
	}

	Context 'KeyType' {
		# PowerShell passes [string],[int] to -KeyType as one array argument. It doesn't name a key type and a value type.
		It 'rejects [string],[int] instead of making [string] the key type' -Tag 'Bug11' {
			$message = [WildcardPattern]::Escape("'[string],[int]' is not a valid .NET or custom-defined type.")
			{ New-Dictionary [string],[int] } | Should-Throw -ExceptionMessage "*$message"
		}
	}
}
