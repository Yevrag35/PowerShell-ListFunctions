<#
.SYNOPSIS
Defines Get-BucketCount, which returns the length of a hash-based collection's bucket array.

.DESCRIPTION
A HashSet, Dictionary, or Hashtable sizes its bucket array for the capacity it's created with, so the bucket count
shows whether a command passed -Capacity on to the collection. .NET Framework has no public API that reports a
collection's capacity, so the function reads a private field: _buckets on .NET 10, m_buckets for a .NET Framework
HashSet, and buckets for a .NET Framework Dictionary or Hashtable.

Test files dot-source this script in their top-level BeforeAll block.
#>

function Get-BucketCount {
	<#
	.SYNOPSIS
	Returns the length of a HashSet's, Dictionary's, or Hashtable's bucket array, or 0 when it has none yet.
	#>
	param(
		[object] $Collection
	)

	$flags = [System.Reflection.BindingFlags]'Instance, NonPublic'
	foreach ($name in '_buckets', 'm_buckets', 'buckets') {
		$field = $Collection.GetType().GetField($name, $flags)
		if ($field) {
			$buckets = $field.GetValue($Collection)
			if ($null -eq $buckets) {
				return 0
			}
			return $buckets.Length
		}
	}

	throw "Get-BucketCount can't find the bucket array of a $($Collection.GetType().FullName)."
}
