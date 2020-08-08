$builder = New-Object -TypeName "System.Text.StringBuilder"

foreach ($priv in $(Get-ChildItem -Path "$PSScriptRoot\..\Private" -Filter *.ps1 -Exclude "ComparerBuilder.ps1" -Recurse))
{
    [void] $builder.AppendLine((Get-Content -Path $priv.FullName -Raw))
    [void] $builder.AppendLine()
}

foreach ($pub in Get-ChildItem -Path "$PSScriptRoot\..\Public" -Filter *.ps1)
{
    [void] $builder.AppendLine((Get-Content -Path $pub.FullName -Raw))
    [void] $builder.AppendLine()
}

Set-Content -Path "$PSScriptRoot\..\ListFunctions.psm1" -Value $builder.ToString() -Force