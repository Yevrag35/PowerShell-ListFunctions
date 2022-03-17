#. "$PSScriptRoot\..\src\private\ComparerBuilder.ps1"
foreach ($dll in $(Get-ChildItem -Path "$PSScriptRoot\..\src\assemblies" -Filter *.dll -Recurse)) {
    Import-Module $dll.FullName
}

foreach ($priv in $(Get-ChildItem -Path "$PSScriptRoot\..\src\private" -Filter *.ps1 -Exclude "ComparerBuilder.ps1" -Recurse)) {

    . $priv.FullName
}

foreach ($pub in $(Get-ChildItem -Path "$PSScriptRoot\..\src\public" -Filter *.ps1)) {

    . $pub.FullName
}