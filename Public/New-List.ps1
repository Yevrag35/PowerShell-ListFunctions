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
            System.Object[] -- Objects of any type of if constrained with a generic, objects must be of that type.
    
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