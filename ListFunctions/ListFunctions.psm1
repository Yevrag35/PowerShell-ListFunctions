Function BuildPredicate() {

    [CmdletBinding()]
    [OutputType([System.Predicate[object]])]
    param
    (
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true)]
        [scriptblock] $ScriptBlock
    )
    Process {

        # Replace all instances of '$_' in the ScriptBlock with '$x'
        $sbString = [regex]::Replace($ScriptBlock, '\$[_](\s|\.)', '$x$1', "IgnoreCase")
        
        # Replace all instance of '$PSItem' in the ScriptBlock with '$_'
        $sbString = [regex]::Replace($sbString, '\$PSItem(\s|\.)', '$_$1', "IgnoreCase")

        if ($sbString -notlike "param (`$x)`n*") {   # If the first line is not the start of a 'param' block then...

            $sbString = "param (`$x)`n" + $sbString  # ...insert one at the beginning of the string.
        }

        # Create a new scriptblock from the StringBuilder and cast it into a System.Predicate[object].
        [System.Predicate[object]][scriptblock]::Create($sbString)
    }
}

Function NewComparer() {

    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string] $GenericType,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [scriptblock] $ComparingScript
    )

    # if ($ComparingScript -match '\$x(\s|\.|\))' -and $ComparingScript -match '\$y(\s|\.|\))') {

    #     $replace1 = [regex]::Replace($ComparingScript, '\$x(\s|\.|\))', '$args[0]$1', "IgnoreCase")
    #     $replace2 = [regex]::Replace($replace1, '\$y(\s|\.|\))', '$args[1]$1', "IgnoreCase")

    #     $ComparingScript = [scriptblock]::Create($replace2)
    # }
    if ($null -ne $ComparingScript -and -not ($ComparingScript -match '\$x(\s|\.|\))' -and $ComparingScript -match '\$y(\s|\.|\))')) {

        return [pscustomobject]@{
            Comparer = $null
            IsFaulted = $true
            ErrorMessage = "Comparing script block does not use '`$x' and '`$y' for comparison."
        }
    }

    if ($GenericType -is [type]) {

        $GenericType = $GenericType.FullName
    }

    $newObjArgs = @{
        TypeName     = "ListFunctions.ScriptBlockComparer[$GenericType]"
    }

    if ($null -ne $ComparingScript) {

        $newObjArgs.Add("Property", @{
            ComparerScript = $ComparingScript
        })
    }

    $comparer = New-Object @newObjArgs

    [pscustomobject]@{
        Comparer     = $comparer
        IsFaulted    = $false
        ErrorMessage = $null
    }
}

Function NewEqualityComparer() {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [scriptblock] $EqualityScript,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [scriptblock] $HashCodeScript
    )

    if ($null -ne $EqualityScript -and -not ($EqualityScript -match '\$x(\s|\.|\))' -and $EqualityScript -match '\$y(\s|\.|\))')) {

        return [pscustomobject]@{
            Comparer = $null
            IsFaulted = $true
            ErrorMessage = $errMsg = 'EqualityScript does not contain valid variables ($x and $y).'
        }
    }

    if ($null -ne $HashCodeScript -and -not ($HashCodeScript -match '\$[_](\.|\s|\))')) {

        $errMsg = "HashCodeScript does not contain the required variables: '`$_'."

        return [pscustomobject]@{
            Comparer = $null
            IsFaulted = $true
            ErrorMessage = $errMsg
        }
    }

    if ($GenericType -is [type]) {

        $GenericType = $GenericType.FullName
    }

    $comparer = New-Object "ListFunctions.ScriptBlockEqualityComparer[$GenericType]"($EqualityScript, $HashCodeScript)

    [pscustomobject]@{
        Comparer = $comparer
        IsFaulted = $false
        ErrorMessage = $null
    }
}

