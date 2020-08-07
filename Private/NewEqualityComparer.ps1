Function NewEqualityComparer() {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $true)]
        [scriptblock] $EqualityScript,

        [Parameter(Mandatory = $true)]
        [scriptblock] $HashCodeScript
    )

    $ms = [regex]::Matches($EqualityScript, '\$(x|y)(\s|\.)', "IgnoreCase")
    if ($ms | Assert-Any -Condition { $_.Success }) {
        $replace1 = [regex]::Replace($EqualityScript, '\$x(\s|\.)', '$args[0]$1', "IgnoreCase")
        $replace2 = [regex]::Replace($replace1, '\$y(\s|\.)', '$args[1]$1', "IgnoreCase")
        $EqualityScript = [scriptblock]::Create($replace2)
    }

    if ($HashCodeScript -match '\$[_](\.|\s)') {
        $HashCodeScript = [scriptblock]::Create([regex]::Replace($HashCodeScript, '\$[_](\.|\s)', '$args[0]$1'))
    }

    if ($GenericType -is [type]) {
        $GenericType = $GenericType.FullName
    }

    New-Object -TypeName "ListFunctions.ScriptBlockComparer[$GenericType]" -Property @{
        EqualityTester = $EqualityScript
        HashCodeScript = $HashCodeScript
    }
}