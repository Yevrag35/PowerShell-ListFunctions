BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'New-List' {
	Context 'IncludeNullElements' {
		# @($list) fails with "Argument types do not match" for the List[object] that New-List outputs, so the tests
		# cast the list to object[] instead.

		It 'adds $null elements to a list without -GenericType' -Tag 'Bug04' {
			$list = New-List -InputObject 1, $null, 2 -IncludeNullElements
			Should-HaveType -Expected ([System.Collections.Generic.List[object]]) -Actual $list
			Should-BeCollection -Expected @(1, $null, 2) -Actual ([object[]]$list)
		}

		It 'adds $null elements to an [object] list' -Tag 'Bug04' {
			$list = New-List ([object]) -InputObject 1, $null, 2 -IncludeNullElements
			Should-BeCollection -Expected @(1, $null, 2) -Actual ([object[]]$list)
		}

		It 'skips $null elements when -IncludeNullElements is absent' -Tag 'Bug04' {
			$list = New-List -InputObject 1, $null, 2
			Should-BeCollection -Expected @(1, 2) -Actual ([object[]]$list)
		}

		# Each expected value is what List[T].Add($null) stores when PowerShell calls it: PowerShell converts $null to 0
		# for [int] and to '' for [string], and passes $null as it is for Nullable[T] and for most other classes, such as
		# [version].
		It 'converts $null elements in a <Name> list the way List[T].Add does' -Tag 'Bug04' -ForEach @(
			@{ Name = '[int]'; Type = [int]; Items = @(1, $null, 2); Expected = @(1, 0, 2) }
			@{ Name = '[string]'; Type = [string]; Items = @('a', $null, 'b'); Expected = @('a', '', 'b') }
			@{ Name = '[Nullable[int]]'; Type = [Nullable[int]]; Items = @(1, $null, 2); Expected = @(1, $null, 2) }
			@{ Name = '[version]'; Type = [version]; Items = @('1.0', $null, '2.0'); Expected = @([version]'1.0', $null, [version]'2.0') }
		) {
			$list = New-List $Type -InputObject $Items -IncludeNullElements
			Should-BeCollection -Expected $Expected -Actual ([object[]]$list)
		}

		It 'skips $null elements in a <Name> list when -IncludeNullElements is absent' -Tag 'Bug04' -ForEach @(
			@{ Name = '[int]'; Type = [int]; Items = @(1, $null, 2); Expected = @(1, 2) }
			@{ Name = '[string]'; Type = [string]; Items = @('a', $null, 'b'); Expected = @('a', 'b') }
			@{ Name = '[Nullable[int]]'; Type = [Nullable[int]]; Items = @(1, $null, 2); Expected = @(1, 2) }
			@{ Name = '[version]'; Type = [version]; Items = @('1.0', $null, '2.0'); Expected = @([version]'1.0', [version]'2.0') }
		) {
			$list = New-List $Type -InputObject $Items
			Should-BeCollection -Expected $Expected -Actual ([object[]]$list)
		}

		# [NullString]::Value isn't $null, but PowerShell converts it to $null for a [string] list, the way
		# List[string].Add stores it. Only the conversion result decides whether it's added.
		It 'adds [NullString]::Value to a [string] list as $null with -IncludeNullElements' -Tag 'Bug04' {
			$list = New-List ([string]) -InputObject 'a', ([System.Management.Automation.Language.NullString]::Value) -IncludeNullElements
			Should-BeCollection -Expected @('a', $null) -Actual ([object[]]$list)
		}

		It 'skips [NullString]::Value in a [string] list when -IncludeNullElements is absent' -Tag 'Bug04' {
			$list = New-List ([string]) -InputObject 'a', ([System.Management.Automation.Language.NullString]::Value)
			Should-BeCollection -Expected @('a') -Actual ([object[]]$list)
		}

		It 'adds a piped $null to a <Name> list with -IncludeNullElements' -Tag 'Bug06' -ForEach @(
			@{ Name = '[object]'; Type = [object]; Expected = @(1, $null, 2) }
			@{ Name = '[int]'; Type = [int]; Expected = @(1, 0, 2) }
		) {
			$list = 1, $null, 2 | New-List $Type -IncludeNullElements
			Should-BeCollection -Expected $Expected -Actual ([object[]]$list)
		}

		It 'skips a piped $null when -IncludeNullElements is absent' -Tag 'Bug06' {
			$list = 1, $null, 2 | New-List
			Should-BeCollection -Expected @(1, 2) -Actual ([object[]]$list)
		}
	}

	Context 'Pipeline input' {
		It 'adds a piped array as one element' -Tag 'Bug06' {
			$list = @(1, @(2, 3)) | New-List
			$list.Count | Should-Be 2
			Should-BeCollection -Expected @(2, 3) -Actual ([object[]]$list[1])
		}

		It 'adds the elements of an array passed to -InputObject' -Tag 'Bug06' {
			$list = New-List -InputObject @(1, @(2, 3))
			$list.Count | Should-Be 2
		}
	}

	Context 'GenericType' {
		# An unquoted argument, such as System.String or [string], reaches -GenericType as a string.

		It 'creates a List[string] from the unquoted type name System.String' -Tag 'Bug11' {
			$list = New-List System.String
			Should-HaveType -Expected ([System.Collections.Generic.List[string]]) -Actual $list
		}

		It 'creates a list from the type name ''<Name>''' -Tag 'Bug11' -ForEach @(
			@{ Name = 'string'; Expected = [System.Collections.Generic.List[string]] }
			@{ Name = 'int[]'; Expected = [System.Collections.Generic.List[int[]]] }
			@{ Name = 'System.Collections.Generic.List[int]'; Expected = [System.Collections.Generic.List[System.Collections.Generic.List[int]]] }
			@{ Name = 'System.Collections.Generic.KeyValuePair[string, int]'; Expected = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string, int]]] }
			@{ Name = 'System.String, mscorlib'; Expected = [System.Collections.Generic.List[string]] }
		) {
			$list = New-List $Name
			Should-HaveType -Expected $Expected -Actual $list
		}

		It 'creates a List[string] from <Label>' -Tag 'Bug11' -ForEach @(
			@{ Label = 'the string ''[string]'''; Value = '[string]' }
			@{ Label = 'the script block { [string] }'; Value = { [string] } }
			@{ Label = 'the type [string]'; Value = [string] }
		) {
			$list = New-List $Value
			Should-HaveType -Expected ([System.Collections.Generic.List[string]]) -Actual $list
		}

		# Windows PowerShell 5.1 converts 'System.String bad text' to [string], because its [type] conversion ignores
		# whatever follows a type name. -GenericType rejects it in both editions.
		It 'rejects the type name ''<Name>''' -Tag 'Bug11' -ForEach @(
			@{ Name = 'NotAType' }
			@{ Name = 'System.String bad text' }
		) {
			{ New-List $Name } | Should-Throw -ExceptionMessage "*'$Name' is not a valid .NET or custom-defined type."
		}

		# PowerShell splits an unparenthesized argument at each comma, so -GenericType gets the parts as an array of
		# strings, such as '[System.Collections.Generic.KeyValuePair[string' and 'int]]', without the spaces after the
		# commas.
		It 'creates a list from <Label>, which PowerShell splits at each comma' -Tag 'Bug11' -ForEach @(
			@{
				Label = '[System.Collections.Generic.KeyValuePair[string,int]]'
				Command = { New-List [System.Collections.Generic.KeyValuePair[string,int]] }
				Expected = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string, int]]]
			}
			@{
				Label = '[System.Collections.Generic.KeyValuePair[string, int]]'
				Command = { New-List [System.Collections.Generic.KeyValuePair[string, int]] }
				Expected = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string, int]]]
			}
			@{
				Label = 'System.Collections.Generic.KeyValuePair[string,int]'
				Command = { New-List System.Collections.Generic.KeyValuePair[string,int] }
				Expected = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string, int]]]
			}
			@{
				Label = '[System.Collections.Generic.Dictionary[string,System.Collections.Generic.KeyValuePair[int,string]]]'
				Command = { New-List [System.Collections.Generic.Dictionary[string,System.Collections.Generic.KeyValuePair[int,string]]] }
				Expected = [System.Collections.Generic.List[System.Collections.Generic.Dictionary[string, System.Collections.Generic.KeyValuePair[int, string]]]]
			}
		) {
			$list = & $Command
			Should-HaveType -Expected $Expected -Actual $list
		}

		# An array of strings is accepted only when its elements join into a single type name, so two type names don't
		# quietly resolve to the first one.
		It 'rejects <Label>' -Tag 'Bug11' -ForEach @(
			@{ Label = 'the array ''[string]'', ''[int]'''; Value = @('[string]', '[int]'); Message = "'[string],[int]' is not a valid .NET or custom-defined type." }
			@{ Label = 'an array that holds a type'; Value = @([string], '[int]'); Message = "Cannot convert a value of type 'System.Object[]' to a type." }
			@{ Label = 'the number 5'; Value = 5; Message = "Cannot convert a value of type 'System.Int32' to a type." }
		) {
			{ New-List -GenericType $Value } | Should-Throw -ExceptionMessage ('*' + [WildcardPattern]::Escape($Message) + '*')
		}

		It 'creates a List[object] when -GenericType is $null' -Tag 'Bug11' {
			$list = New-List -GenericType $null
			Should-HaveType -Expected ([System.Collections.Generic.List[object]]) -Actual $list
		}
	}
}
