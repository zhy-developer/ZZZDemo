@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1
for %%I in ("%~dp0..\..\..") do set "PROJECT_ROOT=%%~fI"
set "OUTPUT_DIR=%PROJECT_ROOT%\Assets\Scripts\Net\Message"
set "TEMP_FILE=%TEMP%\GameProtocol-%RANDOM%-%RANDOM%.cs"
set "RESULT=1"

if not exist "Game.proto" (
    echo [ERROR] Game.proto was not found in the Tools folder.
    goto finish
)
if not exist "protogen.exe" (
    echo [ERROR] protogen.exe was not found in the Tools folder.
    goto finish
)
if not exist "protobuf-net.dll" (
    copy /y "..\lib\protobuf-net.dll" "protobuf-net.dll" >nul
    if errorlevel 1 (
        echo [ERROR] Could not copy protobuf-net.dll from the lib folder.
        goto finish
    )
)

protogen.exe -i:Game.proto "-o:%TEMP_FILE%" -p:fixCase
if errorlevel 1 (
    echo [ERROR] Generation failed. The destination file was not changed.
    goto finish
)
if not exist "%TEMP_FILE%" (
    echo [ERROR] The generator did not produce an output file.
    goto finish
)
if not exist "%OUTPUT_DIR%\" mkdir "%OUTPUT_DIR%"
if not exist "%OUTPUT_DIR%\" (
    echo [ERROR] Could not create the destination folder.
    goto finish
)
copy /y "%TEMP_FILE%" "%OUTPUT_DIR%\GameProtocol.cs" >nul
if errorlevel 1 (
    echo [ERROR] Could not copy GameProtocol.cs to the destination folder.
    goto finish
)
set "RESULT=0"
echo.
echo [SUCCESS] Generated:
echo "%OUTPUT_DIR%\GameProtocol.cs"

:finish
if exist "%TEMP_FILE%" del /q "%TEMP_FILE%"
popd
echo.
if /i not "%~1"=="--no-pause" pause
exit /b %RESULT%
