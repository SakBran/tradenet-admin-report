@echo off
setlocal
set SCRIPT_DIR=%~dp0
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%deploy.ps1" %*
set DEPLOY_EXIT_CODE=%ERRORLEVEL%
if not "%DEPLOY_EXIT_CODE%"=="0" (
    echo.
    echo Deployment failed with exit code %DEPLOY_EXIT_CODE%.
    echo Check the error message above.
    pause
)
endlocal
exit /b %DEPLOY_EXIT_CODE%
