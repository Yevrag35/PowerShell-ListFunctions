Function New-Dictionary() {
    [CmdletBinding(DefaultParameterSetName = "None", PositionalBinding = $false)]
    [OutputType([System.Collections.IDictionary])]
    param (
        [Parameter(Mandatory = $false)]
        [int] $Capacity,

        [Parameter(Mandatory = $false, Position = 0)]
        [Alias("KeyAs")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [ValidateNotNull()]
        [object] $KeyType = "[object]",

        [Parameter(Mandatory = $false, Position = 1)]
        [Alias("ValueAs")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [ValidateNotNull()]
        [object] $ValueType = "[object]",

        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]] $InputObject,

        [Parameter(Mandatory = $true, ParameterSetName = "WithCustomEqualityComparer")]
        [scriptblock] $KeyEqualityScript,

        [Parameter(Mandatory = $false, ParameterSetName = "WithCustomEqualityComparer")]
        [scriptblock] $KeyHashCodeScript
    )
    DynamicParam {

        $rtDict = New-Object -TypeName 'System.Management.Automation.RuntimeDefinedParameterDictionary'

        if (($KeyType -is [type] -and $KeyType.Name -eq "String") -or ($KeyType -is [string] -and $KeyType -in @('[string]', '[System.String]'))) {

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
        $keyTypeResult = Get-FullType -Value $KeyType
        if ($keyTypeResult.IsType) {
            $private:kType = $keyTypeResult.Type
            $KeyType = $keyTypeResult.Name
        }

        $valueTypeResult = Get-FullType -Value $ValueType
        if ($valueTypeResult.IsType) {
            $private:vType = $valueTypeResult.Type
            $ValueType = $valueTypeResult.Name
        }

        if ($KeyType -in @('[string]', 'System.String', '[System.String]')) {

            if (-not $PSBoundParameters.ContainsKey("CaseSensitive") -or -not $PSBoundParameters["CaseSensitive"].ToBool()) {

                $IsCaseInsensitive = $true
            }
        }

        if ($PSCmdlet.ParameterSetName -like "*CustomEquality*") {

            $result = NewEqualityComparer -GenericType $KeyType -EqualityScript $KeyEqualityScript -HashCodeScript $KeyHashCodeScript

            if ($result.IsFaulted) {
                Write-Error -Message $result.ErrorMessage -Category SyntaxError -ErrorId $([System.ArgumentException]).FullName
            }

            $comparer = $result.Comparer
            $dict = New-Object -TypeName "System.Collections.Generic.Dictionary[$KeyType,$ValueType]"($Capacity, $comparer)
        }
        else {

            if ($IsCaseInsensitive) {

                $dict = New-Object -TypeName "System.Collections.Generic.Dictionary[$KeyType,$ValueType]"($Capacity, [System.StringComparer]::CurrentCultureIgnoreCase)
            }
            else {

                $dict = New-Object -TypeName "System.Collections.Generic.Dictionary[$KeyType,$ValueType]"($Capacity)
            }
        }

        $genArgs = $dict.GetType().GenericTypeArguments
        if ($null -eq $private:kType) {
            $private:kType = $genArgs | Select-Object -First 1
        }
        if ($null -eq $private:vType) {
            $private:vType = $genArgs | Select-Object -Skip 1 | Select-Object -First 1
        }
    }
    End {
        , $dict
    }
}