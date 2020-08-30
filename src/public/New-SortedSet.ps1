Function New-SortedSet() {

    [CmdletBinding(DefaultParameterSetName = "None")]
    param (
        [Parameter(Mandatory = $false, Position = 0)]
        [Alias("Type", "t")]
        [ValidateScript( { $_ -is [type] -or $_ -is [string] })]
        [ValidateNotNull()]
        [object] $GenericType = "[object]",

        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [object[]] $InputObject,

        [Parameter(Mandatory=$true, ParameterSetName = "WithCustomComparer")]
        [scriptblock] $ComparingScript,

        [Parameter(Mandatory=$false, ParameterSetName="WithCustomComparer")]
        [switch] $CaseSensitive,

        [Parameter(Mandatory=$true, ParameterSetName="StringInsensitiveComparer")]
        [switch] $IgnoreCaseStringComparer,

        [Parameter(Mandatory=$true, ParameterSetName="WithComparer")]
        [object] $Comparer
    )
    Begin {

        if ($GenericType -is [type]) {
            $private:type = $GenericType
            $GenericType = $GenericType.FullName
        }

        $intComparer = switch ($PSCmdlet.ParameterSetName) {

            "StringInsensitiveComparer" {
                
                $useOrNot = -not $IgnoreCaseStringComparer.ToBool()

                $private:type = $([string])
                $GenericType = $private:type.FullName
                $result = NewComparer -GenericType $GenericType -IsCaseSensitive:$useOrNot
                $result.Comparer
            }

            "WithComparer" {

                if (-not ($Comparer -is [System.Collections.IComparer] -or $Comparer -is "[System.Collections.Generic.IComparer[$GenericType]")) {

                    Write-Error -Message "The specified comparer does not implement `"[System.Collections.IComparer]`"."
                    break
                }

                $Comparer
            }
            "WithCustomComparer" {
                
                $comparerArgs = @{
                    GenericType     = $GenericType
                    ComparingScript = $ComparingScript
                    IsCaseSensitive = $CaseSensitive.ToBool()
                }
                $result = NewComparer @comparerArgs
                
                if ($result.IsFaulted) {
                    Write-Error -Message $result.ErrorMessage -Category SyntaxError -ErrorId 'System.ArgumentException'
                }

                $result.Comparer
            }
            default { }
        }

        $sortedSet = New-Object -TypeName "ListFunctions.SortedSetList[$GenericType]"($intComparer)

        if ($null -eq $private:type) {
            
            $private:type = $set.GetType().GenericTypeArguments | Select-Object -First 1
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