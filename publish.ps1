# 发布配置
$publishDir = "./publish"
$configuration = "Release"
$runtime = "win-x64"
$selfContained = $false

# 获取所有合法 csproj（排除带 _ 的临时项目）
$projects = Get-ChildItem -Recurse -Filter *.csproj | Where-Object {
    $_.Name -notmatch '_'
}

foreach ($proj in $projects) {
    $projPath = $proj.FullName
    $projContent = Get-Content $projPath -Raw -Encoding UTF8

    # 只发布 EXE 项目（WPF/工具）
    if ($projContent -match '<OutputType>(WinExe|Exe)</OutputType>') {
        Write-Host "`n=== 发布: $($proj.Name) ===" -ForegroundColor Cyan

        # 正确版本：无警告 + 无PDB + 保留错误信息
        dotnet publish $projPath `
            -c $configuration `
            -r $runtime `
            --self-contained $selfContained `
            -o $publishDir `
            --nologo `
            /p:WarningLevel=0 `
            /p:DebugSymbols=false `
            /p:DebugType=None

        # 结果输出
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ 成功" -ForegroundColor Green
        }
        else {
            Write-Host "❌ 失败" -ForegroundColor Red
        }
    }
}

Write-Host "`n🎉 发布完成！无警告 · 无pdb · 干净输出" -ForegroundColor Green
Write-Host "📁 发布目录：$(Resolve-Path $publishDir)" -ForegroundColor Cyan
pause