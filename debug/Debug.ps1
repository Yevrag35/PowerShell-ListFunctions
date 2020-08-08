. "$PSScriptRoot\..\Private\ComparerBuilder.ps1"

foreach ($priv in $(Get-ChildItem -Path "$PSScriptRoot\..\Private" -Filter *.ps1 -Exclude "ComparerBuilder.ps1" -Recurse)) {

    . $priv.FullName
}

foreach ($pub in $(Get-ChildItem -Path "$PSScriptRoot\..\Public" -Filter *.ps1)) {

    . $pub.FullName
}