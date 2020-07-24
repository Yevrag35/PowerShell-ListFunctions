Function New-HashSet() {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [int] $Capacity = 1,

        [Parameter(Mandatory = $false, Position = 0)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $false)]
        [ValidateScript({
            $true -in @(
                $_.GetType().ImplementedInterfaces.FullName | Foreach-Object {
                    $_ -like "System.Collections.Generic.IEqualityComparer*"
                }
            )
        })]
        [object] $EqualityComparer
    )

    if ($GenericType -is [type]) {
        $GenericType = $GenericType.FullName
    }

    if ($PSBoundParameters.ContainsKey("Capacity") -and -not $PSBoundParameters.ContainsKey("EqualityComparer"))
    {
        $set = New-Object -TypeName "System.Collections.Generic.Hashset[$GenericType]"($Capacity)
    }
    elseif (-not $PSBoundParameters.ContainsKey("Capacity") -and $PSBoundParameters.ContainsKey("EqualityComparer"))
    {
        $set = New-Object -TypeName "System.Collections.Generic.Hashset[$GenericType]"($EqualityComparer)
    }
    elseif ($PSBoundParameters.ContainsKey("Capacity") -and $PSBoundParameters.ContainsKey("EqualityComparer"))
    {
        $set = New-Object -TypeName "System.Collections.Generic.Hashset[$GenericType]"($Capacity, $EqualityComparer)
    }
    else {
        $set = New-Object -TypeName "System.Collections.Generic.Hashset[$GenericType]"($Capacity)
    }
    , $set
}