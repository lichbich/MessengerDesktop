@echo off
title Publishing Messenger Native as Single File Executable...
echo Publishing self-contained single executable (win-x64)...
cd /d "%~dp0MessengerNative"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0Release"
echo.
echo Build finished! Check the folder 'Release' for MessengerNative.exe.
pause
