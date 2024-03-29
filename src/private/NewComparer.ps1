﻿Function NewComparer() {

    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string] $GenericType,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [scriptblock] $ComparingScript
    )

    if ($null -ne $ComparingScript -and -not ($ComparingScript -match '\$x(\s|\.|\))' -and $ComparingScript -match '\$y(\s|\.|\))')) {

        return [pscustomobject]@{
            Comparer = $null
            IsFaulted = $true
            ErrorMessage = "Comparing script block does not use '`$x' and '`$y' for comparison."
        }
    }

    if ($GenericType -is [type]) {

        $GenericType = $GenericType.FullName
    }

    $newObjArgs = @{
        TypeName     = "ListFunctions.ScriptBlockComparer[$GenericType]"
    }

    if ($null -ne $ComparingScript) {

        $newObjArgs.Add("Property", @{
            ComparerScript = $ComparingScript
        })
    }

    $comparer = New-Object @newObjArgs

    [pscustomobject]@{
        Comparer     = $comparer
        IsFaulted    = $false
        ErrorMessage = $null
    }
}