Function BuildPredicate()
{
    [CmdletBinding()]
    [OutputType([System.Predicate[object]])]
    param
    (
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [scriptblock] $ScriptBlock
    )
    Begin
    {
        $rebuild = New-Object -TypeName 'System.Collections.Generic.List[string]' -ArgumentList 2
    }
    Process
    {
        # Replace all instances of '$_' and '$PSItem' in the ScriptBlock with '$x'
        $sbString = $ScriptBlock.ToString().Replace('$_', '$x')
        $matchCol = [regex]::Matches($sbString, '\$PSItem', "IgnoreCase")
        $matchCol | Select-Object Value -Unique | ForEach-Object {
            $sbString = $sbString.Replace($PSItem.Value, [string]'$_')
        }

        # Split the ScriptBlock by new lines and add them to $rebuild
        $rebuild.AddRange(($sbString -split "`n"))

        if ($rebuild[0] -cnotmatch '^\s*param') # If the first line is not the start of a 'param' block then...
        {
            $rebuild.Insert(0, 'param ($x)')    # ...insert one at the beginning of the list.
        }

        # Cast all joined strings from the list into a System.Predicate[object]
        [System.Predicate[object]][scriptblock]::Create(($rebuild -join "`n"))
    }
}

Function Assert-All()
{
    <#
        .SYNOPSIS
            Asserts all objects of a collections satisfy a condition.
    
        .DESCRIPTION
            Determines whether all elements of a sequence satisfy a condition.  The condition
            is given in the form of a Filter ScriptBlock which will be converted into a 
            System.Predicate (this means the scriptblock must return 'True/False').

            This function is more useful the more complex the InputObjects become.
    
        .PARAMETER InputObject
            The collection(s) that contains the elements to apply the condition to.  If an empty collection
            or null is passed, then the function will return 'False'.

        .PARAMETER Condition
            A ScriptBlock that each is run against each InputObject to satisfy the condition.
            The condition must be a predicate, meaning that a 'True/False' value is returned.
    
        .INPUTS
            System.Object - any .NET object or array of objects.
    
        .OUTPUTS
            System.Boolean
    
        .EXAMPLE
            @(1, 2, 3) | All { $_ -eq 1 }  # returns 'False'

        .EXAMPLE
            @(1, 2, 3) | All { $_ -is [int] } # returns 'True'

        .EXAMPLE
            @(
                [pscustomobject]@{ Greeting = @{ 1 = "Hi" }},
                [pscustomobject]@{ Greeting = @{ 2 = "Hey"}}
            ) | All { $_.Greeting.Count -gt 0 }   # returns 'True'
    #>
    [CmdletBinding()]
    [Alias("Assert-AllObjects", "All-Objects", "All")]
    [OutputType([bool])]
    param
    (
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]] $InputObject,

        [Parameter(Mandatory=$true, Position=0)]
        [scriptblock] $Condition
    )
    Begin
    {
        $list = New-Object -TypeName "System.Collections.Generic.List[object]"
    }
    Process
    {
        if ($null -ne $InputObject -and $InputObject.Length -gt 0)
        {
            $list.AddRange($InputObject)
        }
    }
    End
    {
        if ($list.Count -gt 0)
        {
            $list.Where($Condition).Count -eq $list.Count
        }
        else
        {
            $false
        }
    }
}

Function Assert-Any()
{
    <#
        .SYNOPSIS
            Asserts any object of a collection exists or matches a condition.

        .DESCRIPTION
            Determines whether any element of a sequence satisifies a condition.  If no condition
            (scriptblock) is specified, then the functions determines whether the sequence contains
            any elements at all.

            This function is more useful the more complex the InputObjects become.
        
        .PARAMETER InputObject
            The collection(s) whose elements to apply the condition to.  If the incoming collection(s) of objects
            are empty or null, the function returns 'False'.

        .PARAMETER Condition
            OPTIONAL.  A ScriptBlock that each is run against each InputObject to satisfy the condition.
                       The condition must be a predicate, meaning that a 'True/False' value is returned.
        
        .INPUTS
            System.Object - any .NET object or array of objects.

        .OUTPUTS
            System.Boolean
        
        .EXAMPLE
            @(1, 2, 3) | Any { $_ -eq 1 }  # returns 'True'

        .EXAMPLE
            @(1, 2, 3) | Any   # returns 'True' - (identical to '@(1, 2, 3).Count -gt 0')

        .EXAMPLE
            @(
                [pscustomobject]@{ Greeting = @{ 1 = "Hi" }},
                [pscustomobject]@{ Greeting = @{ 2 = "Hey"}}
            ) | Any { $_.Greeting.ContainsKey(2) -and $_.Greeting[2] -eq "Hey" }   # returns 'True'
    #>
    [CmdletBinding()]
    [Alias("Any-Object", "Any")]
    [OutputType([bool])]
    param
    (
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]] $InputObject,

        [Parameter(Mandatory=$false, Position=0)]
        [scriptblock] $Condition
    )
    Begin
    {
        $list = New-Object -TypeName "System.Collections.Generic.List[object]"
    }
    Process
    {
        if ($null -ne $InputObject -and $InputObject.Length -gt 0)
        {
            $list.AddRange($InputObject)
        }
    }
    End
    {
        if ($list.Count -gt 0)
        {
            if ($PSBoundParameters.ContainsKey("Condition"))
            {
                $list.Where($Condition).Count -gt 0
            }
            else
            {
                $true
            }
        }
        else
        {
            $false
        }
    }
}

