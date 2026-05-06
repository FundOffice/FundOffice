# Publish-FundOffice.ps1
# 发布 FundOffice 解决方案和 Tools 项目到 publish 文件夹
# 排除语言文件夹和 tmp 项目，不输出 pdb 文件，安全清理未引用的项目 DLL

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

$publishDir = Join-Path $scriptPath "publish"
$languageFolders = @(
    "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", 
    "ru", "tr", "zh-Hans", "zh-Hant", "playwright"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "开始发布 FundOffice 项目" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 清理并发布目录
if (Test-Path $publishDir) {
    Write-Host "清理现有发布目录..." -ForegroundColor Yellow
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
Write-Host "发布目录已创建: $publishDir" -ForegroundColor Green
Write-Host ""

# 1. 发布解决方案
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "步骤 1: 发布 FundOffice.slnx 解决方案" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$solutionFile = Join-Path $scriptPath "FundOffice.slnx"
if (Test-Path $solutionFile) {
    Write-Host "发布解决方案: $solutionFile" -ForegroundColor Yellow
    dotnet publish $solutionFile --configuration Release --output $publishDir --no-restore /p:DebugType=None /p:DebugSymbols=false
    if ($LASTEXITCODE -eq 0) { Write-Host "✓ 解决方案发布成功" -ForegroundColor Green } else { Write-Host "✗ 解决方案发布失败" -ForegroundColor Red; exit 1 }
} else { Write-Host "✗ 未找到解决方案文件: $solutionFile" -ForegroundColor Red; exit 1 }
Write-Host ""

# 2. 发布 src\Tools 下的所有项目
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "步骤 2: 发布 src\Tools 下的所有项目" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$toolsPath = Join-Path $scriptPath "src\Tools"
if (Test-Path $toolsPath) {
    $allProjects = Get-ChildItem -Path $toolsPath -Include "*.csproj", "*.vbproj" -Recurse -File
    $projectFiles = $allProjects | Where-Object { $_.BaseName -notlike "*tmp" }
    $excludedProjects = $allProjects | Where-Object { $_.BaseName -like "*tmp" }
    
    if ($excludedProjects.Count -gt 0) {
        Write-Host "排除以下 tmp 项目:" -ForegroundColor Yellow
        $excludedProjects | ForEach-Object { Write-Host "  - $($_.BaseName)" -ForegroundColor Gray }
        Write-Host ""
    }
    
    if ($projectFiles.Count -eq 0) { Write-Host "⚠ 未找到任何项目文件" -ForegroundColor Yellow }
    else {
        Write-Host "找到 $($projectFiles.Count) 个项目文件（已排除 tmp）" -ForegroundColor Green
        $idx = 1
        foreach ($project in $projectFiles) {
            Write-Host "[$idx/$($projectFiles.Count)] 发布: $($project.BaseName)" -ForegroundColor Yellow
            dotnet publish $project.FullName --configuration Release --output $publishDir --no-restore /p:DebugType=None /p:DebugSymbols=false
            if ($LASTEXITCODE -eq 0) { Write-Host "  ✓ 成功" -ForegroundColor Green } else { Write-Host "  ✗ 失败" -ForegroundColor Red }
            $idx++; Write-Host ""
        }
    }
} else { Write-Host "⚠ 未找到 src\Tools 目录" -ForegroundColor Yellow }

# 3. 删除语言文件夹和 pdb 文件
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "步骤 3: 清理语言文件夹和 pdb 文件" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$deletedLang = 0
foreach ($lang in $languageFolders) {
    $p = Join-Path $publishDir $lang
    if (Test-Path $p) { Remove-Item $p -Recurse -Force; $deletedLang++ }
}
if ($deletedLang -gt 0) { Write-Host "✓ 已删除 $deletedLang 个语言文件夹" -ForegroundColor Green }
else { Write-Host "  无语言文件夹需要删除" -ForegroundColor Gray }

$pdbFiles = Get-ChildItem -Path $publishDir -Filter "*.pdb" -Recurse -File
if ($pdbFiles.Count -gt 0) {
    $pdbFiles | Remove-Item -Force
    Write-Host "✓ 已删除 $($pdbFiles.Count) 个 pdb 文件" -ForegroundColor Green
} else { Write-Host "  无 pdb 文件" -ForegroundColor Gray }
Write-Host ""

# 4. 【核心优化】安全清理未引用的项目级 DLL
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "步骤 4: 清理未被 EXE 引用的项目 DLL（保留系统/第三方库）" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. 提取仓库中所有项目的基础名称（锁定“项目级 DLL”范围）
$projectNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$searchDirs = @($scriptPath, (Join-Path $scriptPath "src\Tools"))
foreach ($dir in $searchDirs) {
    if (Test-Path $dir) {
        Get-ChildItem -Path $dir -Include "*.csproj", "*.vbproj" -Recurse -File | ForEach-Object { $null = $projectNames.Add($_.BaseName) }
    }
}

$exes = Get-ChildItem -Path $publishDir -Filter "*.exe" -File
$referencedDlls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

if ($exes.Count -gt 0) {
    Write-Host "正在分析 EXE 依赖关系..." -ForegroundColor Yellow
    foreach ($exe in $exes) {
        $depsFile = Join-Path $publishDir "$($exe.BaseName).deps.json"
        if (Test-Path $depsFile) {
            try {
                $json = Get-Content $depsFile -Raw | ConvertFrom-Json
                if ($json.libraries) {
                    foreach ($lib in $json.libraries.PSObject.Properties.Name) {
                        $null = $referencedDlls.Add(($lib -split '/')[0])
                    }
                }
            } catch {}
        }
    }
}

# 2. 仅筛选“项目级 DLL”进行检查
$allDlls = Get-ChildItem -Path $publishDir -Filter "*.dll" -File
$candidateDlls = $allDlls | Where-Object { $projectNames.Contains($_.BaseName) }

if ($candidateDlls.Count -eq 0) {
    Write-Host "  未发现项目级 DLL，跳过清理" -ForegroundColor Gray
} else {
    $unusedDlls = $candidateDlls | Where-Object { -not $referencedDlls.Contains($_.BaseName) }
    $delCount = 0
    foreach ($dll in $unusedDlls) {
        $base = $dll.BaseName
        Write-Host "  删除未引用项目 DLL: $($dll.Name)" -ForegroundColor Yellow
        Remove-Item $dll.FullName -Force -ErrorAction SilentlyContinue
        # 同步清理附属文件
        @(".xml", ".config") | ForEach-Object {
            $path = Join-Path $publishDir "$base$_"
            if (Test-Path $path) { Remove-Item $path -Force -ErrorAction SilentlyContinue }
        }
        $delCount++
    }

    if ($delCount -eq 0) { Write-Host "  ✓ 所有项目 DLL 均已被引用，无需清理" -ForegroundColor Green }
    else { Write-Host "  ✓ 安全清理 $delCount 个未引用的项目 DLL 及附属文件" -ForegroundColor Green }
    Write-Host "  (系统库与第三方库已自动保留)" -ForegroundColor Gray
}
Write-Host ""

# 完成
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "发布完成!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "发布目录: $publishDir" -ForegroundColor White

