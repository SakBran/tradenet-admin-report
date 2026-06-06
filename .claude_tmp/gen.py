#!/usr/bin/env python3
import json, io

ROOT = "/Users/saobranaung/Code/Ministry of Commerce/tradenet-admin-report/tradenet-admin-report"
F = json.load(open(ROOT + "/.claude_tmp/audit_findings.json"))
DIMS = json.load(open(ROOT + "/.claude_tmp/audit_dim_summaries.json"))
DIMSUM = {d["dimension"]: d["summary"] for d in DIMS}
DATE = "2026-06-06"

# remediation instructions (optional; merged by finding id)
import os
REM = {}
_rem_path = ROOT + "/.claude_tmp/remediations.json"
if os.path.exists(_rem_path):
    for r in json.load(open(_rem_path)):
        REM[r["id"]] = r

SEV_RANK = {"Critical": 0, "High": 1, "Medium": 2, "Low": 3, "Info": 4}
SEV_BADGE = {"Critical": "🔴 CRITICAL", "High": "🟠 HIGH", "Medium": "🟡 MEDIUM", "Low": "🔵 LOW", "Info": "⚪ INFO"}
EFFORT_RANK = {"Trivial": 0, "Small": 1, "Medium": 2, "Large": 3}

DIM_ORDER = [
    "backend-security", "backend-architecture", "backend-code-quality", "backend-performance", "backend-testing",
    "frontend-security", "frontend-state-data", "frontend-performance", "frontend-uiux", "frontend-code-quality",
    "devops-deployment", "dependencies",
]
DIM_TITLE = {
    "backend-security": "Backend — Security",
    "backend-architecture": "Backend — Architecture & Data Access",
    "backend-code-quality": "Backend — Code Quality & Maintainability",
    "backend-performance": "Backend — Performance & Scalability",
    "backend-testing": "Backend — Testing",
    "frontend-security": "Frontend — Security",
    "frontend-state-data": "Frontend — State & Data Fetching",
    "frontend-performance": "Frontend — Performance",
    "frontend-uiux": "Frontend — UI / UX & Accessibility",
    "frontend-code-quality": "Frontend — Code Quality",
    "devops-deployment": "DevOps, Build & Deployment",
    "dependencies": "Dependencies & Supply Chain",
}

by_id = {f["id"]: f for f in F}

# ---- Phase assignment ----
P0_IDS = [
    # secrets / credentials emergency
    "prod-sa-creds-committed-in-git", "hardcoded-prod-sa-credentials-in-git",
    "hardcoded-prod-credentials-jwt-key", "weak-hardcoded-jwt-key",
    "secrets-raw-iconfiguration-no-options", "compose-hardcoded-sa-password",
    # credential leak / auth bypass
    "chatlist-unauth-user-password-dump", "passwords-rendered-in-userlist", "userlist-password-column",
    "plaintext-password-storage-and-auth", "plaintext-password-and-asparallel-auth",
    "allowanonymous-generic-crud-base", "inconsistent-authorization-across-controllers",
    "mass-assignment-baseapicontroller-put",
    "upload-unauth-path-traversal", "upload-controller-path-traversal-and-leak",
    "chatcontroller-unauth-data",
    # cheap hardening that closes the front door
    "jwt-issuer-audience-validation-disabled", "swagger-exposed-in-production", "swagger-served-in-production",
    "verbose-exception-leak-on-login", "credentials-logged-to-console",
]
P1_EXTRA = [
    "no-content-security-policy",
    "cors-allowcredentials-localhost-wildcard", "cors-allowcredentials-localhost-wildcards",
    "excel-export-idor", "excel-export-controller-authz-untested",
    "public-mqtt-broker-chat",
    "xlsx-sheetjs-cve", "xlsx-vulnerable-version", "lodash-prototype-pollution",
    "protobufjs-firebase-critical", "form-data-critical-transitive", "react-router-7-advisory",
    "backend-no-vuln-scan",
    "no-global-exception-middleware", "no-structured-logging-correlation", "no-monitoring-logging-alerting",
    "no-ci-security-scanning",
]
P0_SET, P1_EXTRA_SET = set(P0_IDS), set(P1_EXTRA)

