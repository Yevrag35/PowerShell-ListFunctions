BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'Assert-AnyObject' {
	It 'passes each element to -Condition as $args[0]' -Tag 'Bug05' {
		1, 2, 3 | Assert-AnyObject { $args[0] -gt 2 } | Should-BeTrue
		1, 2, 3 | Assert-AnyObject { $args[0] -gt 5 } | Should-BeFalse
	}
}
