switch ($PSVersionTable.PSVersion.Major) {
    5 { $script:dllPath = "$PSScriptRoot\Desk\ListFunctions.NETFramework.dll" }
    7 { $script:dllPath = "$PSScriptRoot\Core\ListFunctions.Next.dll" }
    default { throw "Incompatible PowerShell Version" }
}

$script:assFolder = Split-Path -Path $script:dllPath -Parent
$script:loadThese = @(
    'MG.Collections.dll',
    'ListFunctions.Engine.dll'
)

foreach ($script:assName in $script:loadThese) {

    Import-Module "$($script:assFolder)\$($script:assName)"
}

Import-Module $script:dllPath -ErrorAction Stop