def phase_of(f):
    i, s = f["id"], f["severity"]
    if i in P0_SET:
        return "P0"
    if s == "Critical":
        return "P0"
    if s == "High" or i in P1_EXTRA_SET:
        return "P1"
    if s == "Medium":
        return "P2"
    return "P3"

for f in F:
    f["phase"] = phase_of(f)

def anchor(i):
    return "f-" + i

def fref(i):
    f = by_id.get(i)
    if not f:
        return "`" + i + "`"
    return "[`%s`](#%s)" % (i, anchor(i))

# ---- counts ----
counts = {k: 0 for k in SEV_RANK}
for f in F:
    counts[f["severity"]] += 1

out = io.StringIO()
w = out.write

# ============================ HEADER ============================
w("# TradeNet Admin Report — Improvement Tips\n\n")
w("> **Security, Performance, Architecture, UI/UX, Code-Quality & DevOps review** of the Myanmar Ministry of Commerce *TradeNet Admin Report* platform (ASP.NET Core 8 backend + React 19 / Vite frontend).\n")
w("> This document records every confirmed weakness found during a full-codebase audit, then turns them into a prioritized, actionable remediation plan.\n\n")
w("**Audit date:** %s &nbsp;|&nbsp; **Scope:** `Backend/` (541 C# files), `Frontend/` (439 TS/TSX files), build & deployment, dependencies.\n\n" % DATE)
w("**Total confirmed findings: %d** &nbsp;—&nbsp; 🔴 %d Critical · 🟠 %d High · 🟡 %d Medium · 🔵 %d Low · ⚪ %d Info.\n\n"
  % (len(F), counts["Critical"], counts["High"], counts["Medium"], counts["Low"], counts["Info"]))
w("---\n\n")

# ============================ TOC ============================
w("## Table of Contents\n\n")
w("1. [Executive Summary](#executive-summary)\n")
w("2. [How to Read This Document](#how-to-read-this-document)\n")
w("3. [P0 — Fix Immediately (Security Emergency)](#p0--fix-immediately-security-emergency)\n")
w("4. [Prioritized Remediation Roadmap](#prioritized-remediation-roadmap)\n")
w("5. [Quick Wins](#quick-wins)\n")
w("6. [Detailed Findings by Area](#detailed-findings-by-area)\n")
w("7. [Implementation Task Checklist](#implementation-task-checklist)\n")
w("8. [Definition of Done / Verification](#definition-of-done--verification)\n")
w("9. [Appendix — Methodology](#appendix--methodology)\n\n")
w("---\n\n")

# ============================ EXECUTIVE SUMMARY ============================
w("## Executive Summary\n\n")
w("### Overall risk rating: 🔴 **CRITICAL**\n\n")
w("This is a **public-internet-facing government system** that handles trade, licence and permit data for the Ministry of Commerce. "
  "The audit found that while the *newer* reporting and Excel-export subsystems are genuinely well-engineered, the **authentication, "
  "secret-management and credential-handling layer is broken end-to-end**. Several issues are not theoretical — they are directly "
  "exploitable today by anyone who can reach the API or read the git repository.\n\n")
w("Plainly, for a non-technical stakeholder:\n\n")
w("1. **The keys to the production database are published in the source code.** The live database password (the all-powerful `sa` "
  "account) and the secret used to sign login tokens are written in plain text in a file that is committed to git history. Anyone who "
  "has ever cloned the repository — current or former staff, contractors, or anyone who obtained a copy — can connect directly to the "
  "production trade database over the internet and read, change or delete everything, and can also forge a valid login as any "
  "administrator. *Deleting the file is not enough; the credentials must be rotated.* (%s, %s, %s)\n\n"
  % (fref("prod-sa-creds-committed-in-git"), fref("weak-hardcoded-jwt-key"), fref("secrets-raw-iconfiguration-no-options")))
w("2. **Every user's password is stored in plain text and can be read by anyone on the network.** A single web address "
  "(`GET /api/ChatList`) returns the entire user table — usernames, plain-text passwords, and permission levels — **with no login "
  "required**. The admin User screen also displays a password column and can export it to Excel. This is a complete compromise of "
  "the login system. (%s, %s, %s)\n\n"
  % (fref("chatlist-unauth-user-password-dump"), fref("plaintext-password-storage-and-auth"), fref("passwords-rendered-in-userlist")))
