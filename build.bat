@echo off
REM Double-click on a Windows PC with the .NET 8 SDK installed. Output: dist\ColorVisionStudio.exe
dotnet publish src\ColorVisionStudio.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
pause