Function Find-IndexOf()
{
    <#
        .SYNOPSIS
            Finds the index of the first element that matches a condition.
    
        .DESCRIPTION
            Searches for an element that matches the condition defined by the specified conditional
            ScriptBlock, and returns the zero-based index of the first occurrence within the entire 
            collection of objects.

            When a 'StartIndex' value is specified, the function returns the first occurrence that 
            starts from the first element of the specified index.  When combined with 'Count', only
            the specified number of elements are searched.

            If no elements satisfy the condition specified, then function returns -1.

            This function is more useful the more complex the InputObjects become.

            @@!! IMPORTANT - READ NOTES SECTION !!@@
    
        .PARAMETER InputObject
            The collection(s) of objects that are searched through.

        .PARAMETER Condition
            The predicate (scriptblock) that defines the conditions of the element to search for.

        .PARAMETER StartIndex
            The zero-based index where the search starts.

        .PARAMETER Count
            The number of elements in the section to search.
    
        .INPUTS
            System.Object - any .NET object or array of objects.
    
        .OUTPUTS
            System.Int32 - the zero-based index of the last occurrence that matches the condition if found; otherwise, -
    
        .EXAMPLE
            @(1, 2, 3, 1, 4) | Find-IndexOf { $_ -eq 1 }    # returns '0'

        .EXAMPLE
            @(1, 2, 3, 1, 4) | Find-IndexOf { $_ -eq 3 -or $_ -eq 4 } # returns '2'

        .EXAMPLE
            @(
                [pscustomobject]@{ Whatev = @(1, 7) },
                [pscustomobject]@{ Whatev = @(2, 7) },
                [pscustomobject]@{ Whatev = @(3, @{ 7 = "Seven" }) },
                [pscustomobject]@{ Whatev = @(4, @{ 7 = "Seven" }) },
                [pscustomobject]@{ Whatev = @{ 7 = "Seven" }; Another = "Property" },
                [pscustomobject]@{
                    Whatev = @(
                        4,5,6
                    )
                }
            ) | Find-IndexOf { $_.Whatev -is [array] -and $($_.Whatev | Any { $PSItem -is [hashtable] -and $PSItem.ContainsKey(7) } ) }

            # returns '2'
    
        .NOTES
            @@!! IMPORTANT NOTE !!@@ --
                In the conditional ScriptBlock, '$_' should ALWAYS represent the value of each InputObject.
                If using additional nested ScriptBlock inside the condition, DO NOT USE '$_'!  Instead
                use '$PSItem'.  The function replaces all instances of '$_' in order to create the System.Predicate[object].

                Look at Example #3 to see an example of this.
    #>
    [CmdletBinding()]
    [Alias("Find-Index", "IndexOf")]
    [OutputType([int])]
    param
    (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object[]] $InputObject,

        [Parameter(Mandatory = $true, Position = 0)]
        [scriptblock] $Condition,

        [Parameter(Mandatory = $false)]
        [int] $StartIndex,

        [Parameter(Mandatory = $false)]
        [int] $Count
    )
    Begin
    {
        $list = New-Object -TypeName "System.Collections.Generic.List[object]"
        $Predicate = BuildPredicate -ScriptBlock $Condition
    }
    Process
    {
        $list.AddRange($InputObject)
    }
    End
    {
        if (-not $PSBoundParameters.ContainsKey("Count"))
        {
            $list.FindIndex($StartIndex, $Predicate)
        }
        else
        {
            $list.FindIndex($StartIndex, $Count, $Predicate)
        }
    }
}

