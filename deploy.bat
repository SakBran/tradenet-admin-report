@echo off
setlocal
set SCRIPT_DIR=%~dp0

REM UAT frontend build config. Vite reads VITE_* from the environment at build time, and
REM Frontend/src/config.ts already falls back to these. This replaces the previous in-place
REM rewrite of config.ts, which dirtied the working tree and broke `git pull --ff-only`.
set VITE_BASE_URL=https://reportuatapi.myanmartradenet.com/api/
set VITE_IMAGE_URL=https://reportuatapi.myanmartradenet.com/Image/
set VITE_QR_URL=https://uatapi.ecomreg.gov.mm/QR/

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
