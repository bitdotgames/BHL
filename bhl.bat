@echo off

set DIR=%~dp0
set SRC=%DIR%\bhl.cs
set EXE=%DIR%\build\bhlb.exe
mkdir "%DIR%\build"
set MONO_PATH=%DIR%\build;%DIR%\deps;%MONO_PATH%

for %%f in (%SRC%) do set SRCDT=%%~tf
for %%f in (%EXE%) do set EXEDT=%%~tf
if %SRCDT:~0, 10% LSS %EXEDT:~0, 10% GOTO RUN

mcs %SRC% -debug -r:%DIR%\deps\Newtonsoft.Json.dll -r:%DIR%\deps\mono_opts.dll -out:%EXE% && mono --debug %EXE% %*
EXIT /b %errorlevel%

:RUN

IF DEFINED BHL_DEBUG (
mono --debug --debugger-agent=transport=dt_socket,server=y,address=127.0.0.1:55556 %EXE% %*
) ELSE (
mono --debug %EXE% %*
)

EXIT /b %errorlevel%
