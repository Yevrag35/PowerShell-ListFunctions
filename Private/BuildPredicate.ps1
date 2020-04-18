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