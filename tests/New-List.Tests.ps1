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
	}
}
