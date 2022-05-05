Function New-SortedSet() {

    [CmdletBinding(DefaultParameterSetName = "None")]
    param (
        [Parameter(Mandatory = $false, Position = 0)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [ValidateNotNull()]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]] $InputObject,

        [Parameter(Mandatory=$true, ParameterSetName = "WithCustomComparer")]
        [AllowNull()]
        [scriptblock] $ComparingScript
    )
    DynamicParam {

        $rtDict = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary

        if (($GenericType -is [type] -and $GenericType.Name -eq "String") -or ($GenericType -is [string] -and $GenericType -in @('[string]', '[System.String]'))) {

            $pName = 'CaseSensitive'
            $attCol = New-Object 'System.Collections.ObjectModel.Collection[System.Attribute]'
            $pAtt = New-Object System.Management.Automation.ParameterAttribute -Property @{
                Mandatory = $true
                ParameterSetName = "WithStringSet"
            };
            $attCol.Add($pAtt)
            $rtParam = New-Object System.Management.Automation.RuntimeDefinedParameter($pName, $([switch]), $attCol)
            
            $rtDict.Add($pName, $rtParam)
        }

        $rtDict
    }
    Begin {

        if ($GenericType -is [type]) {
            $private:type = $GenericType
            $GenericType = $GenericType.FullName
        }

        if ($GenericType -in @('[string]', 'System.String', '[System.String]')) {

            if (-not $PSBoundParameters.ContainsKey("CaseSensitive") -or -not $PSBoundParameters["CaseSensitive"].ToBool()) {

                $IsCaseInsensitive = $true
            }
        }

        if ($PSBoundParameters.ContainsKey("ComparingScript")) {
            
            $comparer = New-Object -TypeName "ListFunctions.ScriptBlockComparer[$GenericType]"($ComparingScript)
        }
        elseif ($IsCaseInsensitive) {

            $comparer = [System.StringComparer]::CurrentCultureIgnoreCase
        }

        if ($null -ne $comparer) {

            $sortedSet = New-Object "System.Collections.Generic.SortedSet[$GenericType]"($comparer)
        }
        else {

            $sortedSet = New-Object "System.Collections.Generic.SortedSet[$GenericType]"
        }
        

        if ($null -eq $private:type) {
            
            $private:type = $sortedSet.GetType().GenericTypeArguments | Select-Object -First 1
        }

        Write-Verbose "HashSet - GenericType $($private:type.FullName)"
        $private:type = $private:type.MakeArrayType()
    }
    Process {

        if ($PSBoundParameters.ContainsKey("InputObject")) {
            
            $sortedSet.UnionWith(($InputObject -as $private:type))
        }
    }
    End {

        , $sortedSet
    }
}