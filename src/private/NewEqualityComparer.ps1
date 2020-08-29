Function NewEqualityComparer() {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $true)]
        [scriptblock] $EqualityScript,

        [Parameter(Mandatory = $false)]
        [scriptblock] $HashCodeScript = { $args[0].GetHashCode() }
    )

    if ($EqualityScript -match '\$x(\s|\.)' -and $EqualityScript -match '\$y(\s|\.)') {

        $replace1 = [regex]::Replace($EqualityScript, '\$x(\s|\.)', '$args[0]$1', "IgnoreCase")
        $replace2 = [regex]::Replace($replace1, '\$y(\s|\.)', '$args[1]$1', "IgnoreCase")
        $EqualityScript = [scriptblock]::Create($replace2)
    }
    elseif (-not ($EqualityScript -match '\$args\[0\]' -and $EqualityScript -match '\$args\[1\]')) {
        
        $errMsg = 'EqualityScript does not contain valid variables ($x and $y -or- $args[0] and $args[1]).'

        return [pscustomobject]@{
            Comparer = $null
            IsFaulted = $true
            ErrorMessage = $errMsg
        }
    }

    if ($HashCodeScript -match '\$[_](\.|\s)') {

        $HashCodeScript = [scriptblock]::Create([regex]::Replace($HashCodeScript, '\$[_](\.|\s)', '$args[0]$1'))
    }
    elseif ($HashCodeScript -notmatch '\$args\[0\]') {

        $errMsg = "HashCodeScript does not contain the required variables: '`$_' -or- '`$args[0]."

        return [pscustomobject]@{
            Comparer = $null
            IsFaulted = $true
            ErrorMessage = $errMsg
        }
    }

    if ($GenericType -is [type]) {

        $GenericType = $GenericType.FullName
    }

    $comparer = New-Object -TypeName "ListFunctions.ScriptBlockComparer[$GenericType]" -Property @{
        EqualityTester = $EqualityScript
        HashCodeScript = $HashCodeScript
    }

    [pscustomobject]@{
        Comparer = $comparer
        IsFaulted = $false
        ErrorMessage = $null
    }
}