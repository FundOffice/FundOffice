@echo off
set "source_folder=%~dp0"
set "destination_folder1=D:\fmo\plugins"
set "destination_folder2=E:\fmo\plugins"

if not exist "%source_folder%" (
    echo 源文件夹不存在。
    pause
    exit /b
)

rem 处理第一个目标文件夹
if not exist "%destination_folder1%" (
    mkdir "%destination_folder1%"
    echo 已创建目标文件夹: %destination_folder1%
)
echo 开始复制文件到 %destination_folder1%...
xcopy "%source_folder%\*.dll" "%destination_folder1%" /s /y
if %errorlevel% equ 0 (
    echo 文件已成功复制到 %destination_folder1%。
) else (
    echo 复制到 %destination_folder1% 时出错。
)

rem 处理第二个目标文件夹
if exist "%destination_folder2%" (
    echo 开始复制文件到 %destination_folder2%...
    xcopy "%source_folder%\*.dll" "%destination_folder2%" /s /y
    if %errorlevel% equ 0 (
        echo 文件已成功复制到 %destination_folder2%。
    ) else (
        echo 复制到 %destination_folder2% 时出错。
    )
) else (
    echo 目标文件夹 %destination_folder2% 不存在，跳过复制。
)

pause