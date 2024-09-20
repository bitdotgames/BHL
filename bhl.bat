@echo off

set DIR=%~dp0
set BHL_DLL=%DIR%\build\Debug\bhl\net7.0\bhl.dll

dotnet %BHL_DLL% %*

EXIT /b %errorlevel%