Function Assert-All() {
    <#
        .SYNOPSIS
            Asserts all objects of a collections satisfy a condition.
    
        .DESCRIPTION
            Determines whether all elements of a sequence satisfy a condition.  The condition
            is given in the form of a Filter ScriptBlock which will be converted into a 
            System.Predicate (this means the scriptblock must return 'True/False').

            This function is more useful the more complex the InputObjects become.

            *Updated* - The entire collection of 'InputObject' is no longer required to be fully enumerated.
            The enumeration will stop as soon as the 1st 'False' value is returned by the scriptblock.
    
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

        .NOTES
            The result of the function will be the FIRST boolean value returned by the specified scriptblock.
    #>
    [CmdletBinding()]
    [Alias("Assert-AllObjects", "All-Objects", "All")]
    [OutputType([bool])]
    param
    (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]] $InputObject,

        [Parameter(Mandatory = $true, Position = 0)]
        [scriptblock] $Condition
    )
    Begin {
        $result = $true
        $allAreNull = $true
        $equality = [ListFunctions.ScriptBlockEquality]::Create($Condition, @(Get-Variable))
    }
    Process {
        
        if ($result -and $null -ne $InputObject -and $InputObject.Count -gt 0) {

            $result = $equality.All($InputObject)
            $allAreNull = $false
        }
    }
    End {
        
        return $result -and -not $allAreNull
    }
}

Function Assert-Any() {
    <#
        .SYNOPSIS
            Asserts any object of a collection exists or matches a condition.

        .DESCRIPTION
            Determines whether any element of a sequence satisfies a condition.  If no condition
            (scriptblock) is specified, then the functions determines whether the sequence contains
            any elements at all.

            This function is more useful the more complex the InputObjects become.

            *Updated* - The entire collection of 'InputObject' is no longer required to be fully enumerated.  
            The enumeration will stop as soon as the 1st 'True' value is returned by the scriptblock.
        
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

        .NOTES
            The result of the function will be the FIRST boolean value returned by the specified scriptblock.
    #>
    [CmdletBinding()]
    [Alias("Any-Object", "Any")]
    [OutputType([bool])]
    param
    (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]] $InputObject,

        [Parameter(Mandatory = $false, Position = 0)]
        [scriptblock] $Condition
    )
    Begin {
        $result = $false
        $hasCondition = $PSBoundParameters.ContainsKey("Condition")
        $equality = [ListFunctions.ScriptBlockEquality]::Create($Condition, @(Get-Variable))
    }
    Process {

        if (-not $result) {

            if (-not $hasCondition) {

                $result = $InputObject.Count -gt 0
                return
            }

            $result = $equality.Any($InputObject)
        }
    }
    End {

        return $result
    }
}

Function Find-IndexOf() {
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
    Begin {
        $list = New-Object -TypeName "System.Collections.Generic.List[object]"
        $Predicate = BuildPredicate -ScriptBlock $Condition
    }
    Process {
        $list.AddRange($InputObject)
    }
    End {
        if (-not $PSBoundParameters.ContainsKey("Count")) {
            $list.FindIndex($StartIndex, $Predicate)
        }
        else {
            $list.FindIndex($StartIndex, $Count, $Predicate)
        }
    }
}

Function Find-LastIndexOf() {
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
    [CmdletBinding(DefaultParameterSetName = "None")]
    [Alias("Find-LastIndex", "LastIndexOf")]
    [OutputType([int])]
    param
    (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object[]] $InputObject,

        [Parameter(Mandatory = $true, Position = 0)]
        [scriptblock] $Condition,

        [Parameter(Mandatory = $true, ParameterSetName = "ByStartingIndex")]
        [int] $StartIndex,

        [Parameter(Mandatory = $false, ParameterSetName = "ByStartingIndex")]
        [int] $Count
    )
    Begin {
        $list = New-Object -TypeName "System.Collections.Generic.List[object]"
        $Predicate = BuildPredicate -ScriptBlock $Condition
    }
    Process {
        $list.AddRange($InputObject)
    }
    End {
        if (-not $PSBoundParameters.ContainsKey("StartIndex")) {
            $StartIndex = $list.Count - 1
        }

        if (-not $PSBoundParameters.ContainsKey("Count")) {
            $list.FindLastIndex($StartIndex, $Predicate)
        }
        else {
            $list.FindLastIndex($StartIndex, $Count, $Predicate)
        }
    }
}