w("3. **The \"locked\" doors can be walked through.** Login-token validation is weakened (issuer/audience checks are off), a generic "
  "base controller is marked *anonymous* and may expose the user table to unauthenticated callers, file upload requires no login and "
  "is vulnerable to path traversal, and the API documentation (Swagger) is open in production. (%s, %s, %s)\n\n"
  % (fref("allowanonymous-generic-crud-base"), fref("upload-unauth-path-traversal"), fref("swagger-exposed-in-production")))
w("4. **Below the security emergency sit real reliability and usability problems:** report pages can silently skip or duplicate rows "
  "when paging (a data-integrity issue for audit-grade reports), the entire frontend ships as one un-split bundle, the report grid "
  "cannot sort or search despite advertising it, there is no automated test/CI gate before deploying to production, and the production "
  "frontend container is broken. (%s, %s, %s, %s)\n\n"
  % (fref("offset-without-stable-order-by"), fref("no-route-code-splitting"), fref("no-table-sort-or-search"), fref("no-dotnet-ci-tests-not-run")))

w("### Severity breakdown\n\n")
w("| Severity | Count | Meaning |\n|---|---|---|\n")
w("| 🔴 Critical | %d | Exploitable now / secret exposure / data loss — fix immediately. |\n" % counts["Critical"])
w("| 🟠 High | %d | Serious risk or near-term failure — fix this sprint. |\n" % counts["High"])
w("| 🟡 Medium | %d | Should be fixed; meaningful risk or debt. |\n" % counts["Medium"])
w("| 🔵 Low | %d | Hardening / polish / hygiene. |\n" % counts["Low"])
w("| ⚪ Info | %d | Notes, non-issues confirmed, or context. |\n" % counts["Info"])
w("| **Total** | **%d** | |\n\n" % len(F))

w("### Findings by area\n\n")
w("| Area | Crit | High | Med | Low | Info | Total |\n|---|---|---|---|---|---|---|\n")
for dm in DIM_ORDER:
    items = [f for f in F if f["dimension"] == dm]
    c = {k: sum(1 for f in items if f["severity"] == k) for k in SEV_RANK}
    w("| %s | %d | %d | %d | %d | %d | %d |\n" % (
        DIM_TITLE[dm], c["Critical"], c["High"], c["Medium"], c["Low"], c["Info"], len(items)))
w("\n")
w("> **Note on the bright spots.** The audit also confirmed solid engineering worth preserving: the async Excel-export queue "
  "(`Backend/Service/ExcelExport`) uses the options pattern, structured logging, scoped `DbContext` access, atomic DB leasing and "
  "full `CancellationToken` propagation; the `_Fast` report paths use `AsNoTracking` + DTO projection and push paging into SQL; and "
  "the report stored-proc / Dynamic-LINQ paths are parameterized, so SQL injection is *not* present there. The remediation plan "
  "below is about bringing the *legacy* surface up to the standard the newer code already sets.\n\n")
w("---\n\n")

# ============================ LEGEND ============================
w("## How to Read This Document\n\n")
w("**Severity** reflects real-world impact *in this specific application* (verified against the actual code), not a generic CVSS score:\n\n")
w("- 🔴 **Critical** — exploitable now, secret exposure, or data loss.\n")
w("- 🟠 **High** — serious risk or likely near-term failure.\n")
w("- 🟡 **Medium** — meaningful risk or technical debt that should be scheduled.\n")
w("- 🔵 **Low** — hardening, polish, hygiene.\n")
w("- ⚪ **Info** — context, confirmed non-issues, or minor notes.\n\n")
w("**Effort** is a rough engineering estimate: **Trivial** (minutes) · **Small** (hours) · **Medium** (1–3 days) · **Large** (week+ / structural).\n\n")
w("Every finding has a stable **ID** (e.g. `chatlist-unauth-user-password-dump`). The roadmap and checklist reference these IDs, "
  "and each is a clickable link into the [Detailed Findings](#detailed-findings-by-area). Findings were produced by a multi-agent "
  "audit and each was **adversarially re-verified against the source**; verifier corrections are included inline where they refine "
  "severity or location.\n\n")
