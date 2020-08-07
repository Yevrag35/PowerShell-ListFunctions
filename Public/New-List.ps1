Function New-List() {
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $false)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $false, Position = 0)]
        [Alias("c", "cap")]
        [ValidateRange(0, [int]::MaxValue)]
        [int] $Capacity = 0,

        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [object[]] $InputObject
    )
    Begin {

        if ($GenericType -is [type]) {
            $private:type = $GenericType
            $GenericType = $GenericType.FullName
        }

        $private:list = New-Object "System.Collections.Generic.List[$GenericType]"($Capacity)
        Write-Verbose "List - Created with 'Capacity': $($private:list.Capacity)"

        if ($null -eq $type) {
            $private:type = $private:list.GetType().GenericTypeArguments | Select-Object -First 1
        }
        Write-Verbose "List - GenericType: $($private:type.FullName)"
        $private:type = $private:type.MakeArrayType()
    }
    Process {

        if ($PSBoundParameters.ContainsKey("InputObject")) {

            $private:list.AddRange(($InputObject -as $private:type))
        }

    }
    End {

        , $private:list
    }
}