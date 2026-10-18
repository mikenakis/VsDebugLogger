@echo off
:: execute as follows:
::     cmd /c $(ProjectDir)post-build-step.bat $(ProjectName) $(TargetDir)

	set projectName=%~1
	set src=%~2

	:: If we are running on the continuous build server, skip copying files.
	if not "%CI%"=="" goto :eof

	set dst=%USERPROFILE%\bin\%ProjectName%

	:: If the deployed instance is running, its files are locked, so close it.
	:: (Only the instance running from the destination directory; instances running from elsewhere are left alone.)
	:: First send it the "Quit" command via its named pipe; if that fails, kill it.
	powershell -NoProfile -ExecutionPolicy Bypass -Command "$exe = Join-Path $env:dst ($env:projectName + '.exe'); $processes = @(Get-Process -Name $env:projectName -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }); foreach( $process in $processes ) { Write-Host ('asking running instance ' + $exe + ' (pid ' + $process.Id + ') to quit'); try { $pipe = New-Object System.IO.Pipes.NamedPipeClientStream( '.', $env:projectName, [System.IO.Pipes.PipeDirection]::InOut ); $pipe.Connect( 2000 ); $writer = New-Object System.IO.StreamWriter( $pipe ); $writer.WriteLine( 'Quit' ); $writer.Flush(); $pipe.Dispose() } catch { Write-Host ('could not send the quit command: ' + $_.Exception.Message) }; if( -not $process.WaitForExit( 5000 ) ) { Write-Host 'it did not quit; killing it'; $process.Kill(); [void]$process.WaitForExit( 5000 ) } }"

	:: Copy changed files to the user's 'bin' directory
	echo copying changed files from '%src%' to '%dst%'
	set retries=0
:retry
	xcopy /i /d /y /s "%src%*" "%dst%\"
	if not %errorlevel%==4 exit /b %errorlevel%
	:: Error 4 means that a file is in use.
	:: Even after the process has exited, Windows may take a moment to release its files, so retry a few times.
	set /a retries+=1
	if %retries% geq 10 (
		echo post-build-step.bat : warning : could not copy files to '%dst%' because they are in use.
		exit /b 0
	)
	:: wait for 1 second; see https://stackoverflow.com/a/79268314/773113
	ping -n 2 -w 1000 localhost > nul
	goto :retry
