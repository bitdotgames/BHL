@echo off

set DIR=%~dp0
set SRC=%DIR%\bhl.cs
set EXE=%DIR%\bhlb.exe

FOR /F %%i IN ('DIR /B /O:D %SRC% %EXE%') DO SET NEWEST=%%i
IF NOT x%NEWEST:.exe=% == x%NEWEST% GOTO RUN

mcs %SRC% -debug -r:%DIR%\mono_opts.dll -out:%EXE% && mono --debug %EXE% %*
GOTO END

:RUN
mono --debug %EXE% %*

:END
