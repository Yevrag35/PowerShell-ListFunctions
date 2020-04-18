Function Remove-At()
{
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