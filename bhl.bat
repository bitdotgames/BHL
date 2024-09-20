@echo off

set DIR=%~dp0
set BHL_DLL=%DIR%\build\Debug\bhl\net7.0\bhl.dll
set VERSION=%$DIR%\src\vm\version.cs

FOR %%f in (%VERSION%) do set VERSION_T=%%~tf
FOR %%f in (%BHL_DLL%) do set BHL_DLL_T=%%~tf
IF %VERSION_T:~0, 10% LSS %BHL_DLL_T:~0, 10% GOTO :RUN
IF EXIST %BHL_DLL% GOTO :RUN

dotnet publish %DIR%\bhl.csproj || GOTO :ERROR

:RUN
dotnet %BHL_DLL% %* || GOTO :ERROR
GOTO :EOF

:ERROR
echo Failed with error #%errorlevel%
EXIT /b %errorlevel%
