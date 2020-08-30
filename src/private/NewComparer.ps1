Function NewComparer() {

    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string] $GenericType,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [scriptblock] $ComparingScript,

        [Parameter(Mandatory = $true)]
        [bool] $IsCaseSensitive
    )

    if ($ComparingScript -match '\$x(\s|\.|\))' -and $ComparingScript -match '\$y(\s|\.|\))') {

        $replace1 = [regex]::Replace($ComparingScript, '\$x(\s|\.|\))', '$args[0]$1', "IgnoreCase")
        $replace2 = [regex]::Replace($replace1, '\$y(\s|\.|\))', '$args[1]$1', "IgnoreCase")

        $ComparingScript = [scriptblock]::Create($replace2)
    }
    elseif (-not ($ComparingScript -match '\$args\[0\]' -and $ComparingScript -match '\$args\[1\]')) {

        return [pscustomobject]@{
            Comparer = New-Object -TypeName "ListFunctions.ScriptBlockComparer[$GenericType]" -ArgumentList $IsCaseSensitive
            IsFaulted = $false
            ErrorMessage = $null
        }
    }

    if ($GenericType -is [type]) {

        $GenericType = $GenericType.FullName
    }

    $newObjArgs = @{
        TypeName     = "ListFunctions.ScriptBlockComparer[$GenericType]"
        ArgumentList = $IsCaseSensitive
        Property     = @{
            CompareScript = $ComparingScript
        }
    }

    $comparer = New-Object @newObjArgs

    [pscustomobject]@{
        Comparer     = $comparer
        IsFaulted    = $false
        ErrorMessage = $null
    }
}