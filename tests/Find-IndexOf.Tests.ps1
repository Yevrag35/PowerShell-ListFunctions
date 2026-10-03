BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'Find-IndexOf' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 3 | Find-IndexOf { $args[0] -eq 2 } | Should-Be 1
	}

	Context 'Pipeline input' {
		It 'counts a piped <Name> as one element' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null, 3) }
			@{ Name = 'empty array'; Items = @(1, @(), 3) }
			@{ Name = 'array'; Items = @(1, @(2, 2), 3) }
		) {
			$Items | Find-IndexOf { $_ -eq 3 } | Should-Be 2
		}

		It 'passes a piped <Name> to -Condition' -Tag 'Bug06' -ForEach @(
			@{ Name = '$null'; Items = @(1, $null, 3); Condition = { $null -eq $_ } }
			@{ Name = 'array'; Items = @(1, @(2, 2), 3); Condition = { $_ -is [array] } }
		) {
			$Items | Find-IndexOf $Condition | Should-Be 1
		}

		It 'counts the elements of an array passed to -InputObject' -Tag 'Bug06' {
			Find-IndexOf -InputObject @(1, $null, 3) { $_ -eq 3 } | Should-Be 2
		}
	}
}
