BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Get-BucketCount.ps1"
	. "$PSScriptRoot/Invoke-InNewRunspace.ps1"
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

		# -GenericType used to be left out of the parameter set of -EqualityScript and -HashCodeScript, so a set that used
		# them always held [object] elements. New-Dictionary's -KeyType could already be combined with them.
		It 'uses -EqualityScript and -HashCodeScript with [<Name>] elements' -ForEach @(
			@{ Name = 'int'; GenericType = [int]; Expected = [System.Collections.Generic.HashSet[int]] }
			@{ Name = 'Nullable[int]'; GenericType = [Nullable[int]]; Expected = [System.Collections.Generic.HashSet[Nullable[int]]] }
		) {
			# Elements that end in the same digit are equal, so 11 and 21 are duplicates of 1.
			$set = 1, 11, 2, 21 | New-HashSet $GenericType -EqualityScript { $x % 10 -eq $y % 10 } -HashCodeScript { $_ % 10 }
			Should-HaveType -Expected $Expected -Actual $set
			Should-BeCollection -Expected @(1, 2) -Actual ([object[]]$set)
		}

		# A [string] set is also offered -CaseSensitive, which isn't in the parameter set of the script blocks.
		It 'uses -EqualityScript and -HashCodeScript with [string] elements' {
			# Strings of the same length are equal, which the default comparison of [string] elements doesn't do.
			$set = 'ab', 'xyz', 'cd' | New-HashSet [string] -EqualityScript { $x.Length -eq $y.Length } -HashCodeScript { $_.Length }
			Should-HaveType -Expected ([System.Collections.Generic.HashSet[string]]) -Actual $set
			Should-BeCollection -Expected @('ab', 'xyz') -Actual ([object[]]$set)
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

	# [object] elements compare the way New-Dictionary's [object] keys do: two strings ordinally and without regard to
	# case, and any other two elements with their own Equals method, without a conversion. They used to compare like the
	# -eq operator, which converts the second value to the first one's type, and which compares strings by culture.
	Context 'Object elements' {
		It 'keeps <Label> apart' -ForEach @(
			@{ Label = "1 and '1'"; Elements = @(1, '1') }
			@{ Label = '[int] 1 and [long] 1'; Elements = @(1, [long]1) }
			@{ Label = '1 and 1.0'; Elements = @(1, 1.0) }
			@{ Label = 'two strings that differ only by a soft hyphen'; Elements = @('ab', ('a' + [char]0xAD + 'b')) }
		) {
			$set = $Elements | New-HashSet
			$set.Count | Should-Be 2
		}

		It "treats 'a' and 'A' as the same element" {
			$set = 'a', 'A' | New-HashSet
			$set.Count | Should-Be 1
		}

		It 'keeps a decomposed and a precomposed accented e apart with -CaseSensitive' {
			$set = ('e' + [char]0x301), [string][char]0xE9 | New-HashSet -CaseSensitive
			$set.Count | Should-Be 2
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

		# -CaseSensitive compares ordinally, as the comparison without it does. A culture-sensitive comparison treats each
		# of these pairs as equal.
		It 'keeps <Label> apart in a [string] set with -CaseSensitive' -ForEach @(
			@{ Label = 'a decomposed and a precomposed accented e'; Elements = @(('e' + [char]0x301), [string][char]0xE9) }
			@{ Label = 'two strings that differ only by a soft hyphen'; Elements = @('ab', ('a' + [char]0xAD + 'b')) }
		) {
			$set = $Elements | New-HashSet [string] -CaseSensitive
			$set.Count | Should-Be 2
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

		# Unlike an [object] set, a typed set skips $null instead of converting it, so it doesn't hold a value that wasn't
		# in the input, such as 0 in an [int] set. The README gives the reason for each command's rule.
		It 'skips a piped $null in a [<Name>] set without an error' -ForEach @(
			@{ Name = 'int'; Type = [int]; Elements = @(1, $null); Expected = @(1) }
			@{ Name = 'string'; Type = [string]; Elements = @('a', $null); Expected = @('a') }
		) {
			$set = $Elements | New-HashSet $Type -ErrorVariable err -ErrorAction SilentlyContinue
			Should-BeCollection -Expected $Expected -Actual ([object[]]$set)
			$err.Count | Should-Be 0
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

	Context 'Errors while adding' {
		BeforeAll {
			# HashSet[T].Add calls GetHashCode, so a typed set can't add an instance of this type. PowerShell converts an
			# [int] to it through the constructor.
			Add-Type -TypeDefinition @'
namespace NewHashSetTests
{
	public sealed class ThrowingHashCode
	{
		public ThrowingHashCode(int value)
		{
			this.Value = value;
		}

		public int Value { get; private set; }

		public override bool Equals(object obj)
		{
			return object.ReferenceEquals(this, obj);
		}

		public override int GetHashCode()
		{
			throw new System.InvalidOperationException("GetHashCode always fails.");
		}
	}
}
'@
		}

		# These errors don't come from a script block, so they're non-terminating. Piped input used to stop at the first
		# one, and the command wrote no set.
		It 'writes an error that targets each element a typed set fails to add, and writes the set, when the input is <Label>' -ForEach @(
			@{ Label = 'piped'; Piped = $true }
			@{ Label = 'passed to -InputObject'; Piped = $false }
		) {
			if ($Piped) {
				$set = 1, 2 | New-HashSet ([NewHashSetTests.ThrowingHashCode]) -ErrorVariable err -ErrorAction SilentlyContinue
			}
			else {
				$set = New-HashSet ([NewHashSetTests.ThrowingHashCode]) -InputObject 1, 2 -ErrorVariable err -ErrorAction SilentlyContinue
			}
			$err.Count | Should-Be 2
			# Conversion errors have the InvalidType category, so InvalidOperation shows that these errors come from Add.
			$err[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidOperation)
			$err[0].TargetObject | Should-Be 1
			$err[1].TargetObject | Should-Be 2
			Should-HaveType -Expected ([System.Collections.Generic.HashSet[NewHashSetTests.ThrowingHashCode]]) -Actual $set
			$set.Count | Should-Be 0
		}

		It 'writes the exception that Add throws when a typed set fails to add an element' {
			$null = New-HashSet ([NewHashSetTests.ThrowingHashCode]) -InputObject 1 -ErrorVariable err -ErrorAction SilentlyContinue
			$err.Count | Should-Be 1
			# Should-HaveType would print the whole exception on failure, which takes minutes. A type name prints quickly.
			$err[0].Exception.GetType().FullName | Should-Be 'System.InvalidOperationException'
		}
	}

	# Errors from -EqualityScript and -HashCodeScript reach PowerShell unchanged, so each result is what ForEach-Object
	# gives for the same script block in both editions, and the command writes no set. They used to be non-terminating
	# errors that stopped the command. The scripts run in a new runspace, because Pester's try block would catch both
	# kinds of error. 'a' and 'b' have the same length, so adding 'b' runs -EqualityScript.
	Context 'Errors in script blocks' {
		It 'ends the script when <Parameter> <Label>' -ForEach @(
			@{ Parameter = '-EqualityScript'; Label = 'writes an error under -ScriptBlockErrorAction Stop'; EqualityScript = "{ if (`$x -or `$y) { Write-Error 'oops' }; `$x -eq `$y }"; HashCodeScript = "{ `$_.Length }"; Action = 'Stop'; ErrorId = 'Microsoft.PowerShell.Commands.WriteErrorException' }
			@{ Parameter = '-EqualityScript'; Label = 'throws'; EqualityScript = "{ if (`$x -or `$y) { throw 'boom' }; `$x -eq `$y }"; HashCodeScript = "{ `$_.Length }"; Action = 'Continue'; ErrorId = 'boom' }
			# A wrapper around this error would end only the statement.
			@{ Parameter = '-HashCodeScript'; Label = 'writes an error under -ScriptBlockErrorAction Stop'; EqualityScript = "{ `$x -eq `$y }"; HashCodeScript = "{ if (`$_) { Write-Error 'oops' }; `$_.Length }"; Action = 'Stop'; ErrorId = 'Microsoft.PowerShell.Commands.WriteErrorException' }
		) {
			$result = Invoke-InNewRunspace "'a', 'b' | New-HashSet -EqualityScript $EqualityScript -HashCodeScript $HashCodeScript -ScriptBlockErrorAction $Action; 'still running'"
			$result.StoppedBy.FullyQualifiedErrorId | Should-Be $ErrorId
		}

		It 'ends only the statement when a method call in -EqualityScript fails' {
			$result = Invoke-InNewRunspace "'a', 'b' | New-HashSet -EqualityScript { if (`$x -or `$y) { `$null.Foo() }; `$x -eq `$y } -HashCodeScript { `$_.Length }; 'still running'"
			$result.StoppedBy | Should-BeNull
			Should-BeCollection -Expected @('still running') -Actual $result.Output
			$result.Errors.Count | Should-Be 1
			# PowerShell keeps the error ID and category of the failed call, and adds the command.
			$result.Errors[0].FullyQualifiedErrorId | Should-Be 'InvokeMethodOnNull,ListFunctions.Cmdlets.Constructs.NewHashSetCmdlet'
			$result.Errors[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidOperation)
		}

		# -ErrorAction SilentlyContinue hid the non-terminating error that the command used to write for this output. It
		# can't hide an error that ends the statement.
		It "ends only the statement when the output of -HashCodeScript isn't a hash code, whatever -ErrorAction says" {
			$result = Invoke-InNewRunspace "'a', 'b' | New-HashSet -EqualityScript { `$x -eq `$y } -HashCodeScript { `$_.ToUpperInvariant() } -ErrorAction SilentlyContinue; 'still running'"
			$result.StoppedBy | Should-BeNull
			Should-BeCollection -Expected @('still running') -Actual $result.Output
			$result.Errors.Count | Should-Be 1
			# Should-HaveType would print the whole exception on failure, which takes minutes. A type name prints quickly.
			$result.Errors[0].Exception.GetType().FullName | Should-Be 'ListFunctions.Modern.Exceptions.HashCodeScriptException'
		}

		It 'leaves the enclosing loop when -EqualityScript runs break' {
			$result = Invoke-InNewRunspace "foreach (`$i in 1..2) { `$i; 'a', 'b' | New-HashSet -EqualityScript { if (`$x -or `$y) { break } } -HashCodeScript { `$_.Length } }; 'after the loop'"
			$result.Errors.Count | Should-Be 0
			Should-BeCollection -Expected @(1, 'after the loop') -Actual $result.Output
		}
	}
}