Function New-HashSet() {
    <#
        .SYNOPSIS
            Creates a HashSet of unique objects.

        .DESCRIPTION
            Creates a 'System.Collections.Generic.HashSet[T]' in order to store unique objects into.

        .PARAMETER Capacity
            The total number of elements the set can hold without resizing.  Default -- 0.

        .PARAMETER GenericType
            The constraining .NET type that every object added into the set must be.

        .PARAMETER CaseSensitive
            When 'GenericType' is equal to the type of 'System.String', then this parameter specifies the HashSet to use the default 
            (case-sensitive) string comparer.

        .PARAMETER InputObject
            A collection of objects that will initially added into the new set.

        .PARAMETER EqualityScript
            The scriptblock that will check the equality between any 2 objects in the set.  It must return a boolean (True/False) value.

            '$x' must represent the 1st item to be compared.
            '$y' must represent the 2nd item to be compared.

        .PARAMETER HashCodeScript
            The scriptblock that retrieve an object's hash code value.

            A "hash code" is a numeric value that is used to insert and identify an object in a "hash-based" collection.
            The easiest way to provide this through an object's 'GetHashCode()' method.
            Two objects that are equal return hash codes that are equal.  However, the reverse is not true: equal hash codes
            do not imply object equality, because different (unequal) objects can have identical hash codes.

        .INPUTS
            System.Object[] -- The objects that will immediately added to the returned set.

        .OUTPUTS
            System.Collections.Generic.HashSet[T] -- where 'T' is the constrained generic type that all objects must be.

        .EXAMPLE
            # Create a HashSet[string] that ignores case for equality.
            $set = New-HashSet -GenericType [string] -EqualityScript { $x -eq $y } -HashCodeScript { $_.ToLower().GetHashCode() }

        .EXAMPLE
            # Create a HashSet[object] that determines objects with the same 'Name' and 'Id' properties to be equal.
            $set = New-HashSet -EqualityScript { $x.Name -eq $y.Name -and $x.Id -eq $y.Id }

        .NOTES
            The EqualityScript must use either '$x' and '$y' in the
            scriptblock to properly identify the 2 comparing values.

            The HashCodeScript must use either '$_' in the scriptblock to properly identify
            the object whose hash code is retrieved.
    #>
    [CmdletBinding(DefaultParameterSetName = "None")]
    param (
        [Parameter(Mandatory = $false)]
        [ValidateRange(0, [int]::MaxValue)]
        [int] $Capacity = 0,

        [Parameter(Mandatory = $false, Position = 0)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [ValidateNotNull()]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]] $InputObject,

        [Parameter(Mandatory = $true, ParameterSetName = "WithCustomEqualityComparer")]
        [scriptblock] $EqualityScript,

        [Parameter(Mandatory = $false, ParameterSetName = "WithCustomEqualityComparer")]
        [scriptblock] $HashCodeScript
    )
    DynamicParam {

        $rtDict = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary

        if (($GenericType -is [type] -and $GenericType.Name -eq "String") -or ($GenericType -is [string] -and $GenericType -in @('[string]', '[System.String]'))) {

            $pName = 'CaseSensitive'
            $attCol = New-Object 'System.Collections.ObjectModel.Collection[System.Attribute]'
            $pAtt = New-Object System.Management.Automation.ParameterAttribute -Property @{
                Mandatory = $true
                ParameterSetName = "WithStringSet"
            };
            $attCol.Add($pAtt)
            $rtParam = New-Object System.Management.Automation.RuntimeDefinedParameter($pName, $([switch]), $attCol)
            
            $rtDict.Add($pName, $rtParam)
        }

        $rtDict
    }
    Begin {

        if ($GenericType -is [type]) {
            $private:type = $GenericType
            $GenericType = $GenericType.FullName
        }

        if ($GenericType -in @('[string]', 'System.String', '[System.String]')) {

            if (-not $PSBoundParameters.ContainsKey("CaseSensitive") -or -not $PSBoundParameters["CaseSensitive"].ToBool()) {

                $IsCaseInsensitive = $true
            }
        }

        if ($PSCmdlet.ParameterSetName -like "*CustomEquality*") {

            $result = NewEqualityComparer -GenericType $GenericType -EqualityScript $EqualityScript -HashCodeScript $HashCodeScript

            if ($result.IsFaulted) {
                Write-Error -Message $result.ErrorMessage -Category SyntaxError -ErrorId $([System.ArgumentException]).FullName
            }

            $comparer = $result.Comparer
            $set = New-Object -TypeName "System.Collections.Generic.HashSet[$GenericType]"($Capacity, $comparer)
        }
        else {

            if ($IsCaseInsensitive) {

                $set = New-Object -TypeName "System.Collections.Generic.HashSet[$GenericType]"($Capacity, [System.StringComparer]::CurrentCultureIgnoreCase)
            }
            else {

                $set = New-Object -TypeName "System.Collections.Generic.HashSet[$GenericType]"($Capacity)
            }
        }

        if ($null -eq $type) {
            $private:type = $set.GetType().GenericTypeArguments | Select-Object -First 1
        }

        Write-Verbose "HashSet - GenericType $($private:type.FullName)"
        $private:type = $private:type.MakeArrayType()
    }
    Process {

        if ($PSBoundParameters.ContainsKey("InputObject") -and $null -ne $InputObject) {

            $set.UnionWith(($InputObject -as $private:type))
        }
    }
    End {

        , $set
    }
}