Function Find-LastIndexOf()
{
    <#
        .SYNOPSIS
            Finds the index of the last element that matches a condition.
    
        .DESCRIPTION
            Searches for an element that matches the condition defined by the specified conditional
            ScriptBlock, and returns the zero-based index of the last occurrence within the entire 
            collection of objects.

            When a 'StartIndex' value is specified, the function returns the last occurrence that 
            extends from the first element to the specified index.  When combined with 'Count', only
            the specified number of elements are searched.

            If no elements satisfy the condition specified, then function returns -1.

            This function is more useful the more complex the InputObjects become.

            @@!! IMPORTANT - READ NOTES SECTION !!@@
    
        .PARAMETER InputObject
            The collection(s) of objects that are searched through.

        .PARAMETER Condition
            The predicate (scriptblock) that defines the conditions of the element to search for.

        .PARAMETER StartIndex
            The zero-based index of the backward search.

        .PARAMETER Count
            The number of elements in the section to search.
    
        .INPUTS
            System.Object - any .NET object or array of objects.
    
        .OUTPUTS
            System.Int32 - the zero-based index of the last occurrence that matches the condition if found; otherwise, -1.
    
        .EXAMPLE
            @(1, 2, 3, 1, 4) | Find-LastIndexOf { $_ -eq 1 }    # returns '3'

        .EXAMPLE
            @(1, 2, 3, 1, 4) | Find-LastIndexOf { $_ -eq 1 -or $_ -eq 4 } # returns '4'

        .EXAMPLE
            @(
                [pscustomobject]@{ Whatev = @(1, 7) },
                [pscustomobject]@{ Whatev = @(2, 7) },
                [pscustomobject]@{ Whatev = @(3, @{ 7 = "Seven" }) },
                [pscustomobject]@{ Whatev = @(4, @{ 7 = "Seven" }) },
                [pscustomobject]@{ Whatev = @{ 7 = "Seven" }; Another = "Property" },
                [pscustomobject]@{
                    Whatev = @(
                        4,5,6
                    )
                }
            ) | Find-LastIndexOf { $_.Whatev -is [array] -and $($_.Whatev | Any { $PSItem -is [hashtable] -and $PSItem.ContainsKey(7) } ) }

            # returns '3'

        .NOTES
            @@!! IMPORTANT NOTE !!@@ --
                In the conditional ScriptBlock, '$_' should ALWAYS represent the value of each InputObject.
                If using additional nested ScriptBlock inside the condition, DO NOT USE '$_'!  Instead
                use '$PSItem'.  The function replaces all instances of '$_' in order to create the System.Predicate[object].

                Look at Example #3 to see an example of this.
    #>
    [CmdletBinding(DefaultParameterSetName="None")]
    [Alias("Find-LastIndex", "LastIndexOf")]
    [OutputType([int])]
    param
    (
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [object[]] $InputObject,

        [Parameter(Mandatory=$true, Position=0)]
        [scriptblock] $Condition,

        [Parameter(Mandatory=$true, ParameterSetName="ByStartingIndex")]
        [int] $StartIndex,

        [Parameter(Mandatory=$false, ParameterSetName="ByStartingIndex")]
        [int] $Count
    )
    Begin
    {
        $list = New-Object -TypeName "System.Collections.Generic.List[object]"
        $Predicate = BuildPredicate -ScriptBlock $Condition
    }
    Process
    {
        $list.AddRange($InputObject)
    }
    End
    {
        if (-not $PSBoundParameters.ContainsKey("StartIndex"))
        {
            $StartIndex = $list.Count - 1
        }

        if (-not $PSBoundParameters.ContainsKey("Count"))
        {
            $list.FindLastIndex($StartIndex, $Predicate)
        }
        else
        {
            $list.FindLastIndex($StartIndex, $Count, $Predicate)
        }
    }
}

