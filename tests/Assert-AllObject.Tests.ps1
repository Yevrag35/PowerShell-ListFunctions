BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'Assert-AllObject' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 3 | Assert-AllObject { $args[0] -gt 0 } | Should-BeTrue
		1, 2, 3 | Assert-AllObject { $args[0] -gt 1 } | Should-BeFalse
	}

	Context 'Pipeline input' {
		It 'passes a piped <Name> to -Condition as one element' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null) }
			@{ Name = 'empty array'; Items = @(1, @()) }
		) {
			# The condition is always true, so the result is True as long as the element reaches it.
			$Items | Assert-AllObject { $true -or $_ } | Should-BeTrue
		}

		It 'passes a piped array to -Condition as one element' -Tag 'Bug06' {
			@(1, @(2, 3)) | Assert-AllObject { $_ -is [int] } | Should-BeFalse
		}

		It 'tests each element of an array passed to -InputObject' -Tag 'Bug06' {
			Assert-AllObject -InputObject @(1, 2) { $_ -is [int] } | Should-BeTrue
		}
	}

	Context 'Empty input' {
		# Every element of an empty input passes, as List[T].TrueForAll and LINQ's All decide.
		It 'returns $true for <Label>' -Tag 'Bug13' -ForEach @(
			@{ Label = 'an empty pipeline'; Command = { @() | Assert-AllObject { $_ -is [int] } } }
			@{ Label = '-InputObject @()'; Command = { Assert-AllObject -InputObject @() -Condition { $_ -is [int] } } }
			@{ Label = '-InputObject $null'; Command = { Assert-AllObject -InputObject $null -Condition { $_ -is [int] } } }
		) {
			& $Command | Should-BeTrue
		}

		It 'tests a piped $null as an element' -Tag 'Bug13' {
			$null | Assert-AllObject { $_ -is [int] } | Should-BeFalse
		}
	}
}