Function New-List() {
    <#
        .SYNOPSIS
            Creates a strongly-typed list of objects.
    
        .DESCRIPTION
            Generates a strongly-typed list of objects that can be accessed by index.

            'System.Collections.Generic.List[T]'

            This functions creates the list with the specified constrained type and the specified capacity.  Objects can be immediately
            added to the list by explicit calling the "InputObject" parameter or by passing the objects through the pipeline.
    
        .PARAMETER Capacity
            The total number of elements the list can hold without resizing.  Default -- 0.

        .PARAMETER GenericType
            The constraining type that every object added into the list must be.

        .PARAMETER InputObject
            A collection of objects that will initially added into the new list.
    
        .INPUTS
            System.Object[] -- Objects of any type. But if constrained with a generic, objects must be of that type.
    
        .OUTPUTS
            System.Collections.Generic.List[T] -- where 'T' is the constrained object type.
    
        .EXAMPLE
            $list = New-List 780 -Type [string]

        .EXAMPLE
            $list = ,@('hi', 'hello', 'goodbye') | New-List 3 -GenericType [string]
    #>
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $false)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $false, Position = 0)]
        [Alias("c", "cap")]
        [ValidateRange(0, [int]::MaxValue)]
        [int] $Capacity = 0,

        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [object[]] $InputObject
    )
    Begin {

        if ($GenericType -is [type]) {
            $private:type = $GenericType
            $GenericType = $GenericType.FullName
        }

        $private:list = New-Object "System.Collections.Generic.List[$GenericType]"($Capacity)
        Write-Verbose "List - Created with 'Capacity': $($private:list.Capacity)"

        if ($null -eq $type) {
            $private:type = $private:list.GetType().GenericTypeArguments | Select-Object -First 1
        }
        Write-Verbose "List - GenericType: $($private:type.FullName)"
        $private:type = $private:type.MakeArrayType()
    }
    Process {

        if ($PSBoundParameters.ContainsKey("InputObject")) {

            $private:list.AddRange(($InputObject -as $private:type))
        }

    }
    End {

        , $private:list
    }
}

