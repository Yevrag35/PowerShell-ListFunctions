
foreach ($dll in $(Get-ChildItem -Path "$PSScriptRoot\..\src\assemblies" -Filter *.dll -Recurse -ea 0)) {
    Import-Module $dll.FullName
}

foreach ($priv in $(Get-ChildItem -Path "$PSScriptRoot\..\src\private" -Filter *.ps1 -Recurse)) {

    . $priv.FullName
}

foreach ($pub in $(Get-ChildItem -Path "$PSScriptRoot\..\src\public" -Filter *.ps1)) {

    . $pub.FullName
}