w("Each detailed finding ends with a **🛠 How to Fix** block: the strategy, an ordered list of concrete steps grounded in this "
  "codebase, a code patch where applicable, how to verify the fix, and pitfalls/rollback notes. These were generated by reading the "
  "actual cited source, so file paths, method names and attributes match the real code.\n\n")
w("---\n\n")

# ============================ P0 EMERGENCY ============================
w("## P0 — Fix Immediately (Security Emergency)\n\n")
w("> These items mean the system can be fully compromised today. Treat the leaked credentials as **already burned**. "
  "Recommended order:\n\n")

w("### Step 1 — Assume breach: rotate every secret now (hours)\n\n")
w("The production `sa` password (`Pr0fessi0nal@IM2022`, server `203.81.66.111,14330`) and the JWT signing key "
  "(`\"This is my supper secret key for jwt\"`) are in `Backend/appsettings.json`, which is tracked in git **and present in history** "
  "(commit `07d95d8`). They cannot be un-leaked.\n\n")
w("- [ ] **Rotate the SQL `sa` password immediately**, and create a **least-privilege application login** (not `sa`) for the app.\n")
w("- [ ] **Generate a new random 256-bit+ JWT signing key**; deploying it invalidates all existing tokens (acceptable — assume they are forged).\n")
w("- [ ] **Restrict the database server firewall** so `203.81.66.111,14330` is not reachable from the public internet.\n")
w("- [ ] **Move all secrets out of source**: `dotnet user-secrets` in dev, environment variables / a secret store in prod; keep only a "
  "non-secret `appsettings.json` + `appsettings.Example.json`. Adopt the options pattern the ExcelExport subsystem already uses.\n")
w("- [ ] **Purge the secrets from git history** (`git filter-repo` / BFG), `git rm --cached Backend/appsettings.json`, force-push, and "
  "confirm `.gitignore` now actually untracks it.\n")
w("- [ ] Also remove the hardcoded SA password from `docker-compose.dev.yml` (%s).\n\n" % fref("compose-hardcoded-sa-password"))
w("Findings: %s.\n\n" % ", ".join(fref(i) for i in [
    "prod-sa-creds-committed-in-git", "weak-hardcoded-jwt-key", "secrets-raw-iconfiguration-no-options", "compose-hardcoded-sa-password"]))

w("### Step 2 — Stop the credential leak (hours)\n\n")
w("- [ ] **Remove or `[Authorize]`-lock `GET /api/ChatList`** and never return the `User` entity directly — project to a DTO that "
  "excludes `Password` (%s).\n" % fref("chatlist-unauth-user-password-dump"))
w("- [ ] **Hash all passwords** with `PasswordHasher<T>` / bcrypt / Argon2; look up by name then verify the hash, never query by "
  "password; force a reset/rehash for existing rows (%s, %s).\n" % (fref("plaintext-password-storage-and-auth"), fref("plaintext-password-and-asparallel-auth")))
w("- [ ] **Strip the password field everywhere it is serialized**: add `[JsonIgnore]` / a DTO on the `User` read path, and remove "
  "`'password'` from `UserList` `displayData` so it cannot be shown or exported (%s, %s).\n\n"
  % (fref("passwords-rendered-in-userlist"), fref("userlist-password-column")))

w("### Step 3 — Make authentication secure-by-default (1–2 days)\n\n")
w("- [ ] Add a **global fallback authorization policy** (`RequireAuthenticatedUser`) in `Program.cs` so endpoints are protected unless "
  "they explicitly opt out (%s).\n" % fref("inconsistent-authorization-across-controllers"))
w("- [ ] **Remove `[AllowAnonymous]` from `BaseAPIController`** — in ASP.NET Core `[AllowAnonymous]` *wins* over `[Authorize]`, so the "
  "inherited attribute may already expose the entire `User` CRUD surface anonymously. **Verify at runtime and treat as Critical until "
  "disproven**, then replace the generic entity in/out with DTOs to stop mass-assignment (%s, %s).\n"
  % (fref("allowanonymous-generic-crud-base"), fref("mass-assignment-baseapicontroller-put")))
