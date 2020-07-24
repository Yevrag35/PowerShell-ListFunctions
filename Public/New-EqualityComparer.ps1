Function New-EqualityComparer() {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory=$true)]
        [scriptblock] $EqualityScript,

        [Parameter(Mandatory=$true)]
        [scriptblock] $HashCodeScript
    )

    if ($GenericType -is [type]) {
        $GenericType = $GenericType.FullName
    }

    New-Object -TypeName "ListFunctions.ScriptBlockComparer[$GenericType]" -Property @{
        EqualityTester = $EqualityScript
        HashCodeScript = $HashCodeScript
    }
}