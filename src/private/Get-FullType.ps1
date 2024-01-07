Function Get-FullType() {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [object] $Value
    )

    $typeName = [string]::Empty
    $type = $null
    
    if ($Value -is [type]) {
        $typeName = $Value.FullName
        $type = $Value
    }
    elseif ($Value -is [string]) {
        $typeName = $Value
    }
    else {
        $typeName = $Value.ToString()
    }

    return [pscustomobject]@{
        Name = $typeName
        IsType = $null -ne $type
        Type = $type
    }
}