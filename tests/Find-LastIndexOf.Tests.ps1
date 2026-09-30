BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'Find-LastIndexOf' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 2, 3 | Find-LastIndexOf { $args[0] -eq 2 } | Should-Be 2
	}
}