w("- [ ] **Add `[Authorize]` to `ChatController` and `UploadController`** (%s, %s).\n" % (fref("chatcontroller-unauth-data"), fref("upload-unauth-path-traversal")))
w("- [ ] **Harden file upload**: generate a server-side safe filename (GUID + validated extension), reject any path separators, "
  "validate content-type server-side (%s).\n" % fref("upload-controller-path-traversal-and-leak"))
w("- [ ] **Enable JWT issuer & audience validation** (`ValidateIssuer`/`ValidateAudience = true`) (%s).\n\n" % fref("jwt-issuer-audience-validation-disabled"))

w("### Step 4 — Close cheap front doors (hours)\n\n")
w("- [ ] **Disable Swagger UI / OpenAPI in production** (move behind the `IsDevelopment()` check or auth) (%s).\n" % fref("swagger-exposed-in-production"))
w("- [ ] **Stop returning raw exception text** from Login/Upload; return generic messages and log the detail server-side (%s).\n" % fref("verbose-exception-leak-on-login"))
w("- [ ] **Remove `console.log` of submitted credentials** on the Login/SignUp/PasswordReset pages (%s).\n\n" % fref("credentials-logged-to-console"))
w("---\n\n")

# ============================ ROADMAP ============================
w("## Prioritized Remediation Roadmap\n\n")
PHASE_BLURB = {
    "P0": ("P0 — Emergency (hours → a few days)",
           "Active or trivially-achievable compromise. Stop the bleeding before anything else. Detailed above."),
    "P1": ("P1 — High priority (this sprint)",
           "Data-integrity, exploitable-with-effort, supply-chain, observability and \"cannot ship safely\" items. Schedule immediately after P0."),
    "P2": ("P2 — Medium (this quarter)",
           "Meaningful risk and technical debt: performance, maintainability, test coverage, and the larger refactors that pay down the legacy surface."),
    "P3": ("P3 — Low / hardening (backlog)",
           "Polish, hygiene, accessibility, minor performance and developer-experience improvements."),
}
for ph in ["P0", "P1", "P2", "P3"]:
    title, blurb = PHASE_BLURB[ph]
    items = [f for f in F if f["phase"] == ph]
    items.sort(key=lambda f: (SEV_RANK[f["severity"]], EFFORT_RANK.get(f["effort"], 9), f["dimension"]))
    w("### %s\n\n" % title)
    w("%s **(%d items)**\n\n" % (blurb, len(items)))
    w("| Finding | Sev | Effort | Area |\n|---|---|---|---|\n")
    for f in items:
        w("| %s — %s | %s | %s | %s |\n" % (fref(f["id"]), f["title"], SEV_BADGE[f["severity"]].split()[1].title(), f["effort"], DIM_TITLE[f["dimension"]].split(" — ")[0]))
    w("\n")
w("---\n\n")

# ============================ QUICK WINS ============================
w("## Quick Wins\n\n")
w("High-value, low-effort (**Trivial**/**Small**) fixes — most can be done in the first day or two and several are part of P0:\n\n")
qw = [f for f in F if f["effort"] in ("Trivial", "Small") and f["severity"] in ("Critical", "High", "Medium")]
qw.sort(key=lambda f: (SEV_RANK[f["severity"]], EFFORT_RANK[f["effort"]]))
w("| Finding | Sev | Effort | Why it's a quick win |\n|---|---|---|---|\n")
for f in qw:
    why = f["recommendation"]
    why = why.split(". ")[0].strip()
    if len(why) > 130:
        why = why[:127] + "…"
    w("| %s | %s | %s | %s |\n" % (fref(f["id"]), SEV_BADGE[f["severity"]].split()[1].title(), f["effort"], why.replace("|", "/")))
w("\n---\n\n")

# ============================ DETAILED FINDINGS ============================
w("## Detailed Findings by Area\n\n")

def clean_code(s):
    return s.replace("```", "`")

