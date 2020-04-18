$objs = [System.Collections.Generic.List[object]]@(
    [pscustomobject]@{ Whatev = 1 },
    [pscustomobject]@{ Whatev = 2 },
    [pscustomobject]@{ Whatev = 1; Another = "Property" },
    [pscustomobject]@{ Whatev = @(3, 7)},
    [pscustomobject]@{
        Whatev = @(
            4,5,6
        )
    }
)

foreach ($priv in Get-ChildItem -Path "$PSScriptRoot\Private" -Filter *.ps1)
{
    . "$($priv.FullName)"
}

foreach ($pub in Get-ChildItem -Path "$PSScriptRoot\Public" -Filter *.ps1)
{
    . "$($pub.FullName)"
}