@echo off
setlocal

set "DIR=%~dp0"
set "PROJECT=%DIR%bhl.csproj"
set "BHL_DLL=%DIR%build\bhl\Release\net8.0\bhl.dll"

IF DEFINED BHL_REBUILD GOTO :BUILD
GOTO :RUN

:BUILD
set "VERBOSITY=--verbosity q -nologo"
IF "%BHL_SILENT%"=="0" set "VERBOSITY="
dotnet clean "%PROJECT%" %VERBOSITY% 1>&2
dotnet publish "%PROJECT%" %VERBOSITY% 1>&2 || GOTO :ERROR

:RUN
dotnet "%BHL_DLL%" %* || GOTO :ERROR
GOTO :EOF

:ERROR
echo Failed with error #%errorlevel% 1>&2
EXIT /b %errorlevel%
