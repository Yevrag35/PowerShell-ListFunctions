Function NewEqualityComparer() {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [scriptblock] $EqualityScript,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [scriptblock] $HashCodeScript
    )

    if ($null -ne $EqualityScript -and -not ($EqualityScript -match '\$x(\s|\.|\))' -and $EqualityScript -match '\$y(\s|\.|\))')) {

        return [pscustomobject]@{
            Comparer = $null
            IsFaulted = $true
            ErrorMessage = $errMsg = 'EqualityScript does not contain valid variables ($x and $y).'
        }
    }

    if ($null -ne $HashCodeScript -and -not ($HashCodeScript -match '\$[_](\.|\s|\))')) {

        $errMsg = "HashCodeScript does not contain the required variables: '`$_'."

        return [pscustomobject]@{
            Comparer = $null
            IsFaulted = $true
            ErrorMessage = $errMsg
        }
    }

    if ($GenericType -is [type]) {

        $GenericType = $GenericType.FullName
    }

    $comparer = New-Object "ListFunctions.ScriptBlockEqualityComparer[$GenericType]"($EqualityScript, $HashCodeScript)

    [pscustomobject]@{
        Comparer = $comparer
        IsFaulted = $false
        ErrorMessage = $null
    }
}