@echo off
setlocal

set "DIR=%~dp0"
set "PROJECT=%DIR%bhl.csproj"
set "BHL_DLL=%DIR%build\bhl\Release\net8.0\bhl.dll"
set "VERS=%DIR%src\vm\version.cs"

set "VERBOSITY="
IF DEFINED BHL_SILENT set "VERBOSITY=--verbosity q -nologo"

IF DEFINED BHL_REBUILD GOTO :BUILD
IF NOT EXIST "%BHL_DLL%" GOTO :BUILD

powershell -NoProfile -Command ^
    "$f1 = Get-Item '%BHL_DLL%'; $f2 = Get-Item '%VERS%';" ^
    "if ($f1.LastWriteTime -lt $f2.LastWriteTime) { exit 1 } else { exit 0 }"

IF errorlevel 1 GOTO :BUILD
GOTO :RUN

:BUILD
dotnet clean "%PROJECT%" %VERBOSITY% 1>&2
dotnet publish "%PROJECT%" %VERBOSITY% 1>&2 || GOTO :ERROR
if EXIST "%BHL_DLL%" (
    powershell -NoProfile -Command "(Get-Item '%BHL_DLL%').LastWriteTime = Get-Date"
)

:RUN
dotnet "%BHL_DLL%" %* || GOTO :ERROR
GOTO :EOF

:ERROR
echo Failed with error #%errorlevel% 1>&2
EXIT /b %errorlevel%
