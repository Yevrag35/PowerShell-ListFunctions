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
        $equality = [ListFunctions.ScriptBlockEquality]::Create($Condition)
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