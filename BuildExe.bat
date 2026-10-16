@echo off
chcp 65001 >nul
title 正在建立 3D 狂牛遊戲 EXE...

set CSC64=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set CSC32=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if exist "%CSC64%" (
    set CSC="%CSC64%"
) else if exist "%CSC32%" (
    set CSC="%CSC32%"
) else (
    echo 找不到系統內建的 C# 編譯器！
    pause
    exit /b
)

echo 正在將 CowGame.cs 編譯成 CowGame3D.exe ...
%CSC% /target:winexe /optimize+ /out:CowGame3D.exe CowGame.cs

if %errorlevel% equ 0 (
    echo.
    echo ==========================================
    echo ✅ 成功生成 CowGame3D.exe！正在啟動遊戲...
    echo ==========================================
    start "" "CowGame3D.exe"
) else (
    echo.
    echo ❌ 編譯失敗，請檢查 CowGame.cs 是否完整複製。
    pause
)
