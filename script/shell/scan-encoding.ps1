$ErrorActionPreference = 'Stop'

$Root = if ($args.Count -gt 0 -and $args[0]) { $args[0] } else { (Get-Location).Path }
$MaxItems = if ($args.Count -gt 1 -and $args[1]) { [int]$args[1] } else { 200 }
$IncludeExtensions = @(
  '.cs', '.csproj', '.sln', '.props', '.targets', '.xaml', '.xml', '.config', '.resx', '.sql',
  '.ps1', '.bat', '.cmd',
  '.js', '.jsx', '.ts', '.tsx', '.vue', '.json', '.yml', '.yaml', '.md', '.css', '.scss', '.html', '.mjs', '.cjs',
  '.java', '.sh'
)
$ExcludeDirNames = @('.git', 'node_modules', 'dist', 'build', 'target', 'bin', 'obj', '.idea', '.vs', '.vscode')

function Test-Utf8NoBom {
  param([byte[]]$Bytes)
  try {
    $utf8 = New-Object System.Text.UTF8Encoding($false, $true)
    [void]$utf8.GetString($Bytes)
    return $true
  } catch {
    return $false
  }
}

function Get-EncodingKind {
  param([byte[]]$Bytes)

  if ($Bytes.Length -eq 0) { return 'empty' }
  if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) { return 'utf8-bom' }
  if ($Bytes.Length -ge 4 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xFE -and $Bytes[2] -eq 0x00 -and $Bytes[3] -eq 0x00) { return 'utf32-le' }
  if ($Bytes.Length -ge 4 -and $Bytes[0] -eq 0x00 -and $Bytes[1] -eq 0x00 -and $Bytes[2] -eq 0xFE -and $Bytes[3] -eq 0xFF) { return 'utf32-be' }
  if ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xFE) { return 'utf16-le' }
  if ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFE -and $Bytes[1] -eq 0xFF) { return 'utf16-be' }
  if (Test-Utf8NoBom -Bytes $Bytes) { return 'utf8' }
  return 'ansi-or-nonutf8'
}

function Should-ExcludePath {
  param([string]$FullName)

  foreach ($name in $ExcludeDirNames) {
    if ($FullName -match "(^|[\\/])" + [regex]::Escape($name) + "([\\/]|$)") {
      return $true
    }
  }
  return $false
}

$files = Get-ChildItem -Path $Root -Recurse -File | Where-Object {
  ($IncludeExtensions -contains $_.Extension.ToLowerInvariant()) -and
  -not (Should-ExcludePath -FullName $_.FullName)
}

$summary = [ordered]@{}
$items = New-Object System.Collections.Generic.List[object]

foreach ($file in $files) {
  $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
  $kind = Get-EncodingKind -Bytes $bytes

  if (-not $summary.Contains($kind)) {
    $summary[$kind] = 0
  }
  $summary[$kind]++

  if ($kind -ne 'utf8' -and $kind -ne 'utf8-bom' -and $kind -ne 'empty') {
    $items.Add([pscustomobject]@{
      Encoding = $kind
      Path = $file.FullName
    })
  }
}

Write-Host "Root: $Root"
Write-Host ''
Write-Host 'Summary:'
$summary.GetEnumerator() | Sort-Object Name | ForEach-Object {
  "{0,-15} {1,6}" -f $_.Key, $_.Value
}

Write-Host ''
Write-Host 'Non-UTF candidates:'
$items | Sort-Object Encoding, Path | Select-Object -First $MaxItems | Format-Table -AutoSize

if ($items.Count -gt $MaxItems) {
  Write-Host ''
  Write-Host ("... truncated, showing first {0} of {1} items" -f $MaxItems, $items.Count)
}
