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

	Context 'KeySelector and ValueSelector' {
		It 'gives -KeySelector the input object as <Name> when an operator follows it' -Tag 'Bug12' -ForEach @(
			@{ Name = '$_'; Selector = { $_*10 } }
			@{ Name = '$PSItem'; Selector = { $PSItem*10 } }
			@{ Name = '$this'; Selector = { $this*10 } }
			@{ Name = '$args[0]'; Selector = { $args[0]*10 } }
		) {
			$dict = 1..3 | ConvertTo-Dictionary -KeySelector $Selector
			Should-BeCollection -Expected @(10, 20, 30) -Actual ([object[]]$dict.Keys)
		}

		It 'infers the key type from -KeySelector when the input is <Label>' -Tag 'Bug12' -ForEach @(
			@{ Label = 'piped'; Piped = $true }
			@{ Label = 'passed to -InputObject'; Piped = $false }
		) {
			if ($Piped) {
				$dict = 1..3 | ConvertTo-Dictionary -KeySelector { $_*10 }
			}
			else {
				$dict = ConvertTo-Dictionary -InputObject 1, 2, 3 -KeySelector { $_*10 }
			}
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[int, int]]) -Actual $dict
			Should-BeCollection -Expected @(10, 20, 30) -Actual ([object[]]$dict.Keys)
		}

		It 'gives a value selector passed to -<Parameter> the input object as $_ when an operator follows it' -Tag 'Bug12' -ForEach @(
			@{ Parameter = 'ValueSelector' }
			@{ Parameter = 'ValuePropertyName' }
		) {
			$parameters = @{ KeySelector = { $_ } }
			$parameters[$Parameter] = { $_*2 }
			$dict = 1..3 | ConvertTo-Dictionary @parameters
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[int, int]]) -Actual $dict
			Should-BeCollection -Expected @(2, 4, 6) -Actual ([object[]]$dict.Values)
		}

		It 'expands $_ in a double-quoted string' -Tag 'Bug12' {
			$dict = 'x', 'y' | ConvertTo-Dictionary -KeySelector { "$_" }
			Should-BeCollection -Expected @('x', 'y') -Actual ([object[]]$dict.Keys)
		}

		It 'leaves $_ in a nested script block to the command that runs it' -Tag 'Bug12' {
			$people = [pscustomobject]@{ Name = 'Ann'; Tags = 'a', 'b' }, [pscustomobject]@{ Name = 'Bob'; Tags = 'c' }
			$dict = $people | ConvertTo-Dictionary Name -ValueSelector { @($_.Tags | Where-Object { $_ -ne 'b' }).Count }
			$dict['Ann'] | Should-Be 1
			$dict['Bob'] | Should-Be 1
		}

		# The selectors record each object they run for. The first object's outputs also give the key and value types.
		It 'runs -KeySelector and -ValueSelector once for each input object when the input is <Label>' -ForEach @(
			@{ Label = 'piped'; Piped = $true }
			@{ Label = 'passed to -InputObject'; Piped = $false }
		) {
			$keyRuns = [System.Collections.Generic.List[object]]::new()
			$valueRuns = [System.Collections.Generic.List[object]]::new()
			$parameters = @{
				KeySelector = { $keyRuns.Add($_); $_ }
				ValueSelector = { $valueRuns.Add($_); $_ * 2 }
			}
			if ($Piped) {
				$dict = 1..3 | ConvertTo-Dictionary @parameters
			}
			else {
				$dict = ConvertTo-Dictionary -InputObject 1, 2, 3 @parameters
			}
			Should-BeCollection -Expected @(1, 2, 3) -Actual ([object[]]$keyRuns)
			Should-BeCollection -Expected @(1, 2, 3) -Actual ([object[]]$valueRuns)
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[int, int]]) -Actual $dict
			Should-BeCollection -Expected @(2, 4, 6) -Actual ([object[]]$dict.Values)
		}

		# The first object's key is $null, so the key type is [object], and the object is skipped.
		It 'runs -KeySelector once for each input object when the first key is $null' {
			$keyRuns = [System.Collections.Generic.List[object]]::new()
			$dict = 0, 1, 2 | ConvertTo-Dictionary -KeySelector { $keyRuns.Add($_); if ($_) { $_ } }
			Should-BeCollection -Expected @(0, 1, 2) -Actual ([object[]]$keyRuns)
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[object, int]]) -Actual $dict
			Should-BeCollection -Expected @(1, 2) -Actual ([object[]]$dict.Keys)
		}
	}

	Context 'KeyPropertyName and ValuePropertyName' {
		# In a single-quoted string, PowerShell treats the typographic single quotes, such as U+2019, as apostrophes.
		It 'selects a key property and a value property whose names contain <Label>' -Tag 'Bug22' -ForEach @(
			@{ Label = 'an apostrophe'; Quote = "'" }
			@{ Label = 'a right single quotation mark'; Quote = [string][char]0x2019 }
		) {
			$keyName = "key${Quote}s"
			$valueName = "value${Quote}s"
			$item = [pscustomobject]@{ $keyName = 'k'; $valueName = 'v' }
			$dict = $item | ConvertTo-Dictionary -KeyPropertyName $keyName -ValuePropertyName $valueName
			$dict.Count | Should-Be 1
			$dict['k'] | Should-Be 'v'
		}
	}
}
