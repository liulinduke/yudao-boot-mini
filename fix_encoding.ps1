$file = "d:\Work\yudao-boot-mini\SocialMatrix.WpfHost\Windows\BrowserMatrixWindow.xaml.cs"
$content = Get-Content $file -Raw -Encoding UTF8

# 替换重复的 results 声明
$pattern = 'js\.AppendLine\("        const results = \[\];"\);'
$replacement = ''
$content = $content -replace $pattern, $replacement

# 修复乱码的中文注释
$content = $content -replace '/// 鐢熸垚缇ょ粍鎴愬憳', '/// 生成群组成员'
$content = $content -replace '/// 鐢熸垚鐢ㄦ埛鍏崇郴', '/// 生成用户关系'

Set-Content $file -Value $content -Encoding UTF8
Write-Host "Fixed encoding and removed duplicate results declaration"