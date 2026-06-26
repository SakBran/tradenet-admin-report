# UAT Auto-Deploy Guide

## Architecture Overview

```
Your Mac
  └── cloudflared tunnel
        └── RDP → Build Server (dev_vm3)
                    ├── git checkout (H:\SakBran\Code\tradenet-report\tradenet-admin-report)
                    ├── .NET 8 SDK + Node.js
                    └── P:\WEBSITES\ (network share → UAT IIS server)
                              ├── tradenet-admin-backend\
                              └── tradenenet-admin-frontend\
```

- **Build Server** (`dev_vm3.myanmartradenet.com`) — performs git pull, dotnet build, npm build, and robocopy.
- **UAT Web Server** — hosts IIS. Cannot be logged into directly; managed only through the file share.
- **P:\WEBSITES** — network file share mapped on the Build Server that points to the UAT server's IIS site folders.
- **GitHub self-hosted runners cannot be used** — all automation runs inside your RDP session.

---

## How It Works

1. `auto-deploy.bat` starts `tools/auto-deploy-watch.ps1 -Loop` in the terminal.
2. Every **5 seconds**, the watcher runs `git fetch origin main`.
3. If `origin/main` is ahead of `HEAD`, it runs `git reset --hard origin/main` then calls `deploy.ps1 -NoGit`.
4. `deploy.ps1` does the full deploy:
   - Builds the backend (`dotnet publish`)
   - Drops `app_offline.htm` onto the backend share → UAT server gracefully unloads the app pool and releases `API.dll`
   - Robocopies the new build to `P:\WEBSITES\tradenet-admin-backend`
   - Removes `app_offline.htm` → app restarts on next request
   - Builds the frontend (`npm run build` with UAT `VITE_*` env vars)
   - Robocopies `dist/` to `P:\WEBSITES\tradenenet-admin-frontend`
   - Polls `https://reportuatapi.myanmartradenet.com/health` until it returns 200
5. If nothing changed, the watcher stays silent (no log noise).

---

## First-Time Setup (one-time, on Build Server)

1. **RDP into the Build Server**
   ```
   cloudflared access rdp --hostname dev_vm3.myanmartradenet.com --url rdp://localhost:33889
   ```
   Then open Microsoft Remote Desktop and connect to `localhost:33889`.

2. **Confirm the repo is checked out**
   ```
   H:\SakBran\Code\tradenet-report\tradenet-admin-report
   ```

3. **Confirm tools are installed**
   ```powershell
   git --version
   dotnet --version   # must be 8.x
   node -v
   ```

4. **Confirm the P: drive is mapped**
   ```powershell
   Test-Path P:\WEBSITES\tradenet-admin-backend
   Test-Path P:\WEBSITES\tradenenet-admin-frontend
   ```

5. **Run a manual deploy first** to verify everything works end-to-end:
   ```
   Double-click deploy.bat
   ```
   You should see dotnet build → robocopy → npm build → `DEPLOY OK - health check returned 200.`

---

## Starting the Auto-Deploy Watcher

```
Double-click auto-deploy.bat
```

Or from a PowerShell terminal in the repo root:
```powershell
.\tools\auto-deploy-watch.ps1 -Loop
```

You will see:
```
[2026-06-26 16:00:57] Watcher started (checking 'main' every 5 s). Leave this window open. Ctrl+C to stop.
```

**Leave this window open.** You can disconnect RDP and the watcher keeps running — it only stops when you sign out or the server reboots.

---

## Normal Deploy Flow (what you'll see in the terminal)

When a new commit is pushed to `main`:

```
[2026-06-26 16:01:01] Change detected on 'main': abc123 -> def456. Deploying.
[2026-06-26 16:01:01] git: HEAD is now at def456 your commit message
[2026-06-26 16:01:01] --- deploy.ps1 output starts ---
Root folder: H:\SakBran\Code\tradenet-report\tradenet-admin-report
Building Backend...
  (dotnet build output...)
Publishing Backend to: ...
Taking backend offline: P:\WEBSITES\tradenet-admin-backend\app_offline.htm
Copying backend files to: P:\WEBSITES\tradenet-admin-backend
Installing Frontend dependencies...
Building Frontend...
  (npm output...)
Copying frontend files to: P:\WEBSITES\tradenenet-admin-frontend
Health check (waiting for 200): https://reportuatapi.myanmartradenet.com/health
DEPLOY OK - health check returned 200.
Deployment complete.
[2026-06-26 16:06:40] --- deploy.ps1 output ends ---
[2026-06-26 16:06:40] Deploy finished OK for def456.
```

When nothing has changed, the terminal stays silent.

---

## Manual Deploy (without the watcher)

```
Double-click deploy.bat
```

This is the same deploy as the watcher triggers, but run on demand. Use this to test changes or redeploy without pushing a new commit.

---

## Log File

All watcher activity (change detection, deploy start/end, failures) is logged to:

```
C:\ProgramData\TradeNetDeploy\auto-deploy.log
```

To tail the log in PowerShell:
```powershell
Get-Content C:\ProgramData\TradeNetDeploy\auto-deploy.log -Wait -Tail 30
```

---

## After Server Reboot or Sign-Out

The watcher stops when the RDP session ends (sign-out) or the server reboots. To restart:

1. RDP back into the Build Server
2. Double-click `auto-deploy.bat` in the repo root

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `git fetch failed` | No internet / GitHub unreachable from Build Server | Check network; retry |
| `git reset --hard failed` | Local uncommitted changes in the repo | `git status` then `git checkout .` to discard |
| `Taking backend offline` hangs | `app_offline.htm` already exists and is locked | Delete it manually from `P:\WEBSITES\tradenet-admin-backend\` |
| Robocopy fails with exit code 8+ | `API.dll` still locked (ANCM didn't unload in time) | Stop the app pool in IIS Manager on the UAT server, then redeploy |
| `DEPLOY HEALTH CHECK FAILED` | App didn't start within 90 s | Open UAT URL manually; check IIS logs on UAT server |
| Watcher shows `Deploy finished OK` but site is old | Frontend Vite cache / browser cache | Hard-refresh (`Ctrl+Shift+R`) in the browser |
| `P:` drive not found | Session was restarted; drive not re-mapped | Re-map: `net use P: \\<uat-server>\WEBSITES` |

---

## Key Files

| File | Purpose |
|---|---|
| `auto-deploy.bat` | Double-click to start the watcher |
| `deploy.bat` | Manual one-shot deploy |
| `deploy.ps1` | Full deploy script (build + app_offline + copy + health check) |
| `tools/auto-deploy-watch.ps1` | Polling watcher that calls deploy.ps1 when main changes |
| `Backend/Program.cs` | Contains the anonymous `/health` endpoint |
