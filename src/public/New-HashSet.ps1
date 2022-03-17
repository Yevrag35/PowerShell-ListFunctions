Function New-HashSet() {
    <#
        .SYNOPSIS
            Creates a HashSet of unique objects.

        .DESCRIPTION
            Creates a 'System.Collections.Generic.HashSet[T]' in order to store unique objects into.

        .PARAMETER Capacity
            The total number of elements the set can hold without resizing.  Default -- 0.

        .PARAMETER GenericType
            The constraining type that every object added into the set must be.

        .PARAMETER EqualityScript
            The scriptblock that will check the equality between any 2 objects in the set.  It must return a boolean (True/False) value.

            '$x' -or- '$args[0]' must represent the 1st item to be compared.
            '$y' -or - '$args[1]' must represent the 2nd item to be compared.

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
            The EqualityScript must use either '$x' and '$y' -or- '$args[0]' and '$args[1]' in the
            scriptblock to properly identify the 2 comparing values.

            The HashCodeScript must use either '$_' -or- '$args[0]' in the scriptblock to properly identify
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
        [object[]] $InputObject,

        #[Parameter(Mandatory = $true, ParameterSetName = "WithCustomEqualityComparer")]
        [Parameter(Mandatory=$false)]
        [scriptblock] $EqualityScript,

        #[Parameter(Mandatory = $false, ParameterSetName = "WithCustomEqualityComparer")]
        [Parameter(Mandatory=$false)]
        [scriptblock] $HashCodeScript
    )
    Begin {

        if ($GenericType -is [type]) {
            $private:type = $GenericType
            $GenericType = $GenericType.FullName
        }

        $result = NewEqualityComparer -GenericType $GenericType -EqualityScript $EqualityScript -HashCodeScript $HashCodeScript

        if ($result.IsFaulted) {
            Write-Error -Message $result.ErrorMessage -Category SyntaxError -ErrorId $([System.ArgumentException]).FullName
        }
        $comparer = $result.Comparer

        $set = New-Object -TypeName "System.Collections.Generic.HashSet[$GenericType]"($Capacity, $comparer)

        if ($null -eq $type) {
            $private:type = $set.GetType().GenericTypeArguments | Select-Object -First 1
        }

        Write-Verbose "HashSet - GenericType $($private:type.FullName)"
        $private:type = $private:type.MakeArrayType()
    }
    Process {

        if ($PSBoundParameters.ContainsKey("InputObject")) {

            $set.UnionWith(($InputObject -as $private:type))
        }
    }
    End {

        , $set
    }
}