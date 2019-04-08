@echo off

set DIR=%~dp0
set SRC=%DIR%\bhl.cs
set EXE=%DIR%\bhlb.exe

mcs %SRC% -debug -r:%DIR%\mono_opts.dll -out:%EXE% && mono --debug %EXE% %*
