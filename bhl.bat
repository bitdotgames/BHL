@echo off
setlocal

set DIR=%~dp0
set BHL_DLL=%DIR%\build\Release\net8.0\bhl\net8.0\bhl.dll
set VERS=%DIR%\src\vm\version.cs

IF DEFINED BHL_REBUILD GOTO :BUILD
IF NOT EXIST %BHL_DLL% GOTO :BUILD

powershell -Command ^
    "$f1 = Get-Item '%BHL_DLL%'; $f2 = Get-Item '%VERS%';" ^
    "if ($f1.LastWriteTime -lt $f2.LastWriteTime) { exit 1 } else { exit 0 }"

IF errorlevel 1 GOTO :BUILD
GOTO :RUN

:BUILD
dotnet clean %DIR%\bhl.csproj
dotnet publish %DIR%\bhl.csproj || GOTO :ERROR

:RUN
dotnet %BHL_DLL% %* || GOTO :ERROR
GOTO :EOF

:ERROR
echo Failed with error #%errorlevel%
EXIT /b %errorlevel%
