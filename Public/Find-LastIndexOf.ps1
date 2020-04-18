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