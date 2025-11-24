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
:retry
    xcopy /i /d /y /s "%src%*" "%dst%\"
    if %errorlevel%==4 (
			:: see https://stackoverflow.com/a/79268314/773113
			ping -n 2 -w 1000 localhost > null
	        goto :retry
    )
