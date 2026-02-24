$root = "W:\1_DXP_Projects\Unity\PlasticBag_Game_0705"
$excludes = @("Library", "Temp", "obj", "Logs")

$files = Get-ChildItem -Path $root -Recurse -File | Where-Object {
    $skip = $false
    foreach ($ex in $excludes) {
        if ($_.FullName -match "\\$ex\\") { $skip = $true; break }
    }
    -not $skip
} | Sort-Object Length -Descending | Select-Object -First 50

Write-Host "=== 상위 50개 큰 파일 (Library/Temp 제외) ===" -ForegroundColor Cyan
Write-Host ("{0,-10} {1}" -f "크기(MB)", "파일 경로")
Write-Host ("-" * 80)
foreach ($f in $files) {
    $sizeMB = [math]::Round($f.Length / 1MB, 2)
    $relPath = $f.FullName.Replace($root + "\", "")
    Write-Host ("{0,-10} {1}" -f $sizeMB, $relPath)
}

Write-Host ""
Write-Host "=== 전체 Assets 폴더 크기 ===" -ForegroundColor Yellow
$assetsSize = (Get-ChildItem "$root\Assets" -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("Assets 폴더 총 크기: {0:F2} MB" -f ($assetsSize / 1MB))