Function Remove-All()
{
    <#
        .SYNOPSIS
            Removes objects from collection(s) that satisfy a condition.
    
        .DESCRIPTION
            Removes all elements that match the conditions defined by the specified ScriptBlock.
    
        .PARAMETER InputObject
            The collection(s) of objects whose elements will be removed.

        .PARAMETER Condition
            The predicate (scriptblock) condition that determines whether any one element should be removed
            from the collection.
    
        .INPUTS
            System.Management.Automation.PSReference - [ref].  This must be a reference to an Array, Collection, or List
            (this includes Dictionaries; e.g. - Hashtables).
    
        .OUTPUTS
            None.
    
        .EXAMPLE
            $numbers = @(1, 2, 3)
            [ref]$numbers | Remove-All { $_ -eq 1 }
            # '$numbers' now equals:
            # @(2, 3)

        .EXAMPLE
            $strings = [System.Collections.ArrayList]@('hi', 'bye', 'so long')
            Remove-All -InputObject ([ref]$strings) -Condition { $_.Contains(' ') }
            # '$strings' now equals:
            # @('hi', 'bye')

        .EXAMPLE
            $hash = @{ "hi" = 1; "bye" = 2; "so long" = @{ multi = $true } }
            [ref]$hash | Remove-All { $_.Key -eq "Hi" -or $_.Value -is [hashtable] }
            # '$hash' now equals:
            # @{ "bye" = 2 }
    
        .NOTES
            If 'InputObject' is not a single-dimensional array or does not inherit from 'System.Collections.ICollection',
            then no removal operation will take place.

            @@!! IMPORTANT NOTE !!@@ --
                In the conditional ScriptBlock, '$_' should ALWAYS represent the value of each InputObject.
                If using additional nested ScriptBlock inside the condition, DO NOT USE '$_'!  Instead
                use '$PSItem'.  The function replaces all instances of '$_' in order to create the System.Predicate[object].
    #>
    [CmdletBinding()]
    [Alias("RemoveAll")]
    param
    (
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [ref] $InputObject,

        [Parameter(Mandatory=$true, Position=0)]
        [scriptblock] $Condition
    )
    Process
    {
        $Predicate = BuildPredicate $Condition
        if ($InputObject.Value.GetType().IsArray)
        {
            $elementType = $InputObject.Value.GetType().GetElementType()
            $list = New-Object -TypeName "System.Collections.Generic.List[object]" -ArgumentList $InputObject.Value.Length

            $list.AddRange(@($InputObject.Value))
            [void] $list.RemoveAll($Predicate)

            $InputObject.Value = New-Object -TypeName "$($elementType.FullName)[]" $list.Count
            for ($i = 0; $i -lt $list.Count; $i++)
            {
                $InputObject.Value[$i] = $list[$i]
            }
        }
        elseif ($InputObject.Value -is [System.Collections.ICollection])
        {
            $list = New-Object -TypeName "System.Collections.Generic.List[object]" -ArgumentList $InputObject.Value.Count
            $list.AddRange(@($InputObject.Value.GetEnumerator()))
            [void] $list.RemoveAll($Predicate)

            $newCol = [System.Activator]::CreateInstance($InputObject.Value.GetType())
            foreach ($item in $list)
            {
                if ($item -is [System.Collections.DictionaryEntry])
                {
                    [void] $newCol.Add($item.Key, $item.Value)
                }
                else
                {
                    [void] $newCol.Add($item)
                }
            }
            $InputObject.Value = $newCol
        }
    }
}

Function Remove-At()
{
    <#
        .SYNOPSIS
            Removes item(s) of a collection at the specified indices.
    
        .DESCRIPTION
            Removes the elemetns at the specified indexes of the supplied collection of objects.

            When 'Index' and 'Count' are specified, the number ('Count') of elements from the specified
            index will be removed.  There cannot be more than one (1) index specified when both parameters
            are used.
    
        .PARAMETER InputObject
            The collection(s) of objects whose elements will be removed.

        .PARAMETER Index
            The zero-based index of the element to remove.  When used with 'Count', it is the starting index
            from which elements will be removed.

        .PARAMETER Count
            The number of elements to remove counting up from the starting index.
    
        .INPUTS
            System.Object - any .NET object or array of objects.
    
        .OUTPUTS
            System.Object[] - the resulting array sans the removed elements.
    
        .EXAMPLE
            @(1, 2, 3) | Remove-At 1
            # returns:
            # @(1, 3)
    #>
    [CmdletBinding()]
    [Alias("RemoveAt")]
    param
    (
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [object[]] $InputObject,

        [Parameter(Mandatory=$true, Position=0)]
        [int[]] $Index,

        [Parameter(Mandatory=$false)]
        [int] $Count
    )
    Begin
    {
        if ($PSBoundParameters.ContainsKey("Count") -and $Index.Length -gt 1)
        {
            throw "When using 'Count', only 1 index may be specified at a time."
        }
        $list = New-Object -TypeName "System.Collections.Generic.List[object]"
    }
    Process
    {
        $list.AddRange($InputObject)
    }
    End
    {
        if (-not $PSBoundParameters.ContainsKey("Count"))
        {
            $itemsToRemove = foreach ($remIndex in $Index)
            {
                $list[$remIndex]
            }
            foreach ($item in $itemsToRemove)
            {
                [void] $list.Remove($item)
            }
        }
        else
        {
            $list.RemoveRange($Index[0], $Count)
        }
        $list
    }
}


