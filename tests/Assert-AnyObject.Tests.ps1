BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'Assert-AnyObject' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 3 | Assert-AnyObject { $args[0] -gt 2 } | Should-BeTrue
		1, 2, 3 | Assert-AnyObject { $args[0] -gt 5 } | Should-BeFalse
	}

	Context 'Pipeline input' {
		It 'passes a piped <Name> to -Condition as one element' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null); Condition = { $null -eq $_ } }
			@{ Name = 'array'; Items = @(1, @(2, 3)); Condition = { $_ -is [array] } }
		) {
			$Items | Assert-AnyObject $Condition | Should-BeTrue
		}

		It 'tests each element of an array passed to -InputObject' -Tag 'Bug06' {
			Assert-AnyObject -InputObject @(1, $null) { $null -eq $_ } | Should-BeTrue
		}
	}
}
