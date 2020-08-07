Function New-HashSet() {

    [CmdletBinding(DefaultParameterSetName = "None")]
    param (
        [Parameter(Mandatory = $false)]
        [ValidateRange(0, [int]::MaxValue)]
        [int] $Capacity = 0,

        [Parameter(Mandatory = $false, Position = 0)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [ValidateNotNull()]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [object[]] $InputObject,

        [Parameter(Mandatory = $true, ParameterSetName = "WithCustomEqualityComparer")]
        [scriptblock] $EqualityScript,

        [Parameter(Mandatory = $true, ParameterSetName = "WithCustomEqualityComparer")]
        [scriptblock] $HashCodeScript
    )
    Begin {

        if ($GenericType -is [type]) {
            $private:type = $GenericType
            $GenericType = $GenericType.FullName
        }

        if ($PSCmdlet.ParameterSetName -eq "WithCustomEqualityComparer") {
            $comparer = NewEqualityComparer -GenericType $GenericType -EqualityScript $EqualityScript -HashCodeScript $HashCodeScript
        }

        $set = New-Object -TypeName "System.Collections.Generic.HashSet[$GenericType]"($Capacity, $comparer)
        
        if ($null -eq $type) {
            $private:type = $set.GetType().GenericTypeArguments | Select-Object -First 1
        }

        Write-Verbose "HashSet - GenericType $($private:type.FullName)"
        $private:type = $private:type.MakeArrayType()
    }
    Process {

        if ($PSBoundParameters.ContainsKey("InputObject")) {
            
            $set.UnionWith(($InputObject -as $private:type))
        }
    }
    End {

        , $set
    }
}