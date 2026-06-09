$file = "d:\Work\yudao-boot-mini\SocialMatrix.WpfHost\Windows\BrowserMatrixWindow.xaml.cs"
$lines = Get-Content $file -Encoding UTF8

$newLines = @()
foreach ($line in $lines) {
    # Fix garbled comments
    if ($line -match "鐢熸垚缇ょ粍鎴愬憳") {
        $line = $line -replace "鐢熸垚缇ょ粍鎴愬憳", "生成群组成员"
    }
    if ($line -match "鐢熸垚鐢ㄦ埛鍏崇郴") {
        $line = $line -replace "鐢熸垚鐢ㄦ埛鍏崇郴", "生成用户关系"
    }
    # Skip duplicate results declaration
    if ($line -match 'const results = \[\]') {
        continue
    }
    $newLines += $line
}

Set-Content $file -Value $newLines -Encoding UTF8
Write-Host "Fixed. Lines: $($newLines.Count)"