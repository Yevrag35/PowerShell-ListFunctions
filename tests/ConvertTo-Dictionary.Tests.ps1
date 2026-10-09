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

		# Unlike an object whose key is $null, a $null input object writes no error.
		It 'skips a $null input object without an error when the input is <Label>' -Tag 'Bug06' -ForEach @(
			@{ Label = 'piped'; Piped = $true }
			@{ Label = 'passed to -InputObject'; Piped = $false }
		) {
			$items = $null, [pscustomobject]@{ K = 'a' }
			if ($Piped) {
				$dict = $items | ConvertTo-Dictionary -KeyPropertyName K -ErrorVariable err
			}
			else {
				$dict = ConvertTo-Dictionary -InputObject $items -KeyPropertyName K -ErrorVariable err
			}
			$err.Count | Should-Be 0
			$dict.Count | Should-Be 1
			$dict['a'].K | Should-Be 'a'
		}
	}

	Context 'Key parameters' {
		# A console would prompt for the missing parameter. The new runspace has no host to prompt with, so PowerShell
		# writes an error that names the parameter instead.
		It 'reports -KeyPropertyName as missing when <Label>' -ForEach @(
			@{ Label = 'no other parameter is given'; Parameters = '' }
			@{ Label = 'only -ValuePropertyName is given'; Parameters = ' -ValuePropertyName Id' }
		) {
			$result = Invoke-InNewRunspace "[pscustomobject]@{ Id = 1 } | ConvertTo-Dictionary$Parameters"
			$result.Errors.Count | Should-Be 1
			$result.Errors[0].FullyQualifiedErrorId | Should-Be 'MissingMandatoryParameter,ListFunctions.Cmdlets.Constructs.ConvertToDictionaryCmdlet'
			$result.Errors[0].Exception.Message | Should-BeLikeString -Expected '*KeyPropertyName*'
		}

		# -KeyPropertyName rejects a script block with an error that PowerShell treats as a failed conversion. PowerShell
		# ignores those while it looks for a parameter that takes an argument by position, so the script block still
		# reaches -KeySelector. Any other error would end the binding at -KeyPropertyName.
		It 'binds a script block passed by position to -KeySelector' {
			$items = [pscustomobject]@{ Id = 1; Name = 'Ann' }, [pscustomobject]@{ Id = 2; Name = 'Bob' }
			$dict = $items | ConvertTo-Dictionary { $_.Id * 10 } Name
			Should-BeCollection -Expected @(10, 20) -Actual ([object[]]$dict.Keys)
			$dict[10] | Should-Be 'Ann'
		}

		It 'rejects a script block passed to -<Parameter> by name' -ForEach @(
			@{ Parameter = 'KeyPropertyName' }
			@{ Parameter = 'Key' }
		) {
			$parameters = @{ $Parameter = { $_.Id } }
			{ [pscustomobject]@{ Id = 1 } | ConvertTo-Dictionary @parameters } | Should-Throw -FullyQualifiedErrorId 'ParameterArgumentTransformationError,ListFunctions.Cmdlets.Constructs.ConvertToDictionaryCmdlet' -ExceptionMessage '*-KeySelector*'
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

		It 'computes each key with -KeySelector when the input is <Label>' -Tag 'Bug12' -ForEach @(
			@{ Label = 'piped'; Piped = $true }
			@{ Label = 'passed to -InputObject'; Piped = $false }
		) {
			if ($Piped) {
				$dict = 1..3 | ConvertTo-Dictionary -KeySelector { $_*10 }
			}
			else {
				$dict = ConvertTo-Dictionary -InputObject 1, 2, 3 -KeySelector { $_*10 }
			}
			Should-BeCollection -Expected @(10, 20, 30) -Actual ([object[]]$dict.Keys)
		}

		It 'gives a value selector passed to -<Parameter> the input object as $_ when an operator follows it' -Tag 'Bug12' -ForEach @(
			@{ Parameter = 'ValueSelector' }
			@{ Parameter = 'ValuePropertyName' }
		) {
			$parameters = @{ KeySelector = { $_ } }
			$parameters[$Parameter] = { $_*2 }
			$dict = 1..3 | ConvertTo-Dictionary @parameters
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

		# The selectors record each object they run for.
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
			Should-BeCollection -Expected @(2, 4, 6) -Actual ([object[]]$dict.Values)
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

		# Get-Content delivers each line wrapped in a PSObject, and a variable keeps the wrapper.
		It 'selects each value with a <Label> wrapped in a PSObject' -ForEach @(
			@{ Label = 'property name'; Value = 'Name' }
			@{ Label = 'script block'; Value = { $_.Name } }
		) {
			$items = [pscustomobject]@{ Id = 1; Name = 'Ann' }, [pscustomobject]@{ Id = 2; Name = 'Bob' }
			$wrapped = [psobject]$Value
			$dict = $items | ConvertTo-Dictionary Id $wrapped
			$dict[1] | Should-Be 'Ann'
			$dict[2] | Should-Be 'Bob'
		}

		It 'rejects a -ValuePropertyName that is <Label>' -ForEach @(
			@{ Label = 'a number'; Value = 5 }
			@{ Label = 'an array of names'; Value = @('Name', 'Id') }
		) {
			{ [pscustomobject]@{ Id = 1; Name = 'Ann' } | ConvertTo-Dictionary Id $Value } | Should-Throw -FullyQualifiedErrorId 'ParameterArgumentTransformationError,ListFunctions.Cmdlets.Constructs.ConvertToDictionaryCmdlet' -ExceptionMessage '*string or a script block*'
		}
	}

	Context 'ValuePropertyName with ValueSelector' {
		It 'rejects -ValueSelector with a <Label> in -ValuePropertyName before it reads any input' -ForEach @(
			@{ Label = 'property name'; Value = 'Name' }
			@{ Label = 'script block'; Value = { $_.Name } }
		) {
			$items = [pscustomobject]@{ Id = 1; Name = 'Ann' }, [pscustomobject]@{ Id = 2; Name = 'Bob' }
			$read = [System.Collections.Generic.List[object]]::new()
			{ $items | ForEach-Object { $read.Add($_); $_ } | ConvertTo-Dictionary Id $Value -ValueSelector { $_.Id } } | Should-Throw -FullyQualifiedErrorId 'System.ArgumentException,ListFunctions.Cmdlets.Constructs.ConvertToDictionaryCmdlet' -ExceptionMessage '*-ValueSelector*'
			$read.Count | Should-Be 0
		}

		# A function that passes its own parameters on can pass $null or an empty string for a value property that it
		# wasn't given.
		It 'uses -ValueSelector when -ValuePropertyName is <Label>' -ForEach @(
			@{ Label = '$null'; Value = $null }
			@{ Label = 'an empty string'; Value = '' }
		) {
			$items = [pscustomobject]@{ Id = 1; Name = 'Ann' }, [pscustomobject]@{ Id = 2; Name = 'Bob' }
			$dict = $items | ConvertTo-Dictionary Id $Value -ValueSelector { $_.Id * 10 }
			$dict[1] | Should-Be 10
			$dict[2] | Should-Be 20
		}
	}

	# The types used to come from the first object's key and value, so the order of the input decided them, and later keys
	# and values were converted to them.
	Context 'Key and value types' {
		It 'creates a Dictionary[object, object] without -KeyType and -ValueType, whatever the first object holds' {
			$items = [pscustomobject]@{ K = 'a'; V = 1 }, [pscustomobject]@{ K = 2; V = 2.5 }
			$dict = $items | ConvertTo-Dictionary K V
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[object, object]]) -Actual $dict
			$dict['a'] | Should-Be 1
			$dict[2] | Should-Be 2.5
		}

		It 'converts each key to -KeyType and each value to -ValueType' {
			$items = [pscustomobject]@{ K = '1'; V = 1 }, [pscustomobject]@{ K = '2'; V = 2 }
			$dict = $items | ConvertTo-Dictionary K V -KeyType ([int]) -ValueType ([string])
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[int, string]]) -Actual $dict
			Should-HaveType -Expected ([string]) -Actual $dict[2]
			$dict[2] | Should-Be '2'
		}

		It 'writes an empty Dictionary[<Name>] when there is no input' -ForEach @(
			@{ Name = 'object, object'; Parameters = @{}; Expected = [System.Collections.Generic.Dictionary[object, object]] }
			@{ Name = 'int, string'; Parameters = @{ KeyType = [int]; ValueType = [string] }; Expected = [System.Collections.Generic.Dictionary[int, string]] }
		) {
			$dict = @() | ConvertTo-Dictionary Id @Parameters
			Should-HaveType -Expected $Expected -Actual $dict
			$dict.Count | Should-Be 0
		}
	}

	# Without -KeyComparer, [object] keys compare the way New-Dictionary's do: two strings ordinally and without regard to
	# case, and any other two keys with their own Equals method.
	Context 'Key comparison' {
		It "treats 'Ann' and 'ann' as the same key" {
			$items = [pscustomobject]@{ Name = 'Ann' }, [pscustomobject]@{ Name = 'ann' }
			$dict = $items | ConvertTo-Dictionary Name -ErrorVariable err -ErrorAction SilentlyContinue
			$dict.Count | Should-Be 1
			$err.Count | Should-Be 1
		}

		It "keeps 1 and '1' apart" {
			$items = [pscustomobject]@{ K = 1 }, [pscustomobject]@{ K = '1' }
			$dict = $items | ConvertTo-Dictionary K
			$dict.Count | Should-Be 2
		}

		It "compares [object] keys with -KeyComparer, so [StringComparer]::Ordinal keeps 'Ann' and 'ann' apart" {
			$items = [pscustomobject]@{ Name = 'Ann' }, [pscustomobject]@{ Name = 'ann' }
			$dict = $items | ConvertTo-Dictionary Name -KeyComparer ([System.StringComparer]::Ordinal)
			$dict.Count | Should-Be 2
		}

		# A StringComparer is an IEqualityComparer[string], not an IEqualityComparer[int], so the dictionary has to wrap it.
		It 'accepts a -KeyComparer for another type than -KeyType' {
			$items = [pscustomobject]@{ Id = 1 }, [pscustomobject]@{ Id = 2 }
			$dict = $items | ConvertTo-Dictionary Id -KeyType ([int]) -KeyComparer ([System.StringComparer]::Ordinal)
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[int, object]]) -Actual $dict
			$dict.Count | Should-Be 2
		}
	}

	Context 'DuplicateKeyBehavior' {
		It 'keeps the first value and writes a warning for a repeated key with Skip' {
			$items = [pscustomobject]@{ K = 'a'; V = 1 }, [pscustomobject]@{ K = 'b'; V = 2 }, [pscustomobject]@{ K = 'a'; V = 3 }
			$dict = $items | ConvertTo-Dictionary K V -DuplicateKeyBehavior Skip -ErrorVariable err -WarningVariable warnings -WarningAction SilentlyContinue
			$err.Count | Should-Be 0
			$warnings.Count | Should-Be 1
			$dict.Count | Should-Be 2
			$dict['a'] | Should-Be 1
		}

		# A key that appears once keeps its value. The first repeat replaces the value with a list that holds both values,
		# and each later repeat adds to that list.
		It 'collects the values of a repeated key in an ObjectList, in input order, with Concatenate' {
			$items = [pscustomobject]@{ K = 'a'; V = 1 }, [pscustomobject]@{ K = 'b'; V = 2 }, [pscustomobject]@{ K = 'a'; V = 3 }, [pscustomobject]@{ K = 'a'; V = 4 }
			$dict = $items | ConvertTo-Dictionary K V -DuplicateKeyBehavior Concatenate
			$dict.Count | Should-Be 2
			$dict['a'].GetType().FullName | Should-Be 'ListFunctions.Modern.ObjectList'
			($dict['a'] -join ', ') | Should-Be '1, 3, 4'
			Should-HaveType -Expected ([int]) -Actual $dict['b']
		}
	}

	# A dictionary can't hold a $null key. The objects whose keys are $null used to be skipped without an error.
	Context 'Null keys' {
		It 'writes an error for each object whose key is $null, and adds the others, when the key comes from <Label>' -ForEach @(
			@{ Label = '-KeyPropertyName'; Parameters = @{ KeyPropertyName = 'K' }; Message = "*no 'K' property*" }
			@{ Label = '-KeySelector'; Parameters = @{ KeySelector = { $_.K } }; Message = '*-KeySelector returned nothing*' }
		) {
			# The third object has no K property.
			$items = [pscustomobject]@{ K = $null; N = 1 }, [pscustomobject]@{ K = 'a'; N = 2 }, [pscustomobject]@{ N = 3 }
			$dict = $items | ConvertTo-Dictionary @Parameters -ErrorVariable err -ErrorAction SilentlyContinue
			$dict.Count | Should-Be 1
			$dict['a'].N | Should-Be 2
			$err.Count | Should-Be 2
			$err[0].FullyQualifiedErrorId | Should-Be 'System.ArgumentNullException,ListFunctions.Cmdlets.Constructs.ConvertToDictionaryCmdlet'
			$err[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidData)
			$err[0].Exception.Message | Should-BeLikeString -Expected $Message
			$err[0].TargetObject.N | Should-Be 1
			$err[1].TargetObject.N | Should-Be 3
		}
	}

	Context 'Null values' {
		# Each expected value is what Add($key, $null) stores in a dictionary with the same value type, in both editions.
		It 'stores <Label> when the value property of an object is $null and -ValueType is [<TypeName>]' -Tag 'Bug19' -ForEach @(
			@{ TypeName = 'string'; Type = [string]; First = 'x'; Label = "''"; Expected = '' }
			@{ TypeName = 'int'; Type = [int]; First = 1; Label = '0'; Expected = 0 }
			@{ TypeName = 'version'; Type = [version]; First = [version]'1.0'; Label = '$null'; Expected = $null }
		) {
			$items = [pscustomobject]@{ K = 'a'; V = $First }, [pscustomobject]@{ K = 'b'; V = $null }
			$dict = $items | ConvertTo-Dictionary K V -ValueType $Type
			$dict.Count | Should-Be 2
			Should-Be -Expected $Expected -Actual $dict['b']
		}

		It 'stores $null when the value property of an object is $null and -ValueType is absent' -Tag 'Bug19' {
			$items = [pscustomobject]@{ K = 'a'; V = $null }, [pscustomobject]@{ K = 'b'; V = 'x' }
			$dict = $items | ConvertTo-Dictionary K V
			$dict.Count | Should-Be 2
			$dict['a'] | Should-BeNull
			$dict['b'] | Should-Be 'x'
		}

		It 'stores the conversion of $null when -ValueSelector outputs nothing' -Tag 'Bug19' {
			$dict = 'a', 'bb' | ConvertTo-Dictionary -KeySelector { $_ } -ValueSelector { if ($_.Length -eq 1) { 1 } } -ValueType ([int])
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
			@{ Label = 'key'; Items = @([pscustomobject]@{ K = 1; V = 'a' }, [pscustomobject]@{ K = 'x'; V = 'b' }); Parameters = @{ KeyType = [int] }; Key = 1; Expected = 'a' }
			@{ Label = 'value'; Items = @([pscustomobject]@{ K = 'a'; V = 1 }, [pscustomobject]@{ K = 'b'; V = 'x' }); Parameters = @{ ValueType = [int] }; Key = 'a'; Expected = 1 }
		) {
			# 'x' has to be converted to [int].
			$dict = $Items | ConvertTo-Dictionary K V @Parameters -ErrorVariable err -ErrorAction SilentlyContinue
			$dict.Count | Should-Be 1
			$dict[$Key] | Should-Be $Expected
			$err.Count | Should-Be 1
			# Should-HaveType would print the whole exception on failure, which takes minutes. A type name prints quickly.
			$err[0].Exception.GetType().FullName | Should-Be 'ListFunctions.Exceptions.LFInvalidCastException'
			$err[0].CategoryInfo.Category | Should-Be ([System.Management.Automation.ErrorCategory]::InvalidType)
			$err[0].TargetObject | Should-Be 'x'
		}

		# Each value is what $d.Add($_, $_) stores in a dictionary with the same value type.
		It 'converts each object to -ValueType when it is its own value' {
			$dict = 1, 2 | ConvertTo-Dictionary -KeySelector { $_ } -ValueType string
			Should-HaveType -Expected ([System.Collections.Generic.Dictionary[object, string]]) -Actual $dict
			$dict[1] | Should-Be '1'
			$dict[2] | Should-Be '2'
		}

		# Get-Item wraps each folder in a PSObject, and -InputObject passes the elements of an array as they are, so the
		# conversion to [System.IO.DirectoryInfo] has to unwrap them.
		It 'stores the objects passed to -InputObject when they are their own values' {
			$null = New-Item -ItemType Directory -Path "$TestDrive/a", "$TestDrive/b"
			$dict = ConvertTo-Dictionary -InputObject (Get-Item -LiteralPath "$TestDrive/a", "$TestDrive/b") -KeyPropertyName Name -ValueType ([System.IO.DirectoryInfo]) -ErrorVariable err -ErrorAction SilentlyContinue
			$err.Count | Should-Be 0
			$dict.Count | Should-Be 2
			$dict['a'].Name | Should-Be 'a'
		}
	}

	# Errors from -KeySelector and -ValueSelector reach PowerShell unchanged, so each result is what ForEach-Object gives
	# for the same script block in both editions. The scripts run in a new runspace, because Pester's try block would catch
	# both kinds of error.
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
