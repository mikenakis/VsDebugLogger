@echo off

if not exist *.sln (
	echo This command is meant to be executed in a Visual Studio solution folder.
	exit 1
)

:: call :remove_directory_if_exists .\.nuget.packages
set tmpFile=%tmp%\%~n0-%RANDOM%.tmp
:: PEARL: Normally, the solution directory contains project directories, 
::        and each project directory contains a 'bin' and an 'obj' directory.
::        in this normal scenario, the command `dir /s/b bin` executed in the solution folder 
::        will list all `bin` directories.
::        However, if there is a `bin` directory under the solution folder, 
::        then the command `dir /s/b bin` will list not the directory, but the contents of that directory!
::        Luckily, we do not have any `bin` or `obj` directories in the solution folder, so we are all good.
dir /s/b | grep \\obj$ > %tmpFile% 2> nul
dir /s/b | grep \\bin$ >> %tmpFile% 2> nul
for /f "delims=" %%f in (%tmpFile%) do call :remove_directory "%%~f"
del %tmpFile%
goto :eof
	
:remove_directory_if_exists
	if exist "%~1\." call :remove_directory "%~1"
	exit /b

:remove_directory
	echo Recursively removing directory "%~1"
	rd /s/q "%~1"
	exit /b
