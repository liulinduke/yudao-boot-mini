$file = "d:\Work\yudao-boot-mini\SocialMatrix.WpfHost\Windows\BrowserMatrixWindow.xaml.cs"
$content = Get-Content $file -Raw

$content = $content -replace "`r`n        /// 鐢熸垚缇ょ粍鎴愬憳", "`r`n        /// 生成群组成员"
$content = $content -replace "`r`n        /// 鐢熸垚鐢ㄦ埛鍏崇郴", "`r`n        /// 生成用户关系"
$content = $content -replace 'js\.AppendLine\("        const results = \[\];"\);' + "`r`n", ''

Set-Content $file -Value $content -Encoding UTF8
Write-Host "Done"