for dm in DIM_ORDER:
    items = [f for f in F if f["dimension"] == dm]
    items.sort(key=lambda f: (SEV_RANK[f["severity"]], EFFORT_RANK.get(f["effort"], 9)))
    w("### %s\n\n" % DIM_TITLE[dm])
    summ = DIMSUM.get(dm, "")
    if summ:
        w("> %s\n\n" % summ.replace("\n", " "))
    if not items:
        w("_No material issues found._\n\n")
        continue
    for f in items:
        w('<a id="%s"></a>\n' % anchor(f["id"]))
        w("#### %s — %s\n\n" % (SEV_BADGE[f["severity"]], f["title"]))
        w("**ID:** `%s` &nbsp;·&nbsp; **Phase:** %s &nbsp;·&nbsp; **Category:** %s &nbsp;·&nbsp; **Effort:** %s &nbsp;·&nbsp; **Confidence:** %s\n\n"
          % (f["id"], f["phase"], f.get("category", "—"), f["effort"], f.get("confidence", "—")))
        locs = f.get("locations") or []
        if locs:
            w("**Location(s):**\n")
            for L in locs:
                w("- `%s`\n" % str(L).replace("`", ""))
            w("\n")
        w("**Problem.** %s\n\n" % f["problem"])
        w("**Impact.** %s\n\n" % f["impact"])
        w("**Recommendation.** %s\n\n" % f["recommendation"])
        ex = f.get("codeExample")
        if ex and ex.strip():
            w("**Example:**\n\n```\n%s\n```\n\n" % clean_code(ex.strip()))
        rem = REM.get(f["id"])
        if rem:
            est = (" _(est. %s)_" % rem["estimatedTime"]) if rem.get("estimatedTime") else ""
            w("**🛠 How to Fix%s**\n\n" % est)
            if rem.get("approach"):
                w("%s\n\n" % rem["approach"].strip())
            steps = rem.get("steps") or []
            if steps:
                for n, s in enumerate(steps, 1):
                    w("%d. %s\n" % (n, str(s).strip()))
                w("\n")
            patch = rem.get("patch")
            if patch and patch.strip():
                lang = rem.get("patchLang") or ""
                w("```%s\n%s\n```\n\n" % (lang, clean_code(patch.strip())))
            ver = rem.get("verification") or []
            if ver:
                w("**Verify:**\n")
                for v in ver:
                    w("- %s\n" % str(v).strip())
                w("\n")
            if rem.get("pitfalls") and rem["pitfalls"].strip():
                w("**Pitfalls / rollback.** %s\n\n" % rem["pitfalls"].strip())
        vn = f.get("verifierNotes")
        if vn and vn.strip():
            w("> 🔍 **Verifier note.** %s\n\n" % vn.strip().replace("\n", " "))
        w("\n")
    w("---\n\n")

# ============================ TASK CHECKLIST ============================
w("## Implementation Task Checklist\n\n")
w("Copy these into your tracker. Each task references its finding ID; check the detailed section for specifics.\n\n")
for ph in ["P0", "P1", "P2", "P3"]:
    title, _ = PHASE_BLURB[ph]
    items = [f for f in F if f["phase"] == ph]
    items.sort(key=lambda f: (SEV_RANK[f["severity"]], EFFORT_RANK.get(f["effort"], 9), f["dimension"]))
    w("### %s\n\n" % title)
    for f in items:
        w("- [ ] **(%s)** %s — _%s_ %s\n" % (f["severity"], f["title"], f["effort"], fref(f["id"])))
    w("\n")
w("---\n\n")