Function New-SortedSet() {
    <#
        .SYNOPSIS
            Creates a Sorted set of unique objects.

        .DESCRIPTION
            Creates a new instance of 'System.Collections.Generic.SortedSet[T]' with the default or custom comparer.

        .PARAMETER GenericType
            The constraining .NET type that every object added into the set must be.

        .PARAMETER InputObject


        .PARAMETER ComparingScript
            A custom script that provides 'CompareTo' functionality for the SortedSet to use. The comparison of two objects returns an
            [int] value indicating whether one is 'less than', 'equal to', or 'greater than' the other.  The table below describes 
            the meaning of the returned [int] value.

            FYI, any object that implements 'System.IComparable' or 'System.IComparable[T]' will always have a method called 'CompareTo' that
            can be used to generate/return the logic below. Examples of .NET types that do this include [int], [string], [guid], [datetime], etc.

            '$x' must represent the 1st item to be compared.
            '$y' must represent the 2nd item to be compared.

                Value           |   Meaning
            ------------------- | --------------------
            Less than zero      | X is less than Y.
            Zero                | X equals Y.
            Greater than zero   | X is greater than Y.

        .INPUTS
            System.Object[] -- The objects that will immediately added to the returned set.

        .OUTPUTS
            System.Collections.Generic.SortedSet[T] -- where 'T' is the constrained generic type that all objects must be.

        .EXAMPLE
            Sort PSCustomObjects by a shared 'ID' property.

            $obj1 = [pscustomobject]@{ Id = 2; Name = "John" }
            $obj2 = [pscustomobject]@{ Id = 1; Name = "Jane" }

            $set = New-SortedSet -ComparingScript { $x.Id.CompareTo($y.Id) } -InputObject $obj1, $obj2

        .EXAMPLE
            Sort strings with case-sensitive reverse logic (i.e. - Descending order)

            $obj1 = "Jane"
            $obj2 = "John"

            $set = $obj1, $obj2 | New-SortedSet -GenericType [string] -ComparingScript { $x.CompareTo($y) * -1 }
    #>
    [CmdletBinding(DefaultParameterSetName = "None")]
    param (
        [Parameter(Mandatory = $false, Position = 0)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]] $InputObject,

        [Parameter(Mandatory=$true, ParameterSetName = "WithCustomComparer")]
        [Alias("ScriptBlock")]
        [AllowNull()]
        [scriptblock] $ComparingScript
    )
    DynamicParam {

        $rtDict = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary

        if (($GenericType -is [type] -and $GenericType.Name -eq "String") -or ($GenericType -is [string] -and $GenericType -in @('[string]', '[System.String]'))) {

            $pName = 'CaseSensitive'
            $attCol = New-Object 'System.Collections.ObjectModel.Collection[System.Attribute]'
            $pAtt = New-Object System.Management.Automation.ParameterAttribute -Property @{
                Mandatory = $true
                ParameterSetName = "WithStringSet"
            };
            $attCol.Add($pAtt)
            $rtParam = New-Object System.Management.Automation.RuntimeDefinedParameter($pName, $([switch]), $attCol)
            
            $rtDict.Add($pName, $rtParam)
        }

        $rtDict
    }
    Begin {

        if ($GenericType -is [type]) {

            $private:type = $GenericType
            $GenericType = $GenericType.FullName
        }
        elseif ($null -eq $GenericType -or ($GenericType -is [string] -and [string]::IsNullOrWhiteSpace($GenericType))) {


        }

        if ($GenericType -in @('[string]', 'System.String', '[System.String]')) {

            if (-not $PSBoundParameters.ContainsKey("CaseSensitive") -or -not $PSBoundParameters["CaseSensitive"].ToBool()) {

                $IsCaseInsensitive = $true
            }
        }

        if ($PSBoundParameters.ContainsKey("ComparingScript")) {
            
            $comparer = New-Object -TypeName "ListFunctions.ScriptBlockComparer[$GenericType]"($ComparingScript)
        }
        elseif ($IsCaseInsensitive) {

            $comparer = [System.StringComparer]::CurrentCultureIgnoreCase
        }

        if ($null -ne $comparer) {

            $sortedSet = New-Object "System.Collections.Generic.SortedSet[$GenericType]"($comparer)
        }
        else {

            $sortedSet = New-Object "System.Collections.Generic.SortedSet[$GenericType]"
        }
        

        if ($null -eq $private:type) {
            
            $private:type = $sortedSet.GetType().GenericTypeArguments | Select-Object -First 1
        }

        Write-Verbose "HashSet - GenericType $($private:type.FullName)"
        $private:type = $private:type.MakeArrayType()
    }
    Process {

        if ($PSBoundParameters.ContainsKey("InputObject")) {
            
            $sortedSet.UnionWith(($InputObject -as $private:type))
        }
    }
    End {

        , $sortedSet
    }
}

