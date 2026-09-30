BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'Assert-AllObject' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 3 | Assert-AllObject { $args[0] -gt 0 } | Should-BeTrue
		1, 2, 3 | Assert-AllObject { $args[0] -gt 1 } | Should-BeFalse
	}
}
