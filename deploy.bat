@echo off
REM Manual one-command UAT deploy: build + publish + copy, with a graceful app_offline cycle
REM and a post-deploy health check (all handled inside deploy.ps1). The UAT frontend URLs are
REM set as VITE_* env-fallbacks inside deploy.ps1, so nothing needs to be set here.
REM
REM NOTE: the unattended Scheduled Task must NOT call this file (the `pause` below would hang it).
REM It calls tools\auto-deploy-watch.ps1, which invokes deploy.ps1 directly.
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