# ============================ DOD ============================
w("## Definition of Done / Verification\n\n")
w("How to confirm each phase is genuinely complete:\n\n")
w("### P0 (security emergency)\n")
w("- [ ] `git log -p` / secret-scanner (e.g. `gitleaks`) on full history shows **no** DB password or JWT key.\n")
w("- [ ] Old `sa` password and old JWT key **no longer authenticate** anywhere (rotated). App runs under a least-privilege SQL login.\n")
w("- [ ] `GET /api/ChatList`, `/api/User`, `/api/Chat`, `/api/Upload` all return **401** without a valid token; user-shaped responses contain **no** `password` field (verified by request capture).\n")
w("- [ ] Passwords in the DB are hashes, not plaintext; login still works after migration.\n")
w("- [ ] Swagger returns **404** in the production environment; error responses contain no stack traces or raw exception text.\n")
w("- [ ] JWTs signed with the *old* key, or with altered issuer/audience, are **rejected**.\n\n")
w("### P1 (high priority)\n")
w("- [ ] Paged reports return each row exactly once across pages on a dataset with duplicate timestamps (regression test for %s).\n" % fref("offset-without-stable-order-by"))
w("- [ ] `npm audit --audit-level=high` and `dotnet list package --vulnerable --include-transitive` are **green** (or risk-accepted in writing) and run in CI.\n")
w("- [ ] A root-level GitHub Actions workflow runs `dotnet test` and `npm ci && npm run build` on every PR and **blocks merge** on failure.\n")
w("- [ ] Frontend production image builds and serves the SPA (correct `dist/` copy, full install).\n")
w("- [ ] A strict CSP header is present; session token is no longer the only thing standing between an XSS and account takeover.\n")
w("- [ ] CORS no longer combines `AllowCredentials` with wildcard/localhost origins in production.\n\n")
w("### P2 / P3\n")
w("- [ ] Lighthouse/bundle analysis shows route-level code-splitting and a materially smaller initial bundle.\n")
w("- [ ] Operators can sort and search inside report grids; sidebar is searchable.\n")
w("- [ ] Global exception middleware + structured logging with correlation IDs cover the whole API; health checks respond.\n")
w("- [ ] ESLint actually runs and the `any`/dead-code debt is trending down; tests cover auth, aggregation and FX logic.\n\n")
w("---\n\n")

# ============================ APPENDIX ============================
w("## Appendix — Methodology\n\n")
w("This report was produced by a **multi-agent audit** of the full codebase, run as a deterministic workflow:\n\n")
w("- **12 specialist finder agents**, one per dimension: Backend Security, Architecture, Code Quality, Performance, Testing; "
  "Frontend Security, State & Data, Performance, UI/UX, Code Quality; DevOps & Deployment; Dependencies.\n")
w("- Each finding was then handed to an independent **adversarial verifier agent** instructed to *refute* it by reading the actual "
  "source. Findings that did not hold up were dropped; severities and locations were corrected (those corrections appear as 🔍 "
  "*Verifier note* entries). Of the raw findings, **%d survived verification**.\n" % len(F))
w("- Severities reflect impact **in this specific application and deployment context**, not generic CVSS. Where a generic CVE was "
  "*not* actually exploitable here (e.g. the SheetJS `xlsx` parse CVEs — `xlsx` is used only to *write* exports, never to parse "
  "untrusted uploads; and the Node-only axios proxy/SSRF advisories, since this is a browser SPA), that nuance is recorded rather "
  "than inflated.\n\n")
w("**Caveats / things to verify on the live system** (the audit read code, it did not run the deployed app):\n\n")
w("- Whether `[AllowAnonymous]` on `BaseAPIController` currently exposes `/api/User` anonymously (the ASP.NET Core precedence rule says "
  "it likely does — verify and treat as Critical until disproven).\n")
w("- Whether the database server is in fact reachable from the public internet on `203.81.66.111,14330` (inferred from config).\n")
w("- Exact production CORS/Swagger behaviour, which depends on the deployed `ASPNETCORE_ENVIRONMENT`.\n\n")
w("_Generated %s. Finding IDs are stable; cross-reference them when filing remediation tickets._\n" % DATE)

data = out.getvalue()
open(ROOT + "/ImprovementTips.md", "w").write(data)
print("Wrote ImprovementTips.md  (%d bytes, %d lines)" % (len(data), data.count("\n") + 1))
# sanity: phase distribution
from collections import Counter
print("Phase distribution:", dict(Counter(f["phase"] for f in F)))
print("Quick wins:", len([f for f in F if f["effort"] in ("Trivial","Small") and f["severity"] in ("Critical","High","Medium")]))
# verify all referenced ids exist
missing = [i for i in (P0_IDS + P1_EXTRA) if i not in by_id]
print("Missing referenced ids (should be []):", missing)
