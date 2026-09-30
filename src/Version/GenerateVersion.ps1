$ErrorActionPreference = 'Stop'
$display = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ReleaseVersion.txt')).Trim()
if ($display -cnotmatch '^(\d+)\.(\d+)([a-zA-Z])$') { throw 'Expected version like 1.20C' }
$numeric = $Matches[1] + '.' + $Matches[2] + '.' + ([int][char]$Matches[3].ToLowerInvariant() - [int][char]'a') + '.0'
$source = 'namespace HikiNotifier { public static class ProductVersion { public const string DisplayVersion = "' +
    $display + '"; public const string NumericVersion = "' + $numeric +
    '"; public static string Current => DisplayVersion; } }'
$path = Join-Path $PSScriptRoot 'VersionInfo.g.cs'
if (!(Test-Path $path) -or [IO.File]::ReadAllText($path) -ne $source) {
    [IO.File]::WriteAllText($path, $source, [Text.UTF8Encoding]::new($false))
}
