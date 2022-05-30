@echo off

set DIR=%~dp0
set SRC=%DIR%\bhl.cs
set EXE=%DIR%\bhlb.exe
set PATH=deps;%PATH%
set MONO_PATH=%DIR%\deps;%MONO_PATH%

FOR /F %%i IN ('DIR /B /O:D %SRC% %EXE%') DO SET NEWEST=%%i
IF NOT x%NEWEST:.exe=% == x%NEWEST% GOTO RUN

mcs %SRC% -debug -r:%DIR%\deps\mono_opts.dll -out:%EXE% && mono --debug %EXE% %*
EXIT /b %errorlevel%

:RUN
mono --debug %EXE% %*
EXIT /b %errorlevel%
