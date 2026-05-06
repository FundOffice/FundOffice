# Publish-FundOffice.ps1
# 发布 FundOffice 解决方案和 Tools 项目到 publish 文件夹
# 排除语言文件夹和 tmp 项目，不输出 pdb 文件，清理未被 EXE 引用的 DLL

# 设置错误处理
$ErrorActionPreference = "Stop"

# 获取脚本所在目录
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# 定义发布目录
$publishDir = Join-Path $scriptPath "publish"

# 定义需要排除的语言文件夹列表
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
    
    dotnet publish $solutionFile `
        --configuration Release `
        --output $publishDir `
        --no-restore `
        /p:DebugType=None `
        /p:DebugSymbols=false
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ 解决方案发布成功" -ForegroundColor Green
    } else {
        Write-Host "✗ 解决方案发布失败" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "✗ 未找到解决方案文件: $solutionFile" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 2. 发布 src\Tools 下的所有项目（直接到 publish 根目录）
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "步骤 2: 发布 src\Tools 下的所有项目" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$toolsPath = Join-Path $scriptPath "src\Tools"
if (Test-Path $toolsPath) {
    Write-Host "搜索项目文件..." -ForegroundColor Yellow
    
    # 查找所有 .csproj 和 .vbproj 文件
    $allProjects = Get-ChildItem -Path $toolsPath -Include "*.csproj", "*.vbproj" -Recurse -File
    
    # 排除以 tmp 结尾的项目
    $projectFiles = $allProjects | Where-Object { $_.BaseName -notlike "*tmp" }
    $excludedProjects = $allProjects | Where-Object { $_.BaseName -like "*tmp" }
    
    if ($excludedProjects.Count -gt 0) {
        Write-Host "排除以下 tmp 项目:" -ForegroundColor Yellow
        foreach ($proj in $excludedProjects) {
            Write-Host "  - $($proj.BaseName)" -ForegroundColor Gray
        }
        Write-Host ""
    }
    
    if ($projectFiles.Count -eq 0) {
        Write-Host "⚠ 未找到任何项目文件" -ForegroundColor Yellow
    } else {
        Write-Host "找到 $($projectFiles.Count) 个项目文件（已排除 tmp 项目）" -ForegroundColor Green
        Write-Host ""
        
        $projectIndex = 1
        foreach ($project in $projectFiles) {
            $projectName = $project.BaseName
            $projectOutputDir = $publishDir
            
            Write-Host "[$projectIndex/$($projectFiles.Count)] 发布项目: $projectName" -ForegroundColor Yellow
            Write-Host "  发布到: $projectOutputDir" -ForegroundColor Gray
            
            dotnet publish $project.FullName `
                --configuration Release `
                --output $projectOutputDir `
                --no-restore `
                /p:DebugType=None `
                /p:DebugSymbols=false
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  ✓ 发布成功" -ForegroundColor Green
            } else {
                Write-Host "  ✗ 发布失败" -ForegroundColor Red
            }
            
            $projectIndex++
            Write-Host ""
        }
    }
} else {
    Write-Host "⚠ 未找到 src\Tools 目录" -ForegroundColor Yellow
}

# 3. 删除语言文件夹和 pdb 文件
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "步骤 3: 清理语言文件夹和 pdb 文件" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$deletedCount = 0

# 删除语言文件夹
Write-Host "检查并删除不需要的语言文件夹..." -ForegroundColor Yellow
foreach ($langFolder in $languageFolders) {
    $folderPath = Join-Path $publishDir $langFolder
    if (Test-Path $folderPath) {
        Remove-Item $folderPath -Recurse -Force
        Write-Host "  ✓ 已删除: $langFolder" -ForegroundColor Yellow
        $deletedCount++
    }
}

if ($deletedCount -eq 0) {
    Write-Host "  没有找到需要删除的语言文件夹" -ForegroundColor Gray
} else {
    Write-Host "  共删除 $deletedCount 个语言文件夹" -ForegroundColor Green
}

Write-Host ""

# 删除 pdb 文件
Write-Host "删除 pdb 调试文件..." -ForegroundColor Yellow
$pdbFiles = Get-ChildItem -Path $publishDir -Filter "*.pdb" -Recurse -File
$pdbCount = $pdbFiles.Count

if ($pdbCount -gt 0) {
    foreach ($pdb in $pdbFiles) {
        Remove-Item $pdb.FullName -Force
    }
    Write-Host "  ✓ 已删除 $pdbCount 个 pdb 文件" -ForegroundColor Green
} else {
    Write-Host "  没有找到 pdb 文件" -ForegroundColor Gray
}
Write-Host ""

# 4. 【新增】清理未被任何 EXE 引用的 DLL 及附属文件
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "步骤 4: 清理未被 EXE 引用的 DLL 及附属文件" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$exes = Get-ChildItem -Path $publishDir -Filter "*.exe" -File
$referencedDlls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

if ($exes.Count -gt 0) {
    Write-Host "正在分析 EXE 依赖关系..." -ForegroundColor Yellow
    
    # 解析每个 EXE 的 .deps.json 获取引用列表
    foreach ($exe in $exes) {
        $depsFile = Join-Path $publishDir "$($exe.BaseName).deps.json"
        if (Test-Path $depsFile) {
            try {
                $depsContent = Get-Content $depsFile -Raw | ConvertFrom-Json
                if ($depsContent.libraries) {
                    foreach ($lib in $depsContent.libraries.PSObject.Properties.Name) {
                        # .deps.json 中库名格式为 "PackageName/Version"，取第一部分即为 DLL 基础名
                        $libName = ($lib -split '/')[0]
                        [void]$referencedDlls.Add($libName)
                    }
                }
            } catch {
                Write-Host "  ⚠ 解析 $($depsFile.Name) 失败: $_" -ForegroundColor Yellow
            }
        }
    }
    
    # 获取根目录下所有 DLL（不递归）
    $allDlls = Get-ChildItem -Path $publishDir -Filter "*.dll" -File
    $unusedDlls = $allDlls | Where-Object { -not $referencedDlls.Contains($_.BaseName) }
    
    $deletedDllCount = 0
    foreach ($dll in $unusedDlls) {
        $baseName = $dll.BaseName
        Write-Host "  删除未引用 DLL: $($dll.Name)" -ForegroundColor Yellow
        
        # 删除 DLL 本身
        Remove-Item $dll.FullName -Force -ErrorAction SilentlyContinue
        
        # 删除常见附属文件 (.xml 文档注释, .config 配置文件)
        $assocExtensions = @(".xml", ".config")
        foreach ($ext in $assocExtensions) {
            $assocPath = Join-Path $publishDir "$baseName$ext"
            if (Test-Path $assocPath) {
                Remove-Item $assocPath -Force -ErrorAction SilentlyContinue
            }
        }
        $deletedDllCount++
    }
    
    if ($deletedDllCount -eq 0) {
        Write-Host "  未发现未引用的 DLL" -ForegroundColor Gray
    } else {
        Write-Host "  ✓ 共清理 $deletedDllCount 个未引用的 DLL 及附属文件" -ForegroundColor Green
    }
} else {
    Write-Host "  ⚠ 发布目录中未找到 .exe 文件，跳过 DLL 依赖清理" -ForegroundColor Yellow
}
Write-Host ""

# 完成
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "发布完成!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "发布目录: $publishDir" -ForegroundColor White

 