Function New-SortedSet() {
    <#
        .SYNOPSIS
            Creates a Sorted set of unique objects.

        .DESCRIPTION
            Creates a new instance of 'System.Collections.Generic.SortedSet[T]' with the default or custom comparer.

        .PARAMETER GenericType
            The constraining .NET type that every object added into the set must be.

        .PARAMETER InputObject


        .PARAMETER ComparingScript
            A custom script that provides 'CompareTo' functionality for the SortedSet to use. The comparison of two objects returns an
            [int] value indicating whether one is 'less than', 'equal to', or 'greater than' the other.  The table below describes 
            the meaning of the returned [int] value.

            FYI, any object that implements 'System.IComparable' or 'System.IComparable[T]' will always have a method called 'CompareTo' that
            can be used to generate/return the logic below. Examples of .NET types that do this include [int], [string], [guid], [datetime], etc.

            '$x' must represent the 1st item to be compared.
            '$y' must represent the 2nd item to be compared.

                Value           |   Meaning
            ------------------- | --------------------
            Less than zero      | X is less than Y.
            Zero                | X equals Y.
            Greater than zero   | X is greater than Y.

        .INPUTS
            System.Object[] -- The objects that will immediately added to the returned set.

        .OUTPUTS
            System.Collections.Generic.SortedSet[T] -- where 'T' is the constrained generic type that all objects must be.

        .EXAMPLE
            Sort PSCustomObjects by a shared 'ID' property.

            $obj1 = [pscustomobject]@{ Id = 2; Name = "John" }
            $obj2 = [pscustomobject]@{ Id = 1; Name = "Jane" }

            $set = New-SortedSet -ComparingScript { $x.Id.CompareTo($y.Id) } -InputObject $obj1, $obj2

        .EXAMPLE
            Sort strings with case-sensitive reverse logic (i.e. - Descending order)

            $obj1 = "Jane"
            $obj2 = "John"

            $set = $obj1, $obj2 | New-SortedSet -GenericType [string] -ComparingScript { $x.CompareTo($y) * -1 }
    #>
    [CmdletBinding(DefaultParameterSetName = "None")]
    param (
        [Parameter(Mandatory = $false, Position = 0)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]] $InputObject,

        [Parameter(Mandatory=$true, ParameterSetName = "WithCustomComparer")]
        [Alias("ScriptBlock")]
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
        elseif ($null -eq $GenericType -or ($GenericType -is [string] -and [string]::IsNullOrWhiteSpace($GenericType))) {


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