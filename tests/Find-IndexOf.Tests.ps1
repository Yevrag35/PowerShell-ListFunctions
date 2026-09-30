BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'Find-IndexOf' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 3 | Find-IndexOf { $args[0] -eq 2 } | Should-Be 1
	}
}
