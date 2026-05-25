# Publish-FundOffice.ps1
# 发布 FundOffice 解决方案和 Tools 项目到 publish 文件夹
# 全局排除语言文件夹、tmp 项目和 Test 项目，不输出 pdb 文件，安全清理未引用的项目 DLL

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath
$publishDir = Join-Path $scriptPath "..\..\Thor"
$languageFolders = @(
    "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR",
    "ru", "tr", "zh-Hans", "zh-Hant", "playwright"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "开始发布 FundOffice 项目" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 需要保留的文件夹列表
$foldersToKeep = @(".ck", "esign", "mission", "runtimes", "trigger", "trustee")

# 清理并发布目录
if (Test-Path $publishDir) {
    Write-Host "清理现有发布目录..." -ForegroundColor Yellow
    $allItems = Get-ChildItem -Path $publishDir -Force
    foreach ($item in $allItems) {
        if ($foldersToKeep -notcontains $item.Name) {
            Remove-Item $item.FullName -Recurse -Force
            Write-Host "  已删除: $($item.Name)" -ForegroundColor Gray
        } else {
            Write-Host "  保留: $($item.Name)" -ForegroundColor Cyan
        }
    }
} else {
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
}
Write-Host "发布目录已准备: $publishDir" -ForegroundColor Green
Write-Host ""

 
 

# 1. 发布核心项目及 src 模块
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "步骤 1: 发布核心项目及 src 模块" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$publishedProjects = @()

# 1.1 先发布 src\Client\FundOffice.csproj
$clientProject = Join-Path $scriptPath "src\Client\FundOffice.csproj"
if (Test-Path $clientProject) {
    Write-Host "发布核心项目: src\Client\FundOffice.csproj" -ForegroundColor Yellow
    dotnet publish $clientProject --configuration Release --output $publishDir --no-restore /p:DebugType=None /p:DebugSymbols=false
    if ($LASTEXITCODE -eq 0) { 
        Write-Host "✓ 核心项目发布成功" -ForegroundColor Green 
        $publishedProjects += $clientProject
    } else { 
        Write-Host "✗ 核心项目发布失败" -ForegroundColor Red; exit 1 
    }
} else { 
    Write-Host "✗ 未找到核心项目文件: $clientProject" -ForegroundColor Red; exit 1 
}
Write-Host ""

# 1.2 找 src 下有 Directory.Build.props 的目录，再按它发布
$srcPath = Join-Path $scriptPath "src"
if (Test-Path $srcPath) {
    # 🔥 规范化 src 路径，防止末尾带不带斜杠导致对比失败
    $srcPathNormalized = (Get-Item $srcPath).FullName 
    
    # 🔥 新增过滤条件：排除直接位于 src 根目录下的 Directory.Build.props
    $buildPropsFiles = Get-ChildItem -Path $srcPath -Filter "Directory.Build.props" -Recurse -File | 
                       Where-Object { $_.DirectoryName -ne $srcPathNormalized }
    
    if ($buildPropsFiles.Count -gt 0) {
        Write-Host "找到 $($buildPropsFiles.Count) 个子模块目录（已排除 src 根目录）" -ForegroundColor Green
        
        $moduleProjects = @()
        foreach ($propsFile in $buildPropsFiles) {
            $moduleDir = $propsFile.DirectoryName
            # 查找该目录下的所有项目，排除已发布的和 tmp 项目
            $projects = Get-ChildItem -Path $moduleDir -Include "*.csproj", "*.vbproj" -Recurse -File | 
                        Where-Object { $_.FullName -notin $publishedProjects -and $_.BaseName -notlike "*tmp" }
            $moduleProjects += $projects
        }
        
        # 去重（防止多个 Directory.Build.props 嵌套导致重复收集）
        $moduleProjects = $moduleProjects | Sort-Object FullName -Unique
        
        if ($moduleProjects.Count -eq 0) {
            Write-Host "⚠ 子模块目录下未找到其他需要发布的项目" -ForegroundColor Yellow
        } else {
            Write-Host "准备发布子模块目录下的 $($moduleProjects.Count) 个项目" -ForegroundColor Yellow
            $idx = 1
            foreach ($project in $moduleProjects) {
                # 获取相对路径以便更清晰地展示
                $relativePath = $project.FullName.Substring($scriptPath.Length).TrimStart('\', '/')
                Write-Host "[$idx/$($moduleProjects.Count)] 发布: $relativePath" -ForegroundColor Yellow
                
                dotnet publish $project.FullName --configuration Release --output $publishDir --no-restore /p:DebugType=None /p:DebugSymbols=false
                if ($LASTEXITCODE -eq 0) { 
                    Write-Host "  ✓ 成功" -ForegroundColor Green 
                    $publishedProjects += $project.FullName
                } else { 
                    Write-Host "  ✗ 失败" -ForegroundColor Red 
                }
                $idx++
                Write-Host ""
            }
        }
    } else {
        Write-Host "⚠ 未在 src 的子目录中找到 Directory.Build.props 文件" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠ 未找到 src 目录" -ForegroundColor Yellow
}
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

Write-Host ""

# ========================================
# 2. 发布 src\Tools 下的所有项目
# ========================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "步骤 2: 发布 src\Tools 下的所有项目" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if (Test-Path $toolsPath) {
    # 修复：Include 参数需带 * 通配符
    $allToolsProjects = Get-ChildItem -Path $toolsPath -Include "*.csproj", "*.vbproj" -Recurse -File
    
    # 同步跳过 tmp 和 Test 项目
    $toolsProjectFiles = $allToolsProjects | Where-Object { $_.BaseName -notlike "*tmp*" -and $_.BaseName -notlike "*Test*" }
    $toolsExcludedProjects = $allToolsProjects | Where-Object { $_.BaseName -like "*tmp*" -or $_.BaseName -like "*Test*" }

    if ($toolsExcludedProjects.Count -gt 0) {
        Write-Host "已跳过以下 Tools 项目: " -ForegroundColor Yellow
        $toolsExcludedProjects | ForEach-Object { Write-Host "  - $($_.BaseName) " -ForegroundColor Gray }
        Write-Host ""
    }

    if ($toolsProjectFiles.Count -eq 0) { 
        Write-Host "⚠ 未找到任何可发布的 Tools 项目文件" -ForegroundColor Yellow 
    } else {
        Write-Host "找到 $($toolsProjectFiles.Count) 个 Tools 项目（已排除 tmp/Test）" -ForegroundColor Green
        $idx = 1
        foreach ($project in $toolsProjectFiles) {
            Write-Host "[$idx/$($toolsProjectFiles.Count)] 发布: $($project.BaseName)" -ForegroundColor Yellow
            dotnet publish $project.FullName --configuration Release --output $publishDir --no-restore /p:DebugType=None /p:DebugSymbols=false
            # 修复：$LA STEXITCODE -> $LASTEXITCODE
            if ($LASTEXITCODE -eq 0) { Write-Host "  ✓ 成功" -ForegroundColor Green } else { Write-Host "  ✗ 失败" -ForegroundColor Red }
            $idx++
            Write-Host ""
        }
    }
} else { Write-Host "⚠ 未找到 src\Tools 目录" -ForegroundColor Yellow }

# ========================================
# 3. 删除语言文件夹和 pdb 文件
# ========================================
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

# ========================================
# 4. 【核心优化】安全清理未引用的项目级 DLL
# ========================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "步骤 4: 清理未被 EXE 引用的项目 DLL（保留系统/第三方库）" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. 提取仓库中所有项目的基础名称（锁定“项目级 DLL”范围）
$projectNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$searchDirs = @($scriptPath, $toolsPath)
foreach ($dir in $searchDirs) {
    if (Test-Path $dir) {
        # 同样排除 tmp 和 Test 项目，避免其 DLL 进入清理候选池
        Get-ChildItem -Path $dir -Include "*.csproj", "*.vbproj" -Recurse -File | 
            Where-Object { $_.BaseName -notlike "*tmp*" -and $_.BaseName -notlike "*Test*" } | 
            ForEach-Object { $null = $projectNames.Add($_.BaseName) }
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

# ========================================
# 完成
# ========================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "发布完成!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "发布目录: $publishDir" -ForegroundColor White