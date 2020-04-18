foreach ($priv in Get-ChildItem -Path "$PSScriptRoot\Private" -Filter *.ps1)
{
    . "$($priv.FullName)"
}

foreach ($pub in Get-ChildItem -Path "$PSScriptRoot\Public" -Filter *.ps1)
{
    . "$($pub.FullName)"
    $pub.BaseName
}