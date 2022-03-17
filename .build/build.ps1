$builder = New-Object -TypeName "System.Text.StringBuilder"

foreach ($priv in $(Get-ChildItem -Path "$PSScriptRoot\..\src\private" -Filter *.ps1 -Exclude "ComparerBuilder.ps1" -Recurse))
{
    [void] $builder.AppendLine((Get-Content -Path $priv.FullName -Raw))
    [void] $builder.AppendLine()
}

foreach ($pub in Get-ChildItem -Path "$PSScriptRoot\..\src\public" -Filter *.ps1)
{
    [void] $builder.AppendLine((Get-Content -Path $pub.FullName -Raw))
    [void] $builder.AppendLine()
}

Set-Content -Path "$PSScriptRoot\..\src\ListFunctions.psm1" -Value $builder.ToString() -Force

# Build DotNet Project
dotnet build "$PSScriptRoot\..\src\engine\ListFunctions.Engine.sln" -c Release

foreach ($dll in $(Get-ChildItem -Path "$PSScriptRoot\..\src\engine\ListFunctions.Engine\bin\Release" -Filter *.dll -Recurse)) {

    $dll | Copy-Item -Destination "$PSScriptRoot\..\src\assemblies" -Force
}