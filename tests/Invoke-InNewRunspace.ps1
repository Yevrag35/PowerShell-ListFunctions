<#
.SYNOPSIS
Defines Invoke-InNewRunspace, which runs a script in a new runspace that has ListFunctions imported.

.DESCRIPTION
Pester runs each test inside a try block. There, an error that ends only the statement it occurs in and an error
that ends the whole script both jump to the catch block, so a test can't tell them apart in its own runspace.
Invoke-InNewRunspace runs the script with no enclosing try block, the way PowerShell runs a script file or a command
typed at the prompt. The new runspace imports the same build as the tests, through Import-ListFunctions.ps1.

Test files dot-source this script in their top-level BeforeAll block.
#>

function Invoke-InNewRunspace {
	<#
	.SYNOPSIS
	Runs a script in a new runspace that has ListFunctions imported, and returns what the script wrote and how it ended.

	.DESCRIPTION
	Returns an object with these properties:

	- Output: the objects that the script wrote to the output stream, unwrapped from their PSObject. When an error
	  ended the script, Output is empty.
	- Errors: the error records in the script's error stream. An error that ends only its statement lands here.
	- StoppedBy: the error record of the error that ended the script, or $null when the script ran to its end.

	.PARAMETER Script
	The script to run.
	#>
	param(
		[Parameter(Mandatory)]
		[string] $Script
	)

	$importPath = Join-Path $PSScriptRoot 'Import-ListFunctions.ps1'
	$powerShell = [powershell]::Create()
	try {
		$null = $powerShell.AddScript("& '$($importPath.Replace("'", "''"))'").Invoke()
		$powerShell.Commands.Clear()
		$powerShell.Streams.Error.Clear()

		$output = @()
		$stoppedBy = $null
		try {
			$output = @(foreach ($item in $powerShell.AddScript($Script).Invoke()) {
				if ($null -eq $item) {
					$null
				}
				else {
					# $item.BaseObject would read a member of the wrapped object instead of the PSObject's own property.
					$item.psobject.BaseObject
				}
			})
		}
		catch {
			# PowerShell wraps the exception that ended the script in a MethodInvocationException.
			$exception = $_.Exception
			if ($exception -is [System.Management.Automation.MethodInvocationException] -and $exception.InnerException) {
				$exception = $exception.InnerException
			}
			$stoppedBy = $exception.ErrorRecord
		}

		[pscustomobject]@{
			Output    = [object[]]$output
			Errors    = [object[]]@($powerShell.Streams.Error)
			StoppedBy = $stoppedBy
		}
	}
	finally {
		$powerShell.Dispose()
	}
}
