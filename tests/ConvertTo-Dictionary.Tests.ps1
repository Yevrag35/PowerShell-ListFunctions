BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
	. "$PSScriptRoot/Invoke-InNewRunspace.ps1"
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

	# A console would prompt for the missing parameter. The new runspace has no host to prompt with, so PowerShell writes
	# an error that names the parameter instead.
	Context 'Key parameters' {
		It 'reports -KeyPropertyName as missing when <Label>' -ForEach @(
			@{ Label = 'no other parameter is given'; Parameters = '' }
			@{ Label = 'only -ValuePropertyName is given'; Parameters = ' -ValuePropertyName Id' }
		) {
			$result = Invoke-InNewRunspace "[pscustomobject]@{ Id = 1 } | ConvertTo-Dictionary$Parameters"
			$result.Errors.Count | Should-Be 1
			$result.Errors[0].FullyQualifiedErrorId | Should-Be 'MissingMandatoryParameter,ListFunctions.Cmdlets.Constructs.ConvertToDictionaryCmdlet'
			$result.Errors[0].Exception.Message | Should-BeLikeString -Expected '*KeyPropertyName*'
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

	Context 'Null values' {
		# Each expected value is what Add($key, $null) stores in a dictionary with the same value type, in both editions.
		It 'stores <Label> when the value property of a later object is $null and the values are [<TypeName>]' -Tag 'Bug19' -ForEach @(
			@{ TypeName = 'string'; First = 'x'; Label = "''"; Expected = '' }
			@{ TypeName = 'int'; First = 1; Label = '0'; Expected = 0 }
			@{ TypeName = 'version'; First = [version]'1.0'; Label = '$null'; Expected = $null }
		) {
			$items = [pscustomobject]@{ K = 'a'; V = $First }, [pscustomobject]@{ K = 'b'; V = $null }
			$dict = $items | ConvertTo-Dictionary K V
			$dict.Count | Should-Be 2
			Should-Be -Expected $Expected -Actual $dict['b']
		}

		It 'stores $null when the value property of the first object is $null' -Tag 'Bug19' {
			$items = [pscustomobject]@{ K = 'a'; V = $null }, [pscustomobject]@{ K = 'b'; V = 'x' }
			$dict = $items | ConvertTo-Dictionary K V
			# The first value is $null, so the value type is [object].
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[string, object]]) -Actual $dict
			$dict.Count | Should-Be 2
			$dict['a'] | Should-BeNull
			$dict['b'] | Should-Be 'x'
		}

		It 'stores the conversion of $null when -ValueSelector outputs nothing' -Tag 'Bug19' {
			$dict = 'a', 'bb' | ConvertTo-Dictionary -KeySelector { $_ } -ValueSelector { if ($_.Length -eq 1) { 1 } }
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[string, int]]) -Actual $dict
			$dict.Count | Should-Be 2
			$dict['bb'] | Should-Be 0
		}

		It 'uses each object as its own value when there is no value selector' -Tag 'Bug19' {
			$items = [pscustomobject]@{ K = 'a' }, [pscustomobject]@{ K = 'b' }
			$dict = $items | ConvertTo-Dictionary K
			$dict['b'].K | Should-Be 'b'
		}
	}

	Context 'Conversion' {
		It "writes the error that New-List writes for a <Label> that can't be converted, and skips its object" -ForEach @(
			@{ Label = 'key'; Items = @([pscustomobject]@{ K = 1; V = 'a' }, [pscustomobject]@{ K = 'x'; V = 'b' }); Key = 1; Expected = 'a' }
			@{ Label = 'value'; Items = @([pscustomobject]@{ K = 'a'; V = 1 }, [pscustomobject]@{ K = 'b'; V = 'x' }); Key = 'a'; Expected = 1 }
		) {
			# The first object sets the key and value types, so 'x' has to be converted to [int].
			$dict = $Items | ConvertTo-Dictionary K V -ErrorVariable err -ErrorAction SilentlyContinue
			$dict.Count | Should-Be 1
			$dict[$Key] | Should-Be $Expected
			$err.Count | Should-Be 1
			# Should-HaveType would print the whole exception on failure, which takes minutes. A type name prints quickly.
			$err[0].Exception.GetType().FullName | Should-Be 'ListFunctions.Exceptions.LFInvalidCastException'
			$err[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidType)
			$err[0].TargetObject | Should-Be 'x'
		}
	}

	# Errors from -KeySelector and -ValueSelector reach PowerShell unchanged, so each result is what ForEach-Object gives
	# for the same script block in both editions. The first object's selectors run while ConvertTo-Dictionary infers the
	# key and value types, and the other objects' selectors run while it adds them, so the tests cover both. The scripts
	# run in a new runspace, because Pester's try block would catch both kinds of error.
	Context 'Errors in selectors' {
		It 'ends the script when -<Parameter> throws for the <Label> object' -ForEach @(
			@{ Parameter = 'KeySelector'; Label = 'first'; Failing = 'a' }
			@{ Parameter = 'KeySelector'; Label = 'second'; Failing = 'b' }
			@{ Parameter = 'ValueSelector'; Label = 'first'; Failing = 'a' }
			@{ Parameter = 'ValueSelector'; Label = 'second'; Failing = 'b' }
		) {
			$selector = "{ if (`$_ -eq '$Failing') { throw 'boom' } else { `$_ } }"
			if ($Parameter -eq 'KeySelector') {
				$command = "'a', 'b' | ConvertTo-Dictionary -KeySelector $selector"
			}
			else {
				$command = "'a', 'b' | ConvertTo-Dictionary -KeySelector { `$_ } -ValueSelector $selector"
			}
			$result = Invoke-InNewRunspace "$command; 'still running'"
			$result.StoppedBy.FullyQualifiedErrorId | Should-Be 'boom'
		}

		It 'ends only the statement when a method call in -KeySelector fails under Stop' {
			# The selector runs in its own scope, so the preference it sets doesn't reach the script.
			$result = Invoke-InNewRunspace "'a' | ConvertTo-Dictionary -KeySelector { `$ErrorActionPreference = 'Stop'; if (`$_) { `$null.Foo() } }; 'still running'"
			$result.StoppedBy | Should-BeNull
			Should-BeCollection -Expected @('still running') -Actual $result.Output
			$result.Errors.Count | Should-Be 1
			# PowerShell keeps the error ID and category of the failed call, and adds the command.
			$result.Errors[0].FullyQualifiedErrorId | Should-Be 'InvokeMethodOnNull,ListFunctions.Cmdlets.Constructs.ConvertToDictionaryCmdlet'
			$result.Errors[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidOperation)
		}

		It 'leaves the enclosing loop when -KeySelector runs break' {
			$result = Invoke-InNewRunspace "foreach (`$i in 1..2) { `$i; 'a' | ConvertTo-Dictionary -KeySelector { if (`$_) { break } } }; 'after the loop'"
			$result.Errors.Count | Should-Be 0
			Should-BeCollection -Expected @(1, 'after the loop') -Actual $result.Output
		}
	}
}
