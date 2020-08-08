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