@echo off
:: execute as follows: 
::     cmd /c $(ProjectDir)post-build-step.bat $(ProjectName) $(TargetDir)

set projectName=%~1
set src=%~2

:: If we are running on the continuous build server, skip copying files.
if not "%CI%"=="" goto :eof

:: Copy changed files to the user's 'bin' directory
set dst=%USERPROFILE%\bin\%ProjectName%
echo copying changed files from '%src%' to '%dst%'
xcopy /i /d /y /q /s "%src%*" "%dst%\"

:: Copy changed files to the 'bin' directory under the Dui Nuget Repository.
set dst=\\demcon.local\Data\Demcon Groep\Dutch united instruments\Software development\nuget\repository\bin\%ProjectName%
echo copying changed files from '%src%' to '%dst%'
xcopy /i /d /y /q /s "%src%*" "%dst%\"
