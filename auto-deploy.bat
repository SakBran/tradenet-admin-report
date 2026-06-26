@echo off
REM Double-click to start the UAT auto-deploy watcher in this RDP session.
REM It checks GitHub 'main' every 5 seconds and runs the full deploy when it changes.
REM Leave this window open. You can disconnect RDP and it keeps running; it stops on sign-out
REM (just double-click again to restart). Press Ctrl+C in the window to stop it.
title TradeNet UAT Auto-Deploy
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\auto-deploy-watch.ps1" -RepoRoot "%CD%" -Loop
pause