Function Remove-All() {
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
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [ref] $InputObject,

        [Parameter(Mandatory = $true, Position = 0)]
        [scriptblock] $Condition
    )
    Process {
        $Predicate = BuildPredicate $Condition
        if ($InputObject.Value.GetType().IsArray) {
            $elementType = $InputObject.Value.GetType().GetElementType()
            $list = New-Object -TypeName "System.Collections.Generic.List[object]" -ArgumentList $InputObject.Value.Length

            $list.AddRange(@($InputObject.Value))
            [void] $list.RemoveAll($Predicate)

            $InputObject.Value = New-Object -TypeName "$($elementType.FullName)[]" $list.Count
            for ($i = 0; $i -lt $list.Count; $i++) {
                $InputObject.Value[$i] = $list[$i]
            }
        }
        elseif ($InputObject.Value -is [System.Collections.ICollection]) {
            $list = New-Object -TypeName "System.Collections.Generic.List[object]" -ArgumentList $InputObject.Value.Count
            $list.AddRange(@($InputObject.Value.GetEnumerator()))
            [void] $list.RemoveAll($Predicate)

            $newCol = [System.Activator]::CreateInstance($InputObject.Value.GetType())
            foreach ($item in $list) {
                if ($item -is [System.Collections.DictionaryEntry]) {
                    [void] $newCol.Add($item.Key, $item.Value)
                }
                else {
                    [void] $newCol.Add($item)
                }
            }
            $InputObject.Value = $newCol
        }
    }
}

Function Remove-At() {
    <#
        .SYNOPSIS
            Removes item(s) of a collection at the specified indices.
    
        .DESCRIPTION
            Removes the elements at the specified indexes of the supplied collection of objects.

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
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object[]] $InputObject,

        [Parameter(Mandatory = $true, Position = 0)]
        [int[]] $Index,

        [Parameter(Mandatory = $false)]
        [int] $Count
    )
    Begin {
        if ($PSBoundParameters.ContainsKey("Count") -and $Index.Length -gt 1) {
            throw "When using 'Count', only 1 index may be specified at a time."
        }
        $list = New-Object -TypeName "System.Collections.Generic.List[object]"
    }
    Process {
        $list.AddRange($InputObject)
    }
    End {
        if (-not $PSBoundParameters.ContainsKey("Count")) {
            $itemsToRemove = foreach ($remIndex in $Index) {
                $list[$remIndex]
            }
            foreach ($item in $itemsToRemove) {
                [void] $list.Remove($item)
            }
        }
        else {
            $list.RemoveRange($Index[0], $Count)
        }
        $list
    }
}

