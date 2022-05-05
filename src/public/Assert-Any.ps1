Function Assert-Any() {
    <#
        .SYNOPSIS
            Asserts any object of a collection exists or matches a condition.

        .DESCRIPTION
            Determines whether any element of a sequence satisfies a condition.  If no condition
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