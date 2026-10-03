BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'ConvertTo-Dictionary' {
	Context 'Pipeline input' {
		It 'treats a piped array as one input object' -Tag 'Bug06' {
			$dict = , @('a', 'b') | ConvertTo-Dictionary -KeySelector { $_.Count }
			$dict.Count | Should-Be 1
			Should-BeCollection -Expected @('a', 'b') -Actual ([object[]]$dict[2])
		}

		It 'infers the key type from the first object that isn''t $null when the input is <Label>' -Tag 'Bug06' -ForEach @(
			@{ Label = 'piped'; Piped = $true }
			@{ Label = 'passed to -InputObject'; Piped = $false }
		) {
			$items = $null, [pscustomobject]@{ K = 'a' }
			if ($Piped) {
				$dict = $items | ConvertTo-Dictionary -KeyPropertyName K
			}
			else {
				$dict = ConvertTo-Dictionary -InputObject $items -KeyPropertyName K
			}
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[string, object]]) -Actual $dict
			$dict.Count | Should-Be 1
		}
	}
}
