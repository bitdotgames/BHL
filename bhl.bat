@echo off

set DIR=%~dp0
set SRC=%DIR%\bhl.cs
set EXE=%DIR%\bhlb.exe
set MONO_PATH=%DIR%\deps;%MONO_PATH%

FOR /F %%i IN ('DIR /B /O:D %SRC% %EXE%') DO SET NEWEST=%%i
IF NOT x%NEWEST:.exe=% == x%NEWEST% GOTO RUN

mcs %SRC% -debug -r:%DIR%\deps\Newtonsoft.Json.dll -r:%DIR%\deps\mono_opts.dll -out:%EXE% && mono --debug %EXE% %*
EXIT /b %errorlevel%

:RUN

IF DEFINED BHL_DEBUG (
mono --debug --debugger-agent=transport=dt_socket,server=y,address=127.0.0.1:55556 %EXE% %*
) ELSE (
mono --debug %EXE% %*
)

EXIT /b %errorlevel%
