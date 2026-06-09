$file = "d:\Work\yudao-boot-mini\SocialMatrix.WpfHost\Windows\BrowserMatrixWindow.xaml.cs"
$content = Get-Content $file -Raw

# Replace all garbled patterns with correct Chinese
$content = $content -replace [char]0x0D[char]0x0A + "        /// 鐢熸垚缇ょ粍鎴愬憳", "`n        /// 生成群组成员"
$content = $content -replace [char]0x0D[char]0x0A + "        /// 鐢熸垚鐢ㄦ埛鍏崇郴", "`n        /// 生成用户关系"

# Remove the duplicate const results = []
$pattern = 'js\.AppendLine\("        const results = \[\];"\);' + [char]0x0D[char]0x0A
$content = $content -replace $pattern, ''

Set-Content $file -Value $content -Encoding UTF8
Write-Host "Done"