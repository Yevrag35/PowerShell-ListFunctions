Function Remove-All()
{
    <#
        .SYNOPSIS
            Removes objects from collection(s) that satisfy a condition.
    
        .DESCRIPTION
            Removes all elements that match the conditions defined by the specified ScriptBlock.
    
        .PARAMETER InputObject
            parameter1
    
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
            $list = [System.Collections.Generic.List[object]]@()
            $list.AddRange($InputObject.Value)
            [void] $list.RemoveAll($Predicate)
            $InputObject.Value = $list.ToArray()
        }
        elseif ($InputObject.Value -is [System.Collections.ICollection])
        {
            $list = [System.Collections.Generic.List[object]]@()
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

$nums = [ordered]@{ Hi = 1; Bye = 2; "So Long" = 3 }
[ref]$nums | Remove-All { $_.Key -eq "Hi" }