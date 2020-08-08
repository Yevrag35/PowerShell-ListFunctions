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