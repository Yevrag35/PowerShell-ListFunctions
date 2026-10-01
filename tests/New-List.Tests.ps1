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
	}
}
