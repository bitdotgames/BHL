@echo off
setlocal

set DIR=%~dp0
set BHL_DLL=%DIR%\build\Release\bhl\net8.0\bhl.dll
set VERS=%DIR%\src\vm\version.cs

IF DEFINED BHL_REBUILD GOTO :BUILD
IF NOT EXIST %BHL_DLL% GOTO :BUILD

FOR %%f in (%VERS%) do set VERS_T=%%~tf
FOR %%f in (%BHL_DLL%) do set BHL_DLL_T=%%~tf
IF %BHL_DLL_T:~0, 10% LSS %VERS_T:~0, 10% GOTO :BUILD

:BUILD
dotnet clean %DIR%\bhl.csproj
dotnet publish %DIR%\bhl.csproj || GOTO :ERROR

:RUN
dotnet %BHL_DLL% %* || GOTO :ERROR
GOTO :EOF

:ERROR
echo Failed with error #%errorlevel%
EXIT /b %errorlevel%
