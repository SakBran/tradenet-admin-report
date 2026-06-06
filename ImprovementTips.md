# TradeNet Admin Report — Improvement Tips

> **Security, Performance, Architecture, UI/UX, Code-Quality & DevOps review** of the Myanmar Ministry of Commerce *TradeNet Admin Report* platform (ASP.NET Core 8 backend + React 19 / Vite frontend).
> This document records every confirmed weakness found during a full-codebase audit, then turns them into a prioritized, actionable remediation plan.

**Audit date:** 2026-06-06 &nbsp;|&nbsp; **Scope:** `Backend/` (541 C# files), `Frontend/` (439 TS/TSX files), build & deployment, dependencies.

**Total confirmed findings: 122** &nbsp;—&nbsp; 🔴 9 Critical · 🟠 14 High · 🟡 45 Medium · 🔵 47 Low · ⚪ 7 Info.

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [How to Read This Document](#how-to-read-this-document)
3. [P0 — Fix Immediately (Security Emergency)](#p0--fix-immediately-security-emergency)
4. [Prioritized Remediation Roadmap](#prioritized-remediation-roadmap)
5. [Quick Wins](#quick-wins)
6. [Detailed Findings by Area](#detailed-findings-by-area)
7. [Implementation Task Checklist](#implementation-task-checklist)
8. [Definition of Done / Verification](#definition-of-done--verification)
9. [Appendix — Methodology](#appendix--methodology)

---

## Executive Summary

### Overall risk rating: 🔴 **CRITICAL**

This is a **public-internet-facing government system** that handles trade, licence and permit data for the Ministry of Commerce. The audit found that while the *newer* reporting and Excel-export subsystems are genuinely well-engineered, the **authentication, secret-management and credential-handling layer is broken end-to-end**. Several issues are not theoretical — they are directly exploitable today by anyone who can reach the API or read the git repository.

Plainly, for a non-technical stakeholder:

1. **The keys to the production database are published in the source code.** The live database password (the all-powerful `sa` account) and the secret used to sign login tokens are written in plain text in a file that is committed to git history. Anyone who has ever cloned the repository — current or former staff, contractors, or anyone who obtained a copy — can connect directly to the production trade database over the internet and read, change or delete everything, and can also forge a valid login as any administrator. *Deleting the file is not enough; the credentials must be rotated.* ([`prod-sa-creds-committed-in-git`](#f-prod-sa-creds-committed-in-git), [`weak-hardcoded-jwt-key`](#f-weak-hardcoded-jwt-key), [`secrets-raw-iconfiguration-no-options`](#f-secrets-raw-iconfiguration-no-options))

2. **Every user's password is stored in plain text and can be read by anyone on the network.** A single web address (`GET /api/ChatList`) returns the entire user table — usernames, plain-text passwords, and permission levels — **with no login required**. The admin User screen also displays a password column and can export it to Excel. This is a complete compromise of the login system. ([`chatlist-unauth-user-password-dump`](#f-chatlist-unauth-user-password-dump), [`plaintext-password-storage-and-auth`](#f-plaintext-password-storage-and-auth), [`passwords-rendered-in-userlist`](#f-passwords-rendered-in-userlist))

3. **The "locked" doors can be walked through.** Login-token validation is weakened (issuer/audience checks are off), a generic base controller is marked *anonymous* and may expose the user table to unauthenticated callers, file upload requires no login and is vulnerable to path traversal, and the API documentation (Swagger) is open in production. ([`allowanonymous-generic-crud-base`](#f-allowanonymous-generic-crud-base), [`upload-unauth-path-traversal`](#f-upload-unauth-path-traversal), [`swagger-exposed-in-production`](#f-swagger-exposed-in-production))

4. **Below the security emergency sit real reliability and usability problems:** report pages can silently skip or duplicate rows when paging (a data-integrity issue for audit-grade reports), the entire frontend ships as one un-split bundle, the report grid cannot sort or search despite advertising it, there is no automated test/CI gate before deploying to production, and the production frontend container is broken. ([`offset-without-stable-order-by`](#f-offset-without-stable-order-by), [`no-route-code-splitting`](#f-no-route-code-splitting), [`no-table-sort-or-search`](#f-no-table-sort-or-search), [`no-dotnet-ci-tests-not-run`](#f-no-dotnet-ci-tests-not-run))

### Severity breakdown

| Severity | Count | Meaning |
|---|---|---|
| 🔴 Critical | 9 | Exploitable now / secret exposure / data loss — fix immediately. |
| 🟠 High | 14 | Serious risk or near-term failure — fix this sprint. |
| 🟡 Medium | 45 | Should be fixed; meaningful risk or debt. |
| 🔵 Low | 47 | Hardening / polish / hygiene. |
| ⚪ Info | 7 | Notes, non-issues confirmed, or context. |
| **Total** | **122** | |

### Findings by area

| Area | Crit | High | Med | Low | Info | Total |
|---|---|---|---|---|---|---|
| Backend — Security | 4 | 0 | 7 | 1 | 0 | 12 |
| Backend — Architecture & Data Access | 1 | 1 | 4 | 2 | 0 | 8 |
| Backend — Code Quality & Maintainability | 2 | 2 | 6 | 1 | 0 | 11 |
| Backend — Performance & Scalability | 0 | 1 | 3 | 2 | 1 | 7 |
| Backend — Testing | 0 | 2 | 5 | 3 | 0 | 10 |
| Frontend — Security | 1 | 1 | 3 | 3 | 3 | 11 |
| Frontend — State & Data Fetching | 0 | 1 | 3 | 4 | 2 | 10 |
| Frontend — Performance | 0 | 1 | 3 | 5 | 1 | 10 |
| Frontend — UI / UX & Accessibility | 0 | 2 | 1 | 7 | 0 | 10 |
| Frontend — Code Quality | 0 | 0 | 3 | 8 | 0 | 11 |
| DevOps, Build & Deployment | 1 | 1 | 6 | 3 | 0 | 11 |
| Dependencies & Supply Chain | 0 | 2 | 1 | 8 | 0 | 11 |

> **Note on the bright spots.** The audit also confirmed solid engineering worth preserving: the async Excel-export queue (`Backend/Service/ExcelExport`) uses the options pattern, structured logging, scoped `DbContext` access, atomic DB leasing and full `CancellationToken` propagation; the `_Fast` report paths use `AsNoTracking` + DTO projection and push paging into SQL; and the report stored-proc / Dynamic-LINQ paths are parameterized, so SQL injection is *not* present there. The remediation plan below is about bringing the *legacy* surface up to the standard the newer code already sets.

---

## How to Read This Document

**Severity** reflects real-world impact *in this specific application* (verified against the actual code), not a generic CVSS score:

- 🔴 **Critical** — exploitable now, secret exposure, or data loss.
- 🟠 **High** — serious risk or likely near-term failure.
- 🟡 **Medium** — meaningful risk or technical debt that should be scheduled.
- 🔵 **Low** — hardening, polish, hygiene.
- ⚪ **Info** — context, confirmed non-issues, or minor notes.

**Effort** is a rough engineering estimate: **Trivial** (minutes) · **Small** (hours) · **Medium** (1–3 days) · **Large** (week+ / structural).

Every finding has a stable **ID** (e.g. `chatlist-unauth-user-password-dump`). The roadmap and checklist reference these IDs, and each is a clickable link into the [Detailed Findings](#detailed-findings-by-area). Findings were produced by a multi-agent audit and each was **adversarially re-verified against the source**; verifier corrections are included inline where they refine severity or location.

---

## P0 — Fix Immediately (Security Emergency)

> These items mean the system can be fully compromised today. Treat the leaked credentials as **already burned**. Recommended order:

### Step 1 — Assume breach: rotate every secret now (hours)

The production `sa` password (`Pr0fessi0nal@IM2022`, server `203.81.66.111,14330`) and the JWT signing key (`"This is my supper secret key for jwt"`) are in `Backend/appsettings.json`, which is tracked in git **and present in history** (commit `07d95d8`). They cannot be un-leaked.

- [ ] **Rotate the SQL `sa` password immediately**, and create a **least-privilege application login** (not `sa`) for the app.
- [ ] **Generate a new random 256-bit+ JWT signing key**; deploying it invalidates all existing tokens (acceptable — assume they are forged).
- [ ] **Restrict the database server firewall** so `203.81.66.111,14330` is not reachable from the public internet.
- [ ] **Move all secrets out of source**: `dotnet user-secrets` in dev, environment variables / a secret store in prod; keep only a non-secret `appsettings.json` + `appsettings.Example.json`. Adopt the options pattern the ExcelExport subsystem already uses.
- [ ] **Purge the secrets from git history** (`git filter-repo` / BFG), `git rm --cached Backend/appsettings.json`, force-push, and confirm `.gitignore` now actually untracks it.
- [ ] Also remove the hardcoded SA password from `docker-compose.dev.yml` ([`compose-hardcoded-sa-password`](#f-compose-hardcoded-sa-password)).

Findings: [`prod-sa-creds-committed-in-git`](#f-prod-sa-creds-committed-in-git), [`weak-hardcoded-jwt-key`](#f-weak-hardcoded-jwt-key), [`secrets-raw-iconfiguration-no-options`](#f-secrets-raw-iconfiguration-no-options), [`compose-hardcoded-sa-password`](#f-compose-hardcoded-sa-password).

### Step 2 — Stop the credential leak (hours)

- [ ] **Remove or `[Authorize]`-lock `GET /api/ChatList`** and never return the `User` entity directly — project to a DTO that excludes `Password` ([`chatlist-unauth-user-password-dump`](#f-chatlist-unauth-user-password-dump)).
- [ ] **Hash all passwords** with `PasswordHasher<T>` / bcrypt / Argon2; look up by name then verify the hash, never query by password; force a reset/rehash for existing rows ([`plaintext-password-storage-and-auth`](#f-plaintext-password-storage-and-auth), [`plaintext-password-and-asparallel-auth`](#f-plaintext-password-and-asparallel-auth)).
- [ ] **Strip the password field everywhere it is serialized**: add `[JsonIgnore]` / a DTO on the `User` read path, and remove `'password'` from `UserList` `displayData` so it cannot be shown or exported ([`passwords-rendered-in-userlist`](#f-passwords-rendered-in-userlist), [`userlist-password-column`](#f-userlist-password-column)).

### Step 3 — Make authentication secure-by-default (1–2 days)

- [ ] Add a **global fallback authorization policy** (`RequireAuthenticatedUser`) in `Program.cs` so endpoints are protected unless they explicitly opt out ([`inconsistent-authorization-across-controllers`](#f-inconsistent-authorization-across-controllers)).
- [ ] **Remove `[AllowAnonymous]` from `BaseAPIController`** — in ASP.NET Core `[AllowAnonymous]` *wins* over `[Authorize]`, so the inherited attribute may already expose the entire `User` CRUD surface anonymously. **Verify at runtime and treat as Critical until disproven**, then replace the generic entity in/out with DTOs to stop mass-assignment ([`allowanonymous-generic-crud-base`](#f-allowanonymous-generic-crud-base), [`mass-assignment-baseapicontroller-put`](#f-mass-assignment-baseapicontroller-put)).
- [ ] **Add `[Authorize]` to `ChatController` and `UploadController`** ([`chatcontroller-unauth-data`](#f-chatcontroller-unauth-data), [`upload-unauth-path-traversal`](#f-upload-unauth-path-traversal)).
- [ ] **Harden file upload**: generate a server-side safe filename (GUID + validated extension), reject any path separators, validate content-type server-side ([`upload-controller-path-traversal-and-leak`](#f-upload-controller-path-traversal-and-leak)).
- [ ] **Enable JWT issuer & audience validation** (`ValidateIssuer`/`ValidateAudience = true`) ([`jwt-issuer-audience-validation-disabled`](#f-jwt-issuer-audience-validation-disabled)).

### Step 4 — Close cheap front doors (hours)

- [ ] **Disable Swagger UI / OpenAPI in production** (move behind the `IsDevelopment()` check or auth) ([`swagger-exposed-in-production`](#f-swagger-exposed-in-production)).
- [ ] **Stop returning raw exception text** from Login/Upload; return generic messages and log the detail server-side ([`verbose-exception-leak-on-login`](#f-verbose-exception-leak-on-login)).
- [ ] **Remove `console.log` of submitted credentials** on the Login/SignUp/PasswordReset pages ([`credentials-logged-to-console`](#f-credentials-logged-to-console)).

---

## Prioritized Remediation Roadmap

### P0 — Emergency (hours → a few days)

Active or trivially-achievable compromise. Stop the bleeding before anything else. Detailed above. **(22 items)**

| Finding | Sev | Effort | Area |
|---|---|---|---|
| [`hardcoded-prod-credentials-jwt-key`](#f-hardcoded-prod-credentials-jwt-key) — Production SQL 'sa' credentials and weak literal JWT signing key checked into appsettings.json | Critical | Small | Backend |
| [`chatlist-unauth-user-password-dump`](#f-chatlist-unauth-user-password-dump) — Unauthenticated endpoint dumps entire Users table including plaintext passwords | Critical | Small | Backend |
| [`weak-hardcoded-jwt-key`](#f-weak-hardcoded-jwt-key) — Weak, human-readable JWT signing key hardcoded in source and committed | Critical | Small | Backend |
| [`passwords-rendered-in-userlist`](#f-passwords-rendered-in-userlist) — Admin user list renders a cleartext 'password' column from the API | Critical | Small | Frontend |
| [`secrets-raw-iconfiguration-no-options`](#f-secrets-raw-iconfiguration-no-options) — Connection strings and JWT signing key read via raw IConfiguration from checked-in appsettings.json (no options/secret binding) | Critical | Medium | Backend |
| [`plaintext-password-and-asparallel-auth`](#f-plaintext-password-and-asparallel-auth) — Authentication compares plaintext passwords and misuses AsParallel() on an EF IQueryable | Critical | Medium | Backend |
| [`hardcoded-prod-sa-credentials-in-git`](#f-hardcoded-prod-sa-credentials-in-git) — Production SQL Server 'sa' credentials hardcoded in appsettings.json and committed to git | Critical | Medium | Backend |
| [`plaintext-password-storage-and-auth`](#f-plaintext-password-storage-and-auth) — Passwords stored and compared in plaintext | Critical | Medium | Backend |
| [`prod-sa-creds-committed-in-git`](#f-prod-sa-creds-committed-in-git) — Live production SQL 'sa' credentials and JWT key committed and still tracked in git | Critical | Medium | DevOps, Build & Deployment |
| [`userlist-password-column`](#f-userlist-password-column) — User list renders a 'password' column and can export it client-side | High | Trivial | Frontend |
| [`allowanonymous-generic-crud-base`](#f-allowanonymous-generic-crud-base) — Generic [AllowAnonymous] CRUD base controller exposes the User entity table over HTTP | High | Small | Backend |
| [`upload-controller-path-traversal-and-leak`](#f-upload-controller-path-traversal-and-leak) — UploadController is unauthenticated, vulnerable to path traversal, and leaks exception messages | High | Medium | Backend |
| [`inconsistent-authorization-across-controllers`](#f-inconsistent-authorization-across-controllers) — Inconsistent authorization: ChatController and UploadController fully anonymous; BaseAPIController marked [AllowAnonymous] exposing User table | High | Medium | Backend |
| [`swagger-exposed-in-production`](#f-swagger-exposed-in-production) — Swagger UI and OpenAPI spec served unconditionally in production | Medium | Trivial | Backend |
| [`verbose-exception-leak-on-login`](#f-verbose-exception-leak-on-login) — Login and upload endpoints return raw exception messages to clients | Medium | Trivial | Backend |
| [`swagger-served-in-production`](#f-swagger-served-in-production) — Swagger UI and OpenAPI spec served unconditionally in production | Medium | Trivial | DevOps, Build & Deployment |
| [`credentials-logged-to-console`](#f-credentials-logged-to-console) — Login/SignUp/PasswordReset log submitted form values (including password) to console | Medium | Trivial | Frontend |
| [`jwt-issuer-audience-validation-disabled`](#f-jwt-issuer-audience-validation-disabled) — JWT issuer and audience validation disabled | Medium | Small | Backend |
| [`chatcontroller-unauth-data`](#f-chatcontroller-unauth-data) — ChatController endpoints are unauthenticated | Medium | Small | Backend |
| [`compose-hardcoded-sa-password`](#f-compose-hardcoded-sa-password) — Hardcoded SQL SA password and personal host path baked into docker-compose | Medium | Small | DevOps, Build & Deployment |
| [`upload-unauth-path-traversal`](#f-upload-unauth-path-traversal) — Unauthenticated file upload with path traversal and ineffective validation | Medium | Medium | Backend |
| [`mass-assignment-baseapicontroller-put`](#f-mass-assignment-baseapicontroller-put) — Generic CRUD PUT/POST allows mass-assignment of any entity field including UserController | Medium | Medium | Backend |

### P1 — High priority (this sprint)

Data-integrity, exploitable-with-effort, supply-chain, observability and "cannot ship safely" items. Schedule immediately after P0. **(27 items)**

| Finding | Sev | Effort | Area |
|---|---|---|---|
| [`no-dotnet-ci-tests-not-run`](#f-no-dotnet-ci-tests-not-run) — No .NET CI pipeline — backend tests never run automatically | High | Small | Backend |
| [`axios-vulnerable-1-9-0`](#f-axios-vulnerable-1-9-0) — axios 1.9.0 (production HTTP client) carries 20+ advisories incl. SSRF, proxy-credential leak, prototype pollution | High | Small | Dependencies & Supply Chain |
| [`no-dependency-scanning`](#f-no-dependency-scanning) — No dependency scanning (Dependabot/Renovate/audit) anywhere; only leftover template CI on EOL Node | High | Small | Dependencies & Supply Chain |
| [`frontend-dockerfile-broken-build`](#f-frontend-dockerfile-broken-build) — Frontend production Dockerfile is broken: drops build tooling and copies wrong output dir | High | Small | DevOps, Build & Deployment |
| [`offset-without-stable-order-by`](#f-offset-without-stable-order-by) — OFFSET/FETCH pagination without a stable, unique ORDER BY drops and duplicates rows | High | Medium | Backend |
| [`auth-jwt-untested`](#f-auth-jwt-untested) — Authentication / JWT issuance (JWTManagerService, AuthController) completely untested | High | Medium | Backend |
| [`no-route-code-splitting`](#f-no-route-code-splitting) — No route-level code splitting: 159 report pages + 336 KB config in one bundle | High | Medium | Frontend |
| [`auth-token-in-localstorage`](#f-auth-token-in-localstorage) — JWT, user id and permission stored in localStorage and injected into every request | High | Medium | Frontend |
| [`no-table-sort-or-search`](#f-no-table-sort-or-search) — Report grid advertises sorting/searching but wires neither — no way to sort or find rows in large reports | High | Medium | Frontend |
| [`jwt-and-identity-in-localstorage`](#f-jwt-and-identity-in-localstorage) — JWT, user id and permission stored in localStorage (XSS token theft) | High | Large | Frontend |
| [`no-global-exception-middleware`](#f-no-global-exception-middleware) — No global exception-handling middleware; in production unhandled errors leak raw stack traces or return opaque 500s | Medium | Small | Backend |
| [`excel-export-idor`](#f-excel-export-idor) — Excel export jobs have no per-user ownership scoping (authenticated IDOR) | Medium | Small | Backend |
| [`react-router-7-advisory`](#f-react-router-7-advisory) — react-router-dom 7.6.2 carries router advisories (DoS / pre-render data spoofing) | Medium | Small | Dependencies & Supply Chain |
| [`cors-allowcredentials-localhost-wildcards`](#f-cors-allowcredentials-localhost-wildcards) — CORS allows credentials with localhost wildcards and a trailing-slash production origin | Medium | Small | DevOps, Build & Deployment |
| [`no-structured-logging-correlation`](#f-no-structured-logging-correlation) — Structured logging and correlation IDs exist only in the Excel workers; the entire report/API surface logs nothing | Medium | Medium | Backend |
| [`excel-export-controller-authz-untested`](#f-excel-export-controller-authz-untested) — ExcelExportController download/list/delete authorization (IDOR surface) untested | Medium | Medium | Backend |
| [`no-ci-security-scanning`](#f-no-ci-security-scanning) — No CI security scanning or backend CI; main branch excluded from tests | Medium | Medium | DevOps, Build & Deployment |
| [`no-monitoring-logging-alerting`](#f-no-monitoring-logging-alerting) — No structured logging, monitoring, or alerting; default console logging only | Medium | Medium | DevOps, Build & Deployment |
| [`no-content-security-policy`](#f-no-content-security-policy) — No Content-Security-Policy or hardening response headers | Medium | Medium | Frontend |
| [`public-mqtt-broker-chat`](#f-public-mqtt-broker-chat) — Chat uses a hardcoded public MQTT broker with a static shared topic | Medium | Medium | Frontend |
| [`form-data-critical-transitive`](#f-form-data-critical-transitive) — Critical form-data 4.0.2 (unsafe random boundary) pulled transitively by axios | Low | Trivial | Dependencies & Supply Chain |
| [`cors-allowcredentials-localhost-wildcard`](#f-cors-allowcredentials-localhost-wildcard) — Over-permissive CORS: AllowCredentials with localhost wildcard and AllowAnyHeader/Method | Low | Small | Backend |
| [`lodash-prototype-pollution`](#f-lodash-prototype-pollution) — lodash 4.17.21 (direct) vulnerable to prototype pollution and _.template code injection | Low | Small | Dependencies & Supply Chain |
| [`backend-no-vuln-scan`](#f-backend-no-vuln-scan) — Backend NuGet packages have no automated vulnerability scanning | Low | Small | Dependencies & Supply Chain |
| [`xlsx-vulnerable-version`](#f-xlsx-vulnerable-version) — xlsx (SheetJS) 0.18.5 from npm registry has unpatched known advisories | Low | Small | Frontend |
| [`xlsx-sheetjs-cve`](#f-xlsx-sheetjs-cve) — xlsx (SheetJS) pinned to vulnerable 0.18.5 with prototype-pollution + ReDoS CVEs, used in live export path | Low | Medium | Dependencies & Supply Chain |
| [`protobufjs-firebase-critical`](#f-protobufjs-firebase-critical) — Critical protobufjs 7.5.3 (code execution / DoS) pulled by firebase, in production path | Low | Medium | Dependencies & Supply Chain |

### P2 — Medium (this quarter)

Meaningful risk and technical debt: performance, maintainability, test coverage, and the larger refactors that pay down the legacy surface. **(26 items)**

| Finding | Sev | Effort | Area |
|---|---|---|---|
| [`dead-code-sp-to-linq-and-controllers`](#f-dead-code-sp-to-linq-and-controllers) — ~34 unreferenced StoredProcedureToLinq classes (explicit _old/_V2/_Seperated/Test dead code) | Medium | Small | Backend |
| [`apifilter-unguarded-datetime-convert`](#f-apifilter-unguarded-datetime-convert) — Dynamic filter does an unguarded Convert.ToDateTime on user input (FormatException -> 500) | Medium | Small | Backend |
| [`xlsx-static-import-basictable`](#f-xlsx-static-import-basictable) — Full xlsx statically imported into shared BasicTable; Excel is server-generated | Medium | Small | Frontend |
| [`bare-vite-build-config`](#f-bare-vite-build-config) — Vite config has no build/chunking/analysis settings | Medium | Small | Frontend |
| [`no-ratelimit-no-healthcheck-no-versioning`](#f-no-ratelimit-no-healthcheck-no-versioning) — No rate limiting, health checks, or API versioning across 158 report endpoints | Medium | Medium | Backend |
| [`cancellationtoken-not-propagated-sync-endpoints`](#f-cancellationtoken-not-propagated-sync-endpoints) — Synchronous paged report endpoints do not accept or propagate CancellationToken | Medium | Medium | Backend |
| [`swallowed-exceptions-and-leaked-messages`](#f-swallowed-exceptions-and-leaked-messages) — Broad catch blocks swallow context, leak raw exception messages, and there is almost no logging on the request path | Medium | Medium | Backend |
| [`inconsistent-api-result-shapes`](#f-inconsistent-api-result-shapes) — Inconsistent API result shapes and return types across controllers | Medium | Medium | Backend |
| [`bysection-cross-join-in-memory`](#f-bysection-cross-join-in-memory) — ImportLicenceBySectionReportController materializes all rows then nested-loops sections x currencies in memory | Medium | Medium | Backend |
| [`border-union-unstable-and-heavy`](#f-border-union-unstable-and-heavy) — Border report pages order a UNION ALL of two 11-table joins by a non-unique key | Medium | Medium | Backend |
| [`db-required-tests-no-skip-guards`](#f-db-required-tests-no-skip-guards) — Most tests hard-require a live SQL Server with no skip guards; suite is not CI-runnable as-is | Medium | Medium | Backend |
| [`report-aggregation-service-untested`](#f-report-aggregation-service-untested) — ReportAggregationService (the report-correctness engine) has zero tests | Medium | Medium | Backend |
| [`usd-fx-conversion-untested`](#f-usd-fx-conversion-untested) — ReportUsdConversionService FX logic is untested despite intricate currency rules | Medium | Medium | Backend |
| [`smoke-tests-assert-plumbing-not-correctness`](#f-smoke-tests-assert-plumbing-not-correctness) — Endpoint smoke tests assert empty/zero results, not report correctness | Medium | Medium | Backend |
| [`containers-run-as-root-no-multistage`](#f-containers-run-as-root-no-multistage) — All containers run as root; backend uses dev SDK image with no multi-stage or .dockerignore | Medium | Medium | DevOps, Build & Deployment |
| [`broken-eslint-gate`](#f-broken-eslint-gate) — ESLint 9 paired with legacy .eslintrc + unsupported --ext flag: lint gate runs nothing | Medium | Medium | Frontend |
| [`dead-template-code`](#f-dead-template-code) — Half the codebase is unused antd-multi-dashboard template demo code shipped to production | Medium | Medium | Frontend |
| [`unvirtualized-large-table`](#f-unvirtualized-large-table) — Unvirtualized HTML table exposes a 1000-row page size | Medium | Medium | Frontend |
| [`no-request-cancellation-race`](#f-no-request-cancellation-race) — Report grid fetch has no cancellation; rapid filter/page changes race and can show stale/wrong data | Medium | Medium | Frontend |
| [`swallowed-errors`](#f-swallowed-errors) — Backend errors are swallowed into generic strings; details discarded with empty catch blocks | Medium | Medium | Frontend |
| [`huge-unsearchable-report-nav`](#f-huge-unsearchable-report-nav) — 134-report sidebar has no search/filter, and clicks log to console | Medium | Medium | Frontend |
| [`report-controller-copypaste-sprawl`](#f-report-controller-copypaste-sprawl) — ~145 report controllers are near-identical copy-paste (157 duplicated TryCreateReportRequest blocks) | Medium | Large | Backend |
| [`count-and-groupby-rerun-per-page`](#f-count-and-groupby-rerun-per-page) — Full COUNT and full GROUP BY (plus FX query) re-executed on every page request | Medium | Large | Backend |
| [`legacy-notracking-and-client-eval`](#f-legacy-notracking-and-client-eval) — Legacy non-Fast LINQ loads full entities with per-row country subqueries and an in-memory UNION buffer | Medium | Large | Backend |
| [`any-and-anyobject-escape-hatch`](#f-any-and-anyobject-escape-hatch) — Pervasive `any` (66 sites) and an AnyObject catch-all type defeat strict mode | Medium | Large | Frontend |
| [`no-rtk-query-no-caching`](#f-no-rtk-query-no-caching) — All server data fetched manually with useState; no caching, dedup, or retry (Redux used only for theme) | Medium | Large | Frontend |

### P3 — Low / hardening (backlog)

Polish, hygiene, accessibility, minor performance and developer-experience improvements. **(47 items)**

| Finding | Sev | Effort | Area |
|---|---|---|---|
| [`moment-unused-direct-dep`](#f-moment-unused-direct-dep) — moment 2.30.1 is a direct dependency with zero source imports (app uses dayjs) | Low | Trivial | Dependencies & Supply Chain |
| [`duplicate-date-and-dead-moment`](#f-duplicate-date-and-dead-moment) — moment is a dependency but unused; dayjs date-format helpers duplicated across report pages | Low | Trivial | Frontend |
| [`moment-dead-dependency`](#f-moment-dead-dependency) — moment (~70 KB gzip) declared but never imported | Low | Trivial | Frontend |
| [`persistgate-provider-order`](#f-persistgate-provider-order) — PersistGate wraps Provider (inverted), bypassing persisted-store gating | Low | Trivial | Frontend |
| [`hardcoded-uat-qr-fallback`](#f-hardcoded-uat-qr-fallback) — Hardcoded UAT/localhost fallbacks in runtime config | Low | Trivial | Frontend |
| [`redundant-json-roundtrip`](#f-redundant-json-roundtrip) — Every API response is round-tripped through JSON.parse(JSON.stringify()) before use | Low | Trivial | Frontend |
| [`reset-and-logout-no-confirmation`](#f-reset-and-logout-no-confirmation) — Filter Reset wipes applied filters and Logout signs out with no confirmation | Low | Trivial | Frontend |
| [`no-dto-validation-attributes`](#f-no-dto-validation-attributes) — Report request DTOs rely on hand-rolled per-controller checks instead of validation attributes | Low | Small | Backend |
| [`connection-pool-unconfigured`](#f-connection-pool-unconfigured) — DbContext pool / SQL connection pool sizing unconfigured with MARS disabled | Low | Small | Backend |
| [`createcontroller-silent-fallback-masks-coverage`](#f-createcontroller-silent-fallback-masks-coverage) — Test harness silently falls back to a default constructor, masking dependency gaps | Low | Small | Backend |
| [`excel-hasher-and-filestore-untested`](#f-excel-hasher-and-filestore-untested) — ExcelExportHasher dedup logic and file-store path handling untested | Low | Small | Backend |
| [`query-translation-coverage-narrow`](#f-query-translation-coverage-narrow) — EF query-translation tests cover a small slice of 93 SP-to-LINQ queries | Low | Small | Backend |
| [`storybook-version-skew-dev`](#f-storybook-version-skew-dev) — Storybook devDependency version skew (mixed 8.6.x and 9.0.x packages) yielding High/Moderate audit hits | Low | Small | Dependencies & Supply Chain |
| [`no-healthchecks-no-resource-limits`](#f-no-healthchecks-no-resource-limits) — No container healthchecks or resource limits; restart policies inconsistent | Low | Small | DevOps, Build & Deployment |
| [`https-hsts-gaps`](#f-https-hsts-gaps) — HTTPS/HSTS configuration gaps: AllowedHosts wildcard, duplicate redirect, dev SSL trust | Low | Small | DevOps, Build & Deployment |
| [`usefetchdata-stale-closure-bug`](#f-usefetchdata-stale-closure-bug) — useFetchData has a stale-closure dependency bug, no auth, and no cleanup | Low | Small | Frontend |
| [`http-service-type-lying`](#f-http-service-type-lying) — BasicHttpServices casts every response to PaginationType and deep-clones via JSON round-trip | Low | Small | Frontend |
| [`console-logs-in-prod`](#f-console-logs-in-prod) — 51 console.log/error statements left in production source, including auth and 401 flows | Low | Small | Frontend |
| [`folder-literal-space-my-components`](#f-folder-literal-space-my-components) — Core shared components live under a folder with a literal space ("My Components") | Low | Small | Frontend |
| [`missing-hook-deps`](#f-missing-hook-deps) — Multiple useEffect/useCallback dependency-array bugs flagged by react-hooks | Low | Small | Frontend |
| [`oversized-public-assets`](#f-oversized-public-assets) — ~7 MB unoptimized demo images + 188 KB favicon in public/ | Low | Small | Frontend |
| [`lodash-full-namespace-import`](#f-lodash-full-namespace-import) — lodash imported as full namespace in 6 starter-template files | Low | Small | Frontend |
| [`unprotected-admin-routes`](#f-unprotected-admin-routes) — Timeline, Test and Certificate routes sit outside ProtectedRoute | Low | Small | Frontend |
| [`no-error-boundary`](#f-no-error-boundary) — No React error boundary anywhere; a single render throw blanks the entire admin app | Low | Small | Frontend |
| [`usefetchdata-stale-closure`](#f-usefetchdata-stale-closure) — useFetchData has a stale-closure bug, no cancellation, and bypasses auth/baseUrl | Low | Small | Frontend |
| [`unbounded-pagesize-client-export`](#f-unbounded-pagesize-client-export) — 1000-row page size plus DOM-scraping Excel fallback can load/serialize very large datasets on the main thread | Low | Small | Frontend |
| [`breadcrumb-collapses-current-page`](#f-breadcrumb-collapses-current-page) — Page breadcrumb shows '...' instead of the report name, using the raw URL segment | Low | Small | Frontend |
| [`signin-hardcoded-demo-creds`](#f-signin-hardcoded-demo-creds) — Sign-in form prefills hardcoded demo credentials and has weak validation/feedback | Low | Small | Frontend |
| [`client-excel-exports-current-page-only`](#f-client-excel-exports-current-page-only) — Client-side Excel export silently exports only the current page, not the full report | Low | Small | Frontend |
| [`table-a11y-and-sticky-header`](#f-table-a11y-and-sticky-header) — Report table lacks header scope/caption for screen readers and has no sticky header on tall/wide grids | Low | Small | Frontend |
| [`second-dbcontext-not-under-migrations`](#f-second-dbcontext-not-under-migrations) — TradeNetDbContext (228 entities, the real data) is a DB-first scaffold with no migrations or schema-version control | Low | Medium | Backend |
| [`reflection-getvalue-per-cell-excel`](#f-reflection-getvalue-per-cell-excel) — Excel writers call PropertyInfo.GetValue via reflection per cell on every export row | Low | Medium | Backend |
| [`heavy-single-use-deps`](#f-heavy-single-use-deps) — Heavy single-use deps (firebase, mqtt) inflate bundle and vulnerability surface | Low | Medium | Dependencies & Supply Chain |
| [`deploy-script-manual-robocopy-no-env-isolation`](#f-deploy-script-manual-robocopy-no-env-isolation) — Production deploy is a manual PowerShell robocopy to a Windows share with no env isolation or rollback | Low | Medium | DevOps, Build & Deployment |
| [`oversized-report-configs`](#f-oversized-report-configs) — reportConfigs.ts is a single 13,793-line file holding 134 report definitions | Low | Medium | Frontend |
| [`basictable-no-row-cell-memo`](#f-basictable-no-row-cell-memo) — BasicTable rows/cells not memoized; full re-render on every state change | Low | Medium | Frontend |
| [`client-side-auth-state-spoofable`](#f-client-side-auth-state-spoofable) — isAuthenticated derived from client-controlled localStorage with no token validation | Low | Medium | Frontend |
| [`dark-theme-dead-and-tables-hardcode-light`](#f-dark-theme-dead-and-tables-hardcode-light) — Dark theme is supported in code but has no UI toggle, and report tables hardcode light colors | Low | Medium | Frontend |
| [`data-access-static-helpers-tight-coupling`](#f-data-access-static-helpers-tight-coupling) — Data access is split across 83 static sp_* helpers and per-controller boilerplate with no injectable abstraction, hurting testability and consistency | Low | Large | Backend |
| [`no-i18n-and-no-antd-locale`](#f-no-i18n-and-no-antd-locale) — No internationalization and no AntD locale despite Myanmar/English requirement — all UI chrome is English-only | Low | Large | Frontend |
| [`scrolltotop-smooth-on-every-nav`](#f-scrolltotop-smooth-on-every-nav) — ScrollToTop runs smooth-scroll on every route change | Info | Trivial | Frontend |
| [`demo-creds-prefilled-login`](#f-demo-creds-prefilled-login) — Login form pre-filled with demo@email.com / demo123 | Info | Trivial | Frontend |
| [`dashboard-card-null-deref`](#f-dashboard-card-null-deref) — Dashboard cards crash on fetch error (null-deref) instead of showing the error | Info | Trivial | Frontend |
| [`persist-no-whitelist`](#f-persist-no-whitelist) — redux-persist configured with no whitelist/blacklist (persists entire root reducer) | Info | Trivial | Frontend |
| [`countrycache-vs-memorycache-duplication`](#f-countrycache-vs-memorycache-duplication) — Two overlapping country caches with different TTLs and a redundant per-request freshness check | Info | Small | Backend |
| [`fileupload-window-write-sink`](#f-fileupload-window-write-sink) — useFileUpload opens a new window and writes image markup built from user input | Info | Small | Frontend |
| [`sheetjs-cve-not-exploitable-note`](#f-sheetjs-cve-not-exploitable-note) — xlsx (SheetJS 0.18.5) is used only for export, not for parsing untrusted files | Info | Small | Frontend |

---

## Quick Wins

High-value, low-effort (**Trivial**/**Small**) fixes — most can be done in the first day or two and several are part of P0:

| Finding | Sev | Effort | Why it's a quick win |
|---|---|---|---|
| [`chatlist-unauth-user-password-dump`](#f-chatlist-unauth-user-password-dump) | Critical | Small | Remove this endpoint or, at minimum, add [Authorize] and never return the User entity directly |
| [`weak-hardcoded-jwt-key`](#f-weak-hardcoded-jwt-key) | Critical | Small | Generate a cryptographically random 256-bit+ key (e.g., 32+ random bytes base64-encoded), store it outside source control (env … |
| [`hardcoded-prod-credentials-jwt-key`](#f-hardcoded-prod-credentials-jwt-key) | Critical | Small | Rotate the sa password and JWT key immediately |
| [`passwords-rendered-in-userlist`](#f-passwords-rendered-in-userlist) | Critical | Small | Never return password (or hash) fields to the client |
| [`userlist-password-column`](#f-userlist-password-column) | High | Trivial | Remove 'password' from displayData (never display credentials) |
| [`allowanonymous-generic-crud-base`](#f-allowanonymous-generic-crud-base) | High | Small | Remove [AllowAnonymous] from BaseAPIController and make [Authorize] the secure default (apply at class level or via a fallback … |
| [`no-dotnet-ci-tests-not-run`](#f-no-dotnet-ci-tests-not-run) | High | Small | Add a root-level GitHub Actions workflow that runs `dotnet test Backend.Tests` on every PR |
| [`frontend-dockerfile-broken-build`](#f-frontend-dockerfile-broken-build) | High | Small | Use a full install for the build stage and copy the correct output dir, and convert to a real multi-stage build with an explici… |
| [`axios-vulnerable-1-9-0`](#f-axios-vulnerable-1-9-0) | High | Small | Upgrade axios to >=1.16.0 (latest 1.x) and re-pin: "axios": "^1.16.0" |
| [`no-dependency-scanning`](#f-no-dependency-scanning) | High | Small | Add a repo-root .github/dependabot.yml covering both the npm (Frontend) and nuget (Backend) ecosystems; add an `npm audit --aud… |
| [`swagger-exposed-in-production`](#f-swagger-exposed-in-production) | Medium | Trivial | Wrap UseSwagger/UseSwaggerUI inside if (app.Environment.IsDevelopment()), or require authentication/IP allow-listing to reach /… |
| [`verbose-exception-leak-on-login`](#f-verbose-exception-leak-on-login) | Medium | Trivial | Return a generic error (e.g., 'Authentication failed' / 'Upload failed') and log the exception server-side with a correlation id |
| [`credentials-logged-to-console`](#f-credentials-logged-to-console) | Medium | Trivial | Remove these console.log calls (and the onFinishFailed logs) |
| [`swagger-served-in-production`](#f-swagger-served-in-production) | Medium | Trivial | Wrap UseSwagger/UseSwaggerUI inside if (app.Environment.IsDevelopment()), or gate them behind authentication and a non-public r… |
| [`jwt-issuer-audience-validation-disabled`](#f-jwt-issuer-audience-validation-disabled) | Medium | Small | Set ValidateIssuer = true and ValidateAudience = true and ensure tokens are issued with matching iss/aud claims (the SecurityTo… |
| [`chatcontroller-unauth-data`](#f-chatcontroller-unauth-data) | Medium | Small | Add [Authorize] to ChatController and derive the participant identity from the authenticated principal rather than trusting cli… |
| [`excel-export-idor`](#f-excel-export-idor) | Medium | Small | If exports are genuinely meant to be shared among all admins, document and accept it; otherwise scope GetJobs/Download/Delete t… |
| [`dead-code-sp-to-linq-and-controllers`](#f-dead-code-sp-to-linq-and-controllers) | Medium | Small | Delete the clearly-dead _old/_V2/_Seperated/Test files now |
| [`apifilter-unguarded-datetime-convert`](#f-apifilter-unguarded-datetime-convert) | Medium | Small | Use DateTime.TryParse and return/ignore on failure (or surface a 400) |
| [`no-global-exception-middleware`](#f-no-global-exception-middleware) | Medium | Small | Add a global handler before routing: in prod app.UseExceptionHandler() with builder.Services.AddProblemDetails(), or a custom I… |
| [`xlsx-static-import-basictable`](#f-xlsx-static-import-basictable) | Medium | Small | Lazy-load via await import('xlsx') inside an async exportClientTableToExcel. |
| [`bare-vite-build-config`](#f-bare-vite-build-config) | Medium | Small | Add manualChunks for vendors and a bundle visualizer. |
| [`compose-hardcoded-sa-password`](#f-compose-hardcoded-sa-password) | Medium | Small | Move SA_PASSWORD and connection strings into a git-ignored .env consumed via ${SA_PASSWORD}; use a named Docker volume (e.g |
| [`cors-allowcredentials-localhost-wildcards`](#f-cors-allowcredentials-localhost-wildcards) | Medium | Small | Define separate CORS policies per environment: in production restrict origins to the exact HTTPS production domains only (no lo… |
| [`react-router-7-advisory`](#f-react-router-7-advisory) | Medium | Small | Upgrade react-router-dom to >7.11.0 (latest 7.x patched line) and re-run audit |

---

## Detailed Findings by Area

### Backend — Security

> The backend has several Critical, exploitable issues. The most severe is an unauthenticated endpoint (GET /api/ChatList) that dumps the entire Users table — including plaintext passwords and permission levels — to anyone on the network. Passwords are stored and compared in cleartext, secrets (production SQL Server `sa` credentials and a weak literal JWT signing key) are hardcoded in appsettings.json and committed to git history, and JWT issuer/audience validation is disabled. CORS is misconfigured (AllowCredentials with a localhost wildcard), Swagger is exposed unconditionally in production, the file-upload endpoint is unauthenticated and vulnerable to path traversal, and login errors leak exception text. The 158 report controllers and the Excel export controller are consistently `[Authorize]`-protected, and the report stored-proc/Dynamic-LINQ paths are parameterized or reflection-guarded, so injection there is mitigated — but the auth, secret-management, and credential-handling layer is broken end to end.

<a id="f-chatlist-unauth-user-password-dump"></a>
#### 🔴 CRITICAL — Unauthenticated endpoint dumps entire Users table including plaintext passwords

**ID:** `chatlist-unauth-user-password-dump` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Broken Authentication / Sensitive Data Exposure &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/ChatList.cs:15`
- `Backend/Controllers/ChatList.cs:25-41`
- `Backend/Model/User.cs:18-24`

**Problem.** ChatList is an [ApiController] with no [Authorize] attribute (class or method). Its GET action (GET /api/ChatList) executes `_context.Users.ToListAsync()` and returns the full list of User entities. The User model (API.Model.User) exposes Name, Password, and Permission as public string properties with no [JsonIgnore], so the response serializes every admin account's credentials verbatim. The result is also cached in memory for an hour, so it is served fast and repeatedly.

**Impact.** Any unauthenticated actor who can reach the API (and CORS/Swagger make discovery easy) can retrieve every admin username, plaintext password, and permission/role with a single GET. This is a complete compromise of the authentication system for a government trade-reporting platform: an attacker logs in as any admin, including high-privilege roles, and gains full access to all 158 reports and the underlying TradeNet production database. This is the single highest-impact issue in the codebase.

**Recommendation.** Remove this endpoint or, at minimum, add [Authorize] and never return the User entity directly. Return a projected DTO that excludes Password (and ideally only the fields the chat UI needs). Mark Password with [JsonIgnore] on the model as defense in depth. Re-scope: a 'chat user list' should not be the full credentials table.

**Example:**

```
// current
public async Task<IActionResult> GetChatList() {
    chatList = await _context.Users.ToListAsync(); // List<User> incl. Password
    return Ok(chatList);
}
// fixed
[Authorize]
public async Task<IActionResult> GetChatList() {
    var users = await _context.Users
        .Select(u => new { u.Id, u.Name }) // no Password/Permission
        .ToListAsync();
    return Ok(users);
}
```

> 🔍 **Verifier note.** Minor location nuance: the no-[Authorize] condition spans the whole class (line 13 [ApiController] through the action), and the plaintext-password fact that makes the leak directly exploitable lives in Service/JWTManagerService.cs:32 (not listed in the finding's locations). The cited locations are all accurate; JWTManagerService.cs:32 could be added as supporting evidence. The endpoint being named 'ChatList' and serving a chat-user list while actually returning the credentials table reinforces the over-scoping called out in the recommendation. Recommendation is sound (remove/Authorize the endpoint, return a Password-free DTO, add [JsonIgnore] as defense-in-depth) and should ideally also note that passwords must be hashed — currently any DB-table read anywhere is a credential leak.


<a id="f-weak-hardcoded-jwt-key"></a>
#### 🔴 CRITICAL — Weak, human-readable JWT signing key hardcoded in source and committed

**ID:** `weak-hardcoded-jwt-key` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Secret Exposure / Broken Authentication &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/appsettings.json:10`
- `Backend/Program.cs:74-90`
- `Backend/Service/JWTManagerService.cs:41-51`

**Problem.** JWT:Key is the literal string "This is my supper secret key for jwt" — a low-entropy, guessable English phrase used as the HMAC-SHA256 signing secret for all tokens. It is committed to git. Tokens are signed with HmacSha256 over this key (JWTManagerService:50,80,119).

**Impact.** An attacker who reads the repo (or guesses/brute-forces this dictionary-style phrase) can forge valid JWTs for any user and any role claim, bypassing authentication on every [Authorize] endpoint, including all 158 reports and Excel export. Combined with the ChatList leak above, even attackers without repo access can pivot. Forged admin tokens give full report/data access.

**Recommendation.** Generate a cryptographically random 256-bit+ key (e.g., 32+ random bytes base64-encoded), store it outside source control (env var/secrets manager), and rotate it (which invalidates all existing tokens — acceptable given they must be assumed forged). Purge the old key from git history.

> 🔍 **Verifier note.** Severity Critical is appropriate and locations are exact. Two minor caveats, neither weakening the finding: (1) The impact mentions "the ChatList leak above" as a secondary pivot path — that is an external cross-reference to another finding I did not validate here; it is not load-bearing for this finding's core claim. (2) The "158 reports" figure is approximate phrasing (controller count not exactly recounted), immaterial to severity. The lack of issuer/audience validation (Program.cs:83-84) actually makes forgery easier than the finding states, reinforcing Critical. Recommendation (random 256-bit key, move to secrets manager/env, rotate, purge git history) is sound; note appsettings.json ALSO leaks DB sa credentials on lines 26-27, which should be remediated in the same secret-purge effort.


<a id="f-hardcoded-prod-sa-credentials-in-git"></a>
#### 🔴 CRITICAL — Production SQL Server 'sa' credentials hardcoded in appsettings.json and committed to git

**ID:** `hardcoded-prod-sa-credentials-in-git` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Secret Exposure &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/appsettings.json:26-27`

**Problem.** Both the TemplateDB and TradeNetDBTest connection strings embed the production database server (203.81.66.111,14330) with `User ID=sa;Password=Pr0fessi0nal@IM2022`. appsettings.json is tracked in git (git ls-files confirms), and the password appears in historical commits 0453b61 and 07d95d8, so it is in the repository history, not just the working tree. `sa` is the SQL Server super-admin account.

**Impact.** Anyone with read access to the repo (or any clone/fork/backup, including the git history) obtains sysadmin-level credentials to the live Myanmar Ministry of Commerce trade database. This permits full read/write/drop of all trade, licence, permit, and account data, plus potential lateral movement via xp_cmdshell. Rotating the file alone is insufficient because the secret is in history.

**Recommendation.** Treat this credential as fully compromised: rotate the sa password immediately and stop using sa for the application (create a least-privilege login). Move all connection strings to environment variables / a secrets manager (user-secrets in dev, env vars or Azure/AWS secret store in prod). Remove appsettings.json from tracking, add it to .gitignore, and purge the secret from git history (git filter-repo / BFG). Restrict the DB server's network exposure.

> 🔍 **Verifier note.** Remediation in the finding is sound and complete (rotate sa, drop sa for a least-privilege login, move strings to env/user-secrets/secret manager, untrack + gitignore, purge history via filter-repo/BFG, restrict DB network exposure). Two additional observations worth surfacing to the team: (a) the same file (line 10) hardcodes a trivially-guessable JWT signing key, which is itself a separate auth-bypass-class issue and is also in history; (b) DefaultConnection uses localhost/Trusted_Connection and is only the fallback, so rotating the sa password will not break local dev that relies on Windows auth, but any environment relying on the TemplateDB string will break until reconfigured.


<a id="f-plaintext-password-storage-and-auth"></a>
#### 🔴 CRITICAL — Passwords stored and compared in plaintext

**ID:** `plaintext-password-storage-and-auth` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Cryptographic Failure / Broken Authentication &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Service/JWTManagerService.cs:32`
- `Backend/Model/User.cs:21`
- `Backend/Controllers/ChatList.cs:31`

**Problem.** Authentication compares the supplied password directly against the stored value: `Where(x => x.Name == users.Name && x.Password == users.Password)`. There is no hashing, salting, or verification function anywhere — User.Password is a plain string column. The same plaintext is also exposed via ChatList.

**Impact.** The credential database stores recoverable plaintext passwords. Any DB read (via the leaked sa credentials, a SQL issue, a backup, or ChatList) immediately yields usable login credentials, which users commonly reuse elsewhere. There is no protection against offline disclosure. The equality comparison is also non-constant-time, but that is minor next to plaintext storage.

**Recommendation.** Hash passwords with a memory-hard algorithm (ASP.NET Core's PasswordHasher<T> / Argon2 / bcrypt). On login, look up by Name then verify the hash; never query by password. Migrate existing rows (force reset or rehash-on-next-login). Remove Password from all serialized responses.

**Example:**

```
// current
_userService.Retrieve.Where(x => x.Name == users.Name && x.Password == users.Password)
// fixed
var u = await _userService.Retrieve.FirstOrDefaultAsync(x => x.Name == users.Name);
if (u == null || _hasher.VerifyHashedPassword(u, u.PasswordHash, users.Password) == PasswordVerificationResult.Failed)
    return null;
```

> 🔍 **Verifier note.** Severity Critical is correct. Two distinct critical issues are bundled here and both verify: (1) plaintext password storage + non-hashed login comparison, (2) the same plaintext exposed to anonymous callers through the unprotected ChatList endpoint (no [Authorize], no DTO projection, no global auth fallback). The recommendation (PasswordHasher<T>/Argon2/bcrypt, look up by Name then verify hash, never query by password, strip Password from serialized responses) is sound. I'd add: ChatList should also be removed or locked behind authorization and should project to a DTO that omits Password regardless of hashing. The non-constant-time comparison note is accurate but minor as stated.


<a id="f-swagger-exposed-in-production"></a>
#### 🟡 MEDIUM — Swagger UI and OpenAPI spec served unconditionally in production

**ID:** `swagger-exposed-in-production` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Information Disclosure &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Program.cs:109-115`
- `Backend/Program.cs:100-103`

**Problem.** app.UseSwagger() and app.UseSwaggerUI() are called outside the IsDevelopment() guard (which only wraps the developer exception page), so the full interactive API explorer at /swagger is available in every environment, including production.

**Impact.** Anyone reaching the server gets a complete, navigable map of all 158 report endpoints, request/response schemas, and parameter names — ideal reconnaissance for crafting attacks (e.g., enumerating report inputs, finding the unauthenticated ChatList/Upload endpoints). It is unnecessary exposure for a government system.

**Recommendation.** Wrap UseSwagger/UseSwaggerUI inside if (app.Environment.IsDevelopment()), or require authentication/IP allow-listing to reach /swagger in non-dev environments.

> 🔍 **Verifier note.** Locations are accurate: Program.cs:109-115 (UseSwagger/UseSwaggerUI) and Program.cs:100-103 (IsDevelopment guard wrapping only the dev exception page). Secondary observation worth flagging separately: the unauthenticated ChatList/UploadController endpoints are a distinct and likely higher-severity issue in their own right — Swagger merely makes them easier to discover. Severity left at Medium: defensible given the recon value combined with real unauthenticated endpoints, though a case could be made for Low since the bulk of endpoints remain behind JWT auth.


<a id="f-verbose-exception-leak-on-login"></a>
#### 🟡 MEDIUM — Login and upload endpoints return raw exception messages to clients

**ID:** `verbose-exception-leak-on-login` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Information Disclosure &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/AuthController.cs:39-42`
- `Backend/Controllers/AuthController.cs:39-42`
- `Backend/Controllers/UploadController.cs:94-99`

**Problem.** AuthController.Login catches Exception and returns StatusCode(500, ex.Message). On the unauthenticated /api/Auth login path, ex.Message can surface SQL/EF errors that reveal connection details, table/column names, or server information. UploadController similarly stuffs ex.Message into a response.

**Impact.** On the most-probed (anonymous) endpoint, an attacker can trigger and read backend error text — database structure, server identity, or stack-derived hints — aiding SQL/EF enumeration and confirming the live DB target. It leaks internal detail without any auth.

**Recommendation.** Return a generic error (e.g., 'Authentication failed' / 'Upload failed') and log the exception server-side with a correlation id. Never echo ex.Message to clients on public endpoints.

**Example:**

```
// current
catch (Exception ex) { return StatusCode(500, ex.Message); }
// fixed
catch (Exception ex) { _logger.LogError(ex, "Login failed"); return StatusCode(500, "An error occurred."); }
```

> 🔍 **Verifier note.** UploadController.Postupload should be removed from the finding — it does not leak (local dict discarded, empty 500 body). Note Postupload also has no [Authorize]/[AllowAnonymous] and Program.cs sets no FallbackPolicy, so it is effectively anonymous, but that is a separate access-control concern and still does not produce a message leak. The recommendation (generic error + server-side logging with correlation id) is valid for the AuthController case. Tangential observations not part of this finding: JWTManagerService compares passwords in plaintext (x.Password == users.Password) and the JWT signing key falls back to empty string if unconfigured.


<a id="f-jwt-issuer-audience-validation-disabled"></a>
#### 🟡 MEDIUM — JWT issuer and audience validation disabled

**ID:** `jwt-issuer-audience-validation-disabled` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Broken Authentication / JWT Misconfiguration &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Program.cs:81-90`

**Problem.** TokenValidationParameters set ValidateIssuer = false and ValidateAudience = false, even though ValidIssuer/ValidAudience are configured. Only the signature and lifetime are validated.

**Impact.** Tokens issued for any issuer/audience are accepted as long as they are signed with the (weak, exposed) key. If that key is ever reused across other systems/environments, a token minted elsewhere is honored here. It removes a defense-in-depth layer that would otherwise constrain where a valid token may be used; most impactful in combination with the weak signing key.

**Recommendation.** Set ValidateIssuer = true and ValidateAudience = true and ensure tokens are issued with matching iss/aud claims (the SecurityTokenDescriptor in JWTManagerService does not currently set Issuer/Audience — add them). This is cheap and meaningfully tightens token scope.


<a id="f-chatcontroller-unauth-data"></a>
#### 🟡 MEDIUM — ChatController endpoints are unauthenticated

**ID:** `chatcontroller-unauth-data` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Broken Access Control &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/ChatController.cs:14`
- `Backend/Controllers/ChatController.cs:21-53`

**Problem.** ChatController has no [Authorize] on the class or its GET (read messages by room) and POST (insert message) actions. The room key is fully client-supplied, so any anonymous caller can read messages for any room and post arbitrary messages/timestamps.

**Impact.** Unauthenticated reading of potentially sensitive internal chat content (room ids are guessable, e.g., user-AND-user) and unauthenticated writes (spam/injection of message rows). For an internal government admin tool, chat content may include operational details that should not be public.

**Recommendation.** Add [Authorize] to ChatController and derive the participant identity from the authenticated principal rather than trusting client-supplied room values; validate the caller is a participant of the room.

> 🔍 **Verifier note.** Location citation is accurate (line 14 = class; 21-53 covers both actions). The same unauthenticated exposure also applies to the sibling Backend/Controllers/Chat.cs and Backend/Controllers/ChatList.cs (no [Authorize]); if a follow-up fix is scoped, those should be remediated alongside ChatController. The recommendation is sound — add [Authorize] and derive participant identity from User.Claims rather than trusting the client-supplied room value.


<a id="f-excel-export-idor"></a>
#### 🟡 MEDIUM — Excel export jobs have no per-user ownership scoping (authenticated IDOR)

**ID:** `excel-export-idor` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Broken Access Control (IDOR) &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/ExcelExportController.cs:34-95`

**Problem.** GetJobs returns every export job (explicitly 'shared visibility'), and Download/Delete look jobs up by Guid id only, with no check that the job's RequestedByUserName matches the caller. The controller is [Authorize], so callers are authenticated, but any authenticated user can list, download, and delete any other user's generated report export.

**Impact.** An authenticated user can enumerate and download report exports they were not authorized to generate (which may cover date ranges / form types beyond their remit) and can delete other users' exports (data-availability impact). Lower severity than the anonymous issues because it requires a valid token, but it is still cross-user data access on a sensitive trade-reporting system.

**Recommendation.** If exports are genuinely meant to be shared among all admins, document and accept it; otherwise scope GetJobs/Download/Delete to jobs where RequestedByUserName == User.Identity.Name (or enforce a role check), and return 404/403 for jobs the caller does not own.

> 🔍 **Verifier note.** The "shared visibility" comment on GetJobs indicates the broad list may be a deliberate design choice for admins, which supports the finding's own recommendation ("if genuinely meant to be shared, document and accept it"). The stronger, harder-to-defend part of the issue is cross-user Download and especially Delete (file + DB row removal). If the team intends a shared exports drive, the minimal fix is to scope at least Delete (and ideally Download) to RequestedByUserName == User.Identity.Name or a role check. No role-based authorization exists in the backend at all, so a same-issue pattern likely affects other controllers, but that is out of scope for this specific finding. Severity Medium is appropriate; not adjusting.


<a id="f-upload-unauth-path-traversal"></a>
#### 🟡 MEDIUM — Unauthenticated file upload with path traversal and ineffective validation

**ID:** `upload-unauth-path-traversal` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Broken Access Control / Path Traversal / Unrestricted Upload &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/UploadController.cs:23 (no [Authorize]); :43,:83 (unsanitized client filename into Path.Combine); :104-147 (SaveAsWebpAsync writes via Directory.CreateDirectory + FileStream). Backend/Program.cs:22,168 (AddControllers with no fallback authz policy) and :169 (UseStaticFiles serves wwwroot).`
- `Backend/Controllers/UploadController.cs:22-23`
- `Backend/Controllers/UploadController.cs:42-87`
- `Backend/Controllers/UploadController.cs:104-147`

**Problem.** UploadController has no [Authorize]. Postupload takes the destination file name from the caller-controlled `filename` query/route value and passes it straight into `Path.Combine("wwwroot", "Image", filename)` (line 83) and into SaveAsWebpAsync, which calls Directory.CreateDirectory(Path.GetDirectoryName(filePath)) and writes there. `filename` is never sanitized, so values like `..\..\appsettings.json` or an absolute path escape the intended Image directory (Path.Combine treats a rooted segment as absolute). Extension/size checks are also weak: the validation branch returns early with the file unsaved only for disallowed types, but the actual save uses `filePath.Split(".")[0]` which mishandles names containing multiple dots, and the loop iterates httpRequest.Form.Files while always saving Form.Files[0].

**Impact.** An anonymous attacker can write arbitrary files into wwwroot (web-served) or, via traversal, elsewhere on disk the process can reach — enabling overwrite of config/static assets or planting content. Because uploads land under wwwroot which is served by app.UseStaticFiles(), an attacker-controlled file could be retrieved back. No authentication is required to do any of this.

**Recommendation.** Add [Authorize]. Never trust the client-supplied file name: generate a server-side name (e.g., Guid) and use Path.GetFileName() to strip directory components; validate the final resolved path stays under the intended root. Validate content (magic bytes) not just extension, enforce the size limit before reading, and write outside wwwroot if the files are not meant to be public.

**Example:**

```
// current
var filePath = Path.Combine("wwwroot", "Image", filename); // filename attacker-controlled
// fixed
var safeName = Guid.NewGuid().ToString("N") + ".webp";
var root = Path.Combine(_hostingEnvironment.ContentRootPath, "App_Data", "Image");
var filePath = Path.Combine(root, safeName);
```

> 🔍 **Verifier note.** Two factual corrections for the report: (a) the .webp extension is forced on every save path (Split(".")[0] + ".webp"), so config/executable overwrite as described is not possible — limit the impact to arbitrary .webp writes (asset overwrite, disk-fill DoS, publicly servable planted webp). (b) Confirmed there is no global authorization fallback policy, so the missing [Authorize] truly leaves the endpoint anonymous. The "loop iterates Form.Files but always saves Form.Files[0]" detail is accurate but is a functional bug, not a security amplifier. Severity adjusted High -> Medium primarily due to the forced-extension constraint; if the deployment's working directory/wwwroot or adjacent .webp assets are sensitive, it could be argued back up toward High.


<a id="f-mass-assignment-baseapicontroller-put"></a>
#### 🟡 MEDIUM — Generic CRUD PUT/POST allows mass-assignment of any entity field including UserController

**ID:** `mass-assignment-baseapicontroller-put` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Mass Assignment / Broken Access Control &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/BaseAPIController.cs:65-110`
- `Backend/Controllers/UserController.cs:15-19`

**Problem.** BaseAPIController<T>.PutData/PostData bind the entire entity T from the request body and write it directly (Entry(obj).State = Modified; SaveChangesAsync). UserController : BaseAPIController<User> exposes this for the User entity, whose fields include Password and Permission. Any authenticated user (the methods are [Authorize] but with no role restriction) can POST/PUT a User with an arbitrary Permission value or change another user's record by id.

**Impact.** An authenticated low-privilege user can escalate their own privileges by setting Permission, or set/reset another user's Password/Permission via PUT /api/User/{id}, because there is no per-field protection, no ownership check, and no role gate. This is a privilege-escalation and account-takeover vector within the authenticated surface.

**Recommendation.** Do not expose a generic CRUD controller over the User entity. Use explicit DTOs that exclude Password/Permission, add role-based authorization (e.g., [Authorize(Roles="Admin")]) for user management, and enforce ownership checks. Hash passwords on write (see plaintext finding).

> 🔍 **Verifier note.** Locations are accurate. Severity Medium is fair (arguably could be argued High for account takeover, but tempered by the fact that no role-based authorization is currently enforced anywhere, so escalation grants nothing extra at present). Two related facts strengthen the finding: (1) User.Permission is wired directly into ClaimTypes.Role in JWTManagerService.cs:47, so it is a true role field; (2) the same generic CRUD base exposes unprotected PUT/POST/DELETE for every entity, not just User — User is just the highest-impact instance. Also note the base class [AllowAnonymous] at BaseAPIController.cs:15 is overridden by the method-level [Authorize] attributes, so the finder's claim that the endpoints are authenticated-but-unrestricted is correct.


<a id="f-cors-allowcredentials-localhost-wildcard"></a>
#### 🔵 LOW — Over-permissive CORS: AllowCredentials with localhost wildcard and AllowAnyHeader/Method

**ID:** `cors-allowcredentials-localhost-wildcard` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** CORS Misconfiguration &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Program.cs:118-156`
- `Backend/Program.cs:119-156`

**Problem.** The CORS policy calls .AllowAnyMethod().AllowAnyHeader().AllowCredentials() while WithOrigins includes "http://localhost:*" (a wildcard-port localhost origin) plus numerous localhost variants and a trailing-slash production origin. Allowing credentials together with a wildcard/loopback origin is exactly the combination the CORS spec and browsers warn against. The repeated builder.WithMethods(...) calls are also redundant/confusing (the last AllowAnyMethod wins).

**Impact.** Any page the victim loads on any localhost port (e.g., a malicious local dev server, an Electron/Capacitor app, or malware-served content on the loopback) can make credentialed cross-origin requests to the API and read the responses on the admin's behalf — including pulling report data or, combined with the JWT in localStorage/cookies, acting as the admin. This widens the attack surface considerably for a sensitive internal tool.

**Recommendation.** Remove localhost wildcards from production. Maintain an explicit allow-list of real front-end origins only, drop the trailing-slash duplicates, and keep AllowCredentials only for those exact origins. Gate localhost origins behind IsDevelopment(). Replace the redundant WithMethods calls with a single explicit method list.

> 🔍 **Verifier note.** Confirmed parts: AllowCredentials + AllowAnyHeader/AllowAnyMethod over hardcoded localhost/capacitor origins (real), redundant WithMethods then AllowAnyMethod (real, cosmetic), trailing-slash origins (real but inert — browsers never send trailing-slash Origin). Refuted part: "http://localhost:*" is NOT a functioning wildcard in ASP.NET Core WithOrigins (exact string match, no wildcard-subdomain config, net8.0), so the "any localhost port" impact is invalid. Recommendation remains valid; severity lowered from High to Low. This is the only CORS configuration in the backend (grep found no other AddCors/SetIsOriginAllowed/WithOrigins).


---

### Backend — Architecture & Data Access

> The codebase is two architecturally distinct halves. The newer ExcelExport subsystem (Backend/Service/ExcelExport) is genuinely well-built: it uses the IOptions pattern, ILogger structured logging, scoped DbContext-per-scope in a BackgroundService, atomic DB leasing, and full CancellationToken propagation. The much larger legacy reporting surface (158 controllers in Backend/Controllers/Report, 83 static sp_* helpers, a 228-entity scaffolded TradeNetDbContext) lacks these disciplines. The cross-cutting gaps are the highest-impact: no global exception-handling middleware (only the dev exception page), no rate limiting, no health checks, no API versioning, and structured logging present in only 2 files of the whole app. The two-DbContext design is reasonable in principle (one code-first app DB, one DB-first read model) but only one is under migration control, and connection strings/JWT secrets are read via raw IConfiguration instead of bound/secret-managed options. A generic [AllowAnonymous] CRUD base controller over the User entity and the lack of CancellationToken on the synchronous paged endpoints round out the notable findings.

<a id="f-secrets-raw-iconfiguration-no-options"></a>
#### 🔴 CRITICAL — Connection strings and JWT signing key read via raw IConfiguration from checked-in appsettings.json (no options/secret binding)

**ID:** `secrets-raw-iconfiguration-no-options` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Configuration / Secrets &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Program.cs:64-67 (DbContexts), Backend/Program.cs:74-89 (JWT setup, ValidateIssuer/ValidateAudience=false at 83-84), Backend/appsettings.json:9-13 (JWT block; finding said 8-12), Backend/appsettings.json:24-28 (ConnectionStrings block; finding said 21-24). ExcelExport options pattern at Backend/Service/ExcelExport/ExcelExportServiceCollectionExtensions.cs:20.`
- `Backend/Program.cs:64-67`
- `Backend/Program.cs:74-89`
- `Backend/appsettings.json:8-12`
- `Backend/appsettings.json:21-24`

**Problem.** Program.cs binds both DbContexts and the JWT key directly from builder.Configuration with no options object, no environment-variable/Key Vault/user-secrets override, and no validation beyond a null check on JWT:Key (Program.cs:74-78). appsettings.json (committed to the repo) contains the production SQL Server 'sa' credentials for 203.81.66.111,14330 (TemplateDB and TradeNetDB) and a guessable literal JWT key 'This is my supper secret key for jwt' with issuer/audience validation disabled (ValidateIssuer/ValidateAudience=false, Program.cs:83-84). Note the ExcelExport subsystem already demonstrates the correct pattern (services.Configure<ExcelExportOptions>, ExcelExportServiceCollectionExtensions.cs:20) — connection/JWT config simply never adopted it.

**Impact.** Anyone with repo access has full sysadmin ('sa') credentials to the production trade database and can forge valid admin JWTs offline using the literal key (signature is the only check since issuer/audience are not validated). This is direct, exploitable government-data compromise. Even rotating the password is futile while it lives in version control.

**Recommendation.** Move all secrets out of appsettings.json into environment variables / a secret store and load via the options pattern (e.g. AddOptions<JwtOptions>().Bind(...).ValidateOnStart()). Use a least-privilege SQL login, not 'sa'. Rotate the leaked password and JWT key immediately and purge them from git history. Set ValidateIssuer/ValidateAudience=true with a strong (>=256-bit) random key.

> 🔍 **Verifier note.** Citation line numbers for appsettings.json are slightly off (off-by-one to off-by-a-few) but point at the right blocks; corrected above. Severity Critical is appropriate: committed 'sa' production credentials + an in-repo, low-entropy JWT signing key with issuer/audience validation disabled = direct offline admin-token forgery and full DB compromise. Additional supporting context not in the finding: Backend/appsettings.json is listed in .gitignore yet remains git-tracked, so the secrets persist in history and require a history purge + rotation, reinforcing the recommendation. CORS also includes a broad WithMethods("*") and many origins (out of scope here but adjacent). No change to severity.


<a id="f-allowanonymous-generic-crud-base"></a>
#### 🟠 HIGH — Generic [AllowAnonymous] CRUD base controller exposes the User entity table over HTTP

**ID:** `allowanonymous-generic-crud-base` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Authorization / Separation of concerns &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/BaseAPIController.cs:13-25`
- `Backend/Controllers/BaseAPIController.cs:91-126`
- `Backend/Controllers/UserController.cs:15-19`

**Problem.** BaseAPIController<T> is annotated [AllowAnonymous] at the class level (line 15) and provides full unparameterized CRUD (Get list, Get by id, Put, Post, PostDataList, Delete) directly against context.Set<T>(). UserController : BaseAPIController<User> (UserController.cs:15) inherits all of it and exposes /api/User. The individual actions carry [Authorize], which at runtime overrides the class-level [AllowAnonymous], but relying on that override ordering for an anonymous-by-default base class that wraps the credentials table is fragile: any future subclass or any action added without [Authorize] is silently public. PostData/PutData also accept the full entity T with no DTO/projection, enabling over-posting of fields like password hashes or roles.

**Impact.** A single missed [Authorize] (easy when the default is AllowAnonymous) exposes read/write/delete of the User table to anonymous callers. The over-posting surface lets an authenticated low-privilege user potentially escalate by setting fields the API never intended to be client-settable.

**Recommendation.** Remove [AllowAnonymous] from BaseAPIController and make [Authorize] the secure default (apply at class level or via a fallback authorization policy in Program.cs). Replace the generic entity in/out with explicit DTOs to stop over-posting. If the generic CRUD base is not actually needed for User, delete it.

> 🔍 **Verifier note.** Correction to the finding's rationale: [AllowAnonymous] wins over [Authorize] in ASP.NET Core regardless of placement, so the inherited base-class [AllowAnonymous] most likely renders the /api/User Get/Get-by-id/Put/Post/PostDataList/Delete endpoints anonymously reachable right now (read/write/delete of the credentials+permission table), not merely "fragile for future subclasses." UserController also carries its own [Authorize] at line 13 (finding missed this), but that does not change the outcome — AllowAnonymous still short-circuits. Recommendation (remove [AllowAnonymous], make [Authorize] the default / add a fallback policy, replace generic T with DTOs) is correct and should be treated as urgent. Severity High is defensible; given likely-live anonymous access to a credentials table with write/delete, Critical is also reasonable — I leave it at High pending a runtime confirmation but flag the upgrade case.


<a id="f-no-global-exception-middleware"></a>
#### 🟡 MEDIUM — No global exception-handling middleware; in production unhandled errors leak raw stack traces or return opaque 500s

**ID:** `no-global-exception-middleware` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Reliability / Error handling &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Program.cs:100-107 (only UseDeveloperExceptionPage in Dev / UseHsts otherwise; no global handler); Backend/Controllers/Report/AccountSummaryReportController.cs:70-76,160-164 (the only report-controller try/catch, narrow to missing _pagination SqlException)`
- `Backend/Program.cs:99-107`
- `Backend/Controllers/Report/AccountSummaryReportController.cs:57-77`

**Problem.** The pipeline only registers app.UseDeveloperExceptionPage() in Development and app.UseHsts() otherwise (Program.cs:100-107). There is no app.UseExceptionHandler(...), no IExceptionHandler, and no AddProblemDetails(). A grep across the whole backend shows zero UseExceptionHandler/IExceptionHandler/AddProblemDetails registrations. Only 5 of 158 report controllers contain any try/catch, and those that do (e.g. AccountSummaryReportController.cs:70) catch only a specific SqlException for a missing _pagination proc. Any other exception (DB timeout against the remote 203.81.66.111 server, null reference in an aggregation, a SkiaSharp failure) propagates unhandled.

**Impact.** In production any unhandled exception returns the framework's default 500 with no consistent error contract for the React client, and there is no central place that logs the failure with a correlation id — operators are blind. If the environment is ever misconfigured to Development (a real risk given Swagger is already served in prod), full stack traces and SQL details are returned to callers.

**Recommendation.** Add a global handler before routing: in prod app.UseExceptionHandler() with builder.Services.AddProblemDetails(), or a custom IExceptionHandler that logs with a correlation id and returns a sanitized ProblemDetails. Keep UseDeveloperExceptionPage strictly for Development. Remove the per-controller SqlException catch in favor of a typed exception the handler can map.

> 🔍 **Verifier note.** Corrections: (1) Program.cs range is 100-107, not 99-107. (2) '5 of 158 report controllers' should read '5 controllers total in the backend, but only 1 of 158 report controllers (AccountSummaryReportController)'; the other 4 are Auth/Base/ExcelExport/Upload controllers, and report controllers do not inherit BaseAPIController so its DbUpdateConcurrencyException catch does not apply to them. (3) The 'leak raw stack traces' impact is conditional on the Development-misconfiguration path; under a correctly configured Production environment, the default behavior is an opaque empty 500, not a stack-trace leak — this is why I downgraded to Medium. The observability gap (no central logging / no correlation id / inconsistent error contract for the React client) is unconditionally true and is the real substance of the finding.


<a id="f-no-ratelimit-no-healthcheck-no-versioning"></a>
#### 🟡 MEDIUM — No rate limiting, health checks, or API versioning across 158 report endpoints

**ID:** `no-ratelimit-no-healthcheck-no-versioning` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Operability / Hardening &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Program.cs:22-95`
- `Backend/Program.cs:158-170`

**Problem.** Program.cs registers controllers, memory cache, Swagger, two DbContexts, JWT and the Excel queue, but a full grep finds no AddRateLimiter/UseRateLimiter, no AddHealthChecks/MapHealthChecks, and no AddApiVersioning. The 158 report endpoints each run heavy paginated SQL against a remote DB (per-call SetCommandTimeout(120) in only 3 places, e.g. sp_HSCodeReport.StoredProcedure.cs:45) and some build full unbounded result sets in-memory before paging (ImportLicenceBySectionReportController.cs:101-125 materializes the whole join with .ToList() then groups in app memory).

**Impact.** Without rate limiting, a single authenticated user (or a leaked token) can issue many concurrent expensive report queries and exhaust the DB connection pool / remote SQL Server, a denial-of-service against a government reporting system. Without health checks, load balancers and ops have no liveness/readiness signal (e.g. DB unreachable) and route traffic to a dead instance. Without versioning, any breaking change to a report's response shape immediately breaks the React client.

**Recommendation.** Add ASP.NET Core rate limiting (a per-user fixed/sliding window on the report endpoints) and MapHealthChecks("/health") with AddDbContextCheck for both contexts. Introduce Asp.Versioning for the /api surface. Cap in-memory materialization in controllers like ImportLicenceBySectionReport by pushing the aggregation into SQL.

> 🔍 **Verifier note.** Two mitigations the finding does not mention but that do not invalidate it: (1) ImportLicenceBySectionReportController wraps BuildRowsAsync in a 5-minute IMemoryCache (lines 32, 64-68) keyed on all filter params — this blunts repeated-identical-query abuse but not distinct-parameter flooding, so the DoS argument still stands. (2) DbContexts use AddDbContextPool, which bounds context object allocation but not SQL Server CPU/IO. Severity Medium is appropriate and I am leaving it: these are defense-in-depth / operability gaps behind [Authorize] (not an unauthenticated RCE/data-exposure issue), so not High; but they are real gaps on a production government reporting API (no readiness probe, no throttle, no versioning contract for the React client), so not Info/Low. Recommendation (add rate limiting, MapHealthChecks + AddDbContextCheck for both contexts, Asp.Versioning, push the ImportLicenceBySection aggregation into SQL) is sound.


<a id="f-cancellationtoken-not-propagated-sync-endpoints"></a>
#### 🟡 MEDIUM — Synchronous paged report endpoints do not accept or propagate CancellationToken

**ID:** `cancellationtoken-not-propagated-sync-endpoints` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Reliability / Resource management &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/Report/AccountSummaryReportController.cs:42-77`
- `Backend/Controllers/Report/ImportLicenceBySectionReportController.cs:42-97`
- `Backend/StoredProcedureToLinq/sp_HSCodeReport.StoredProcedure.cs:47-51`

**Problem.** The CancellationToken is plumbed only through the Excel streaming path (WriteRowsAsync, e.g. AccountSummaryReportController.cs:101-116). The primary JSON Post(...) actions take no CancellationToken parameter and call sp_*.ExecuteAsync / ToListAsync with no token (sp_HSCodeReport.StoredProcedure.cs:51 calls .ToListAsync() with no argument; ImportLicenceBySectionReportController.cs:126 awaits ToListAsync with no token). When a client aborts a slow report request, the underlying SQL query keeps running to completion.

**Impact.** Abandoned report requests continue consuming a DB connection and remote SQL CPU until they finish or hit the 120s timeout, amplifying the DoS exposure above and wasting connection-pool slots. Under load the pool can be saturated by queries no one is waiting for.

**Recommendation.** Add CancellationToken cancellationToken to each Post action (model binding supplies HttpContext.RequestAborted), thread it through the sp_* helpers, and pass it to every ...Async EF call.

**Example:**

```
// current
public async Task<...> Post([FromBody] AccountSummaryReportRequest? request) {
    var rows = await sp_AccountSummaryReport.ExecuteAsync(_context, procedureRequest!, ...);
}
// fixed
public async Task<...> Post([FromBody] AccountSummaryReportRequest? request, CancellationToken ct) {
    var rows = await sp_AccountSummaryReport.ExecuteAsync(_context, procedureRequest!, ..., ct);
}
```

> 🔍 **Verifier note.** Severity Medium is fair and I leave it unchanged. Real reliability/resource gap that is systemic across every synchronous report endpoint, which justifies Medium over Low. Mitigating factors that keep it from being High: this is an authenticated, internal Ministry admin reporting tool (not public-facing), command timeouts are bounded (e.g. 120s in sp_HSCodeReport and CommandTimeoutSeconds in sp_AccountSummaryReport), and the ImportLicenceBySection path is additionally fronted by a 5-minute IMemoryCache. The finding's phrase "amplifying the DoS exposure above" references a separate finding not in scope here; on its own this is a reliability/connection-pool concern rather than a standalone DoS. Note: the ImportLicenceBySection Post path also does a fully synchronous .ToList() materialization at line 125 (not just ToListAsync at 126), which is even less cancellable, reinforcing the finding.


<a id="f-no-structured-logging-correlation"></a>
#### 🟡 MEDIUM — Structured logging and correlation IDs exist only in the Excel workers; the entire report/API surface logs nothing

**ID:** `no-structured-logging-correlation` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Observability &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Program.cs:17-95`
- `Backend/Service/ExcelExport/ExcelExportWorker.cs:24-35`
- `Backend/Controllers/Report/AccountSummaryReportController.cs:35-39`

**Problem.** Only 2 files in the backend inject ILogger — ExcelExportWorker.cs and ExcelExportCleanupWorker.cs. None of the 158 report controllers, the JWT service, the lookup controller, or any sp_* helper logs anything. There is no request-logging middleware, no correlation/trace-id enrichment, and the default Logging config is just console-level filters (appsettings.json:2-7). There is no Serilog/OpenTelemetry registration in Program.cs.

**Impact.** When a report fails, returns wrong totals (a recurring complaint per the project memory), or runs slowly, operators have no per-request log, no correlation id to tie a user complaint to a server event, and no audit trail of who ran which report against sensitive trade data — a significant gap for a government system.

**Recommendation.** Register structured logging (Serilog or built-in with a JSON console formatter), add UseW3CLogging or a request-logging middleware that emits a correlation id (echo X-Correlation-Id), and add scoped log statements around report execution including the report key, user (ClaimTypes.Name is already available, AccountSummaryReportController.cs:91), and duration. Consider OpenTelemetry for traces/metrics.

> 🔍 **Verifier note.** Locations are accurate. Backend/Program.cs:17-95 correctly bounds the service-registration block where logging/middleware would be added (none present). ExcelExportWorker.cs:24-35 correctly shows the only ILogger injection pattern; the second logging file is Service/ExcelExport/ExcelExportCleanupWorker.cs (the finding mentions it in prose but did not list it in locations — minor, not a defect). AccountSummaryReportController.cs:35-39 (ctor) is cited as a representative no-logger controller; note the user-identity line is at :91, which the finding also references correctly. Tangential observation found during review (out of scope for this finding): appsettings.json ships plaintext sa DB credentials and a hardcoded JWT key — separate security finding, not part of this observability finding.


<a id="f-second-dbcontext-not-under-migrations"></a>
#### 🔵 LOW — TradeNetDbContext (228 entities, the real data) is a DB-first scaffold with no migrations or schema-version control

**ID:** `second-dbcontext-not-under-migrations` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Data access / Migration hygiene &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/DBContext/TradeNetDbContext.cs:8-13`
- `Backend/DBContext/TradeNetDbContext.cs:1574-1575`
- `Backend/Migrations/ApplicationDbContextModelSnapshot.cs:13-14`

**Problem.** Two DbContexts are registered as pooled (Program.cs:64-67). Only ApplicationDbContext has migrations — the lone model snapshot is [DbContext(typeof(ApplicationDbContext))] (ApplicationDbContextModelSnapshot.cs:13). TradeNetDbContext is a 1575-line reverse-engineered scaffold (228 DbSets, OnModelCreatingPartial hook at line 1574) with all mapping hand-maintained and no migration history. There is no startup Migrate()/EnsureCreated() call for either context, so deploying schema changes for the app DB is a manual step, and the scaffolded read-model can silently drift from the live TradeNetDB. This aligns with the project's documented 'deployed proc drift' problem where .sql files are not auto-applied.

**Impact.** Schema drift between the scaffolded TradeNetDbContext and the production database produces runtime EF mapping/translation errors that surface only when a specific report is run (consistent with the recurring report-parity complaints). For the app DB, the absence of an automated migration step at deploy time means a forgotten manual migration leaves the running code expecting columns that do not exist.

**Recommendation.** Document and automate the regeneration of TradeNetDbContext from the canonical schema and add a CI check that fails if the scaffold is stale. For ApplicationDbContext, run db.Database.MigrateAsync() at startup (guarded) or as an explicit deploy step, and add an integration test that validates the model against the real schema.

> 🔍 **Verifier note.** Downgraded Medium -> Low. Two reasons. (1) The DB-first scaffold lacking migrations is largely by-design, not a defect: TradeNetDbContext is a read-model over an externally-owned schema (separate `TradeNetDBTest` connection string, heavy use of keyless `.ToView(...)` mappings such as vw_ImportLicenceItemTotalByCurrency). You do not author migrations for a schema another system owns — the standard EF approach is exactly to re-scaffold. So the genuine gap is narrow: no CI staleness check for the scaffold, plus no guarded Migrate() for the app-owned ApplicationDbContext. (2) The asserted impact linking schema drift to the recurring report-parity complaints is speculative; those documented complaints have concerned column/filter config parity (reportConfigs.ts / RDLC headers), not EF mapping/translation exceptions. No evidence in-repo ties this finding to an observed runtime failure. The recommendation (CI scaffold-staleness check + guarded MigrateAsync for ApplicationDbContext) is sound and worth doing, but this is latent process/hygiene debt rather than a confirmed runtime defect — Low is the appropriate rating. The locations are accurate and need no correction.


<a id="f-data-access-static-helpers-tight-coupling"></a>
#### 🔵 LOW — Data access is split across 83 static sp_* helpers and per-controller boilerplate with no injectable abstraction, hurting testability and consistency

**ID:** `data-access-static-helpers-tight-coupling` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Separation of concerns / Testability &nbsp;·&nbsp; **Effort:** Large &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/StoredProcedureToLinq/sp_HSCodeReport.StoredProcedure.cs:24-52`
- `Backend/Controllers/Report/ImportLicenceBySectionReportController.cs:99-163`
- `Backend/Controllers/Report/AccountSummaryReportController.cs:70-76`
- `Backend/Service/Reports/ReportQueryService.cs:8-35`

**Problem.** Of 158 report controllers, data access goes through 83 static partial sp_* classes (e.g. sp_HSCodeReport.ExecuteAggregateStoredProcedureAsync) plus a static ReportQueryService and a static ReportAggregationService — none are interfaces registered in DI, so controllers are hard-bound to concrete static methods that take a concrete TradeNetDbContext. The only DI-registered domain services are ICommonService<>, IJWTManagerService, ICountryCache and the Excel queue (Program.cs:92-95). Each controller also re-implements the same validation (TryCreateReportRequest), paging math, and the brittle fallback that detects a missing _pagination proc by string-matching SqlException.Number==2812 and the proc name (AccountSummaryReportController.cs:160-164). One controller (ImportLicenceBySectionReport) still holds full inline LINQ join+aggregation logic in the controller (lines 101-163).

**Impact.** Static, context-bound data access cannot be mocked, so unit testing forces a real SQL Server fixture (confirmed by Backend.Tests/ReportSqlServerFixture.cs). The duplicated validation/paging/fallback across 158 files means a fix (e.g. correcting a total) must be repeated everywhere, which is the mechanism behind the repeated per-report parity complaints. The 2812/string-match fallback is fragile to message localization or proc renames.

**Recommendation.** Introduce a thin IReportQueryExecutor / per-report service interface registered in DI so controllers depend on abstractions; centralize request validation, paging, and the missing-proc fallback into one shared base/service. Detect the missing proc by checking proc existence once at startup rather than catching SqlException.Number on every call. Move the inline LINQ in ImportLicenceBySectionReport into a helper consistent with the rest.

> 🔍 **Verifier note.** Keep severity Low. Two corrections to the writeup before acting on it: (a) The SqlException.Number==2812 / proc-name string-match fallback exists in only ONE controller (AccountSummaryReportController.cs:160-164), not "across 158 files" — do not present it as widespread duplication. (b) Drop or soften the claim that this abstraction gap is the mechanism behind per-report parity complaints; it is unsubstantiated and conflicts with documented root causes (lookup wiring, missing ColumnTotals, header mismatches). The genuinely broad duplication is TryCreateReportRequest (157/157 controllers) and direct static-helper + concrete-DbContext coupling, which is the legitimate basis for the Low-severity testability/maintainability finding. sp_ class count is 82 (finding's 83 is fine). Locations cited are all accurate.


---

### Backend — Code Quality & Maintainability

> The backend is two codebases in one. The newer reporting infrastructure (Service/ExcelExport, Service/Reports/CountryCache, ReportLookupsController) is genuinely well-engineered: scoped DbContext access via IServiceScopeFactory, atomic DB leases, structured ILogger usage, magic strings hoisted to constants, and correct nullable handling. The older surface — AuthController, JWTManagerService, UploadController, ChatController, BaseAPIController, ApiResult's dynamic filter, and the ~145 hand-cloned report controllers plus ~93 StoredProcedureToLinq files — is low quality: pervasive copy-paste (157 identical TryCreateReportRequest blocks), ~34 unreferenced/dead SP classes, plaintext password handling, broad swallowed exceptions that leak raw messages, almost no logging outside the Excel queue, missing/inconsistent authorization, weak input validation, and unsafe conversions that turn bad input into 500s. The hardcoded prod 'sa' connection strings and literal JWT key sit in checked-in appsettings.json. Maintainability is the dominant risk: any cross-cutting change to report request handling must be applied ~145 times by hand.

<a id="f-hardcoded-prod-credentials-jwt-key"></a>
#### 🔴 CRITICAL — Production SQL 'sa' credentials and weak literal JWT signing key checked into appsettings.json

**ID:** `hardcoded-prod-credentials-jwt-key` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Configuration / Secrets &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/appsettings.json:10 (JWT:Key), Backend/appsettings.json:26 (TemplateDB sa conn), Backend/appsettings.json:27 (TradeNetDBTest sa conn)`
- `Backend/appsettings.json:11`
- `Backend/appsettings.json:24`
- `Backend/appsettings.json:25`

**Problem.** appsettings.json hardcodes the JWT signing key as the literal string "This is my supper secret key for jwt" and two production connection strings to 203.81.66.111,14330 using 'User ID=sa;Password=Pr0fessi0nal@IM2022' for both TemplateDB and TradeNetDB, all with TrustServerCertificate=True. These are committed to source control.

**Impact.** Anyone with repo access (or a leaked artifact) gets sa-level access to the live government trade database and can forge valid JWTs for any user/role offline. This is the single highest-impact item: it converts a code read into full data and auth compromise. JWT:Key is consumed in Program.cs:74 and JWTManagerService.cs:41/71/111.

**Recommendation.** Rotate the sa password and JWT key immediately. Move all secrets to environment variables / user-secrets / a secret manager and inject at deploy time. Use a least-privilege SQL login instead of sa, set a long random JWT key (>=256 bits), and remove the values from the committed file.

> 🔍 **Verifier note.** Severity correctly Critical; verdict is Adjusted purely to fix the line numbers (JWT key is line 10 not 11; sa conn strings are lines 26-27 not 24-25). Additional supporting risk not in the finding: Authenticate() at JWTManagerService.cs:32 compares x.Password == users.Password in a LINQ query, implying plaintext password storage, and ValidateIssuer/ValidateAudience are both false in Program.cs (83-84), so a forged token only needs the leaked signing key. Recommendation (rotate sa password + JWT key, move to env vars/secret manager, use least-privilege SQL login, >=256-bit random key, purge from committed file and history) is sound. Note the secret also lives in git history across multiple commits, so removing it from HEAD alone is insufficient — history rewrite + credential rotation is required.


<a id="f-plaintext-password-and-asparallel-auth"></a>
#### 🔴 CRITICAL — Authentication compares plaintext passwords and misuses AsParallel() on an EF IQueryable

**ID:** `plaintext-password-and-asparallel-auth` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Security / Correctness &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Service/JWTManagerService.cs:32`
- `Backend/Service/JWTManagerService.cs:33`
- `Backend/Model/User.cs:21`

**Problem.** Authenticate() builds an IQueryable filtering on x.Name == users.Name && x.Password == users.Password — passwords are stored and compared in plaintext (User.Password is a plain string with no hashing). Worse, line 33 calls .AsParallel().Any() on an EF Core IQueryable. AsParallel() is a PLINQ operator; applied to an IQueryable it forces (at best) a degenerate client-side enumeration and is semantically meaningless for a DB query, indicating the author misunderstood the abstraction. The intended single-row existence check should be a server-side AnyAsync.

**Impact.** Plaintext passwords mean a single DB read (or a leaked backup) exposes every credential; the hardcoded 'sa' connection string in appsettings makes that read trivial for anyone with the repo. The AsParallel misuse can pull the predicate client-side, defeating SQL-level filtering and harming performance/correctness on the login hot path.

**Recommendation.** Hash passwords (ASP.NET Core PasswordHasher / BCrypt) and compare hashes. Replace UsersRecords.AsParallel().Any() with await _userService.Retrieve.AnyAsync(...) or fetch the user and verify the hash. Never store or compare raw passwords.

**Example:**

```
// current
IQueryable<User> UsersRecords = _userService.Retrieve.Where(x => x.Name == users.Name && x.Password == users.Password);
if (!UsersRecords.AsParallel().Any()) return null;
// fixed
var user = await _userService.Retrieve.FirstOrDefaultAsync(x => x.Name == users.Name);
if (user is null || !_hasher.Verify(users.Password, user.PasswordHash)) return null;
```


<a id="f-upload-controller-path-traversal-and-leak"></a>
#### 🟠 HIGH — UploadController is unauthenticated, vulnerable to path traversal, and leaks exception messages

**ID:** `upload-controller-path-traversal-and-leak` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Security / Exception handling &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/UploadController.cs:43 (anonymous Postupload), :83 (Path.Combine with unsanitized filename), :63-65 (extension check on postedFile.FileName not the write name), :113/:136 (SaveAsWebpAsync forces .webp output); Program.cs:166-169 (UseAuthorization + UseStaticFiles, no global FallbackPolicy)`
- `Backend/Controllers/UploadController.cs:23`
- `Backend/Controllers/UploadController.cs:43`
- `Backend/Controllers/UploadController.cs:83`
- `Backend/Controllers/UploadController.cs:94-100`

**Problem.** The controller has no [Authorize] (class or method), so Postupload is anonymous. The destination is built as Path.Combine("wwwroot","Image", filename) using the caller-supplied `filename` query value with no sanitization, then SaveAsWebpAsync writes there — a filename like '..\..\appsettings' enables path traversal / arbitrary file write. The size/extension checks are evaluated against postedFile but the actual write uses `file` from the loop and the extension validation can be bypassed (it checks `extension` from postedFile while saving under the user-supplied filename's name). The catch block returns the raw ex.Message-derived content to the client. Half the class is dead commented-out code (lines 25-39).

**Impact.** An unauthenticated attacker can write files into the served wwwroot (or escape it), enabling defacement or, combined with static file serving (app.UseStaticFiles), hosting of malicious content; raw exception text aids reconnaissance.

**Recommendation.** Add [Authorize], generate a server-side safe filename (e.g. Guid + validated extension) and reject any path separators; validate content-type server-side; return generic 400/500 without ex.Message; delete the commented-out fields. Confirm whether this endpoint is even used (it references wwwroot/Image while the report app stores exports under App_Data).

> 🔍 **Verifier note.** Verdict Adjusted because the exception-message-leak claim (lines 94-100) is false: ex.Message is added to a local Dictionary `dict` that is never serialized into the returned HttpResponseMessage (no Content/Json/Ok, no AddWebApiConventions formatter), so nothing leaks to the client — in fact no error body is returned at all. The two load-bearing vulnerabilities (no [Authorize] on an anonymous endpoint + unsanitized path-traversal file write into the static-served wwwroot) are real and correctly High. Two accuracy corrections to the impact: (a) SaveAsWebpAsync always rewrites the extension to .webp (filePath.Split(".")[0] + ".webp"), so the write is an arbitrary-path .webp, not arbitrary extension; (b) only .webp uploads are copied verbatim, other types are re-encoded via SkiaSharp, so writing arbitrary text (e.g., a poisoned appsettings) is not straightforward. Recommendation guidance to add [Authorize], generate a server-side Guid filename, and reject path separators remains valid; the "return generic 500 without ex.Message" item is moot since nothing is currently returned.


<a id="f-inconsistent-authorization-across-controllers"></a>
#### 🟠 HIGH — Inconsistent authorization: ChatController and UploadController fully anonymous; BaseAPIController marked [AllowAnonymous] exposing User table

**ID:** `inconsistent-authorization-across-controllers` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Security / Consistency &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/ChatController.cs:12-14`
- `Backend/Controllers/UploadController.cs:21-23`
- `Backend/Controllers/BaseAPIController.cs:15-16`
- `Backend/Controllers/UserController.cs:15`

**Problem.** Authorization is applied ad hoc per controller with no convention. Report controllers use [Authorize], but ChatController has no auth attribute at all (anonymous GET/POST of chat messages), UploadController has none, and BaseAPIController<T> is decorated [AllowAnonymous] at the class level with [Authorize] sprinkled on individual methods. UserController : BaseAPIController<User> thus exposes a generic, sortable/filterable Get over the User table whose model includes the plaintext Password (User.cs:21).

**Impact.** Easy to ship a User endpoint that returns password hashes/plaintext to any caller; the [AllowAnonymous]-class + [Authorize]-method pattern is fragile (a new method added without [Authorize] is silently public). Anonymous chat read/write allows tampering and spam.

**Recommendation.** Adopt a global fallback authorization policy (RequireAuthenticatedUser) via AddAuthorization/FallbackPolicy in Program.cs so endpoints are secure by default and only explicit [AllowAnonymous] opts out. Remove [AllowAnonymous] from BaseAPIController, add [Authorize] to ChatController, and never serialize Password (use a DTO / [JsonIgnore]).

> 🔍 **Verifier note.** Severity High is appropriate: anonymous file upload (UploadController.Postupload writes to wwwroot using a caller-supplied `filename`, enabling overwrite/path-abuse and public-web persistence), anonymous chat read/write (tampering/spam, and Post trusts a fully client-supplied ChatModel), no FallbackPolicy in Program.cs, and a User model whose plaintext Password is serialized by the generic Get. Correction to the impact wording: UserController's password-exposing Get is NOT anonymous — UserController has class-level [Authorize] (UserController.cs:13) and the inherited Get methods are [Authorize]-decorated, so it requires a valid JWT (but lacks role/permission checks). The recommendations (global RequireAuthenticatedUser FallbackPolicy, remove class-level [AllowAnonymous] from BaseAPIController, add [Authorize] to ChatController/UploadController, never serialize Password via DTO/[JsonIgnore]) are all sound for this codebase.


<a id="f-dead-code-sp-to-linq-and-controllers"></a>
#### 🟡 MEDIUM — ~34 unreferenced StoredProcedureToLinq classes (explicit _old/_V2/_Seperated/Test dead code)

**ID:** `dead-code-sp-to-linq-and-controllers` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Dead code &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/StoredProcedureToLinq/sp_LicencePermitSearch_old.cs`
- `Backend/StoredProcedureToLinq/sp_NewReport_old.cs`
- `Backend/StoredProcedureToLinq/sp_TestReport.cs`
- `Backend/StoredProcedureToLinq/sp_MPUReportV2.cs`
- `Backend/StoredProcedureToLinq/sp_MPUReport_Seperated_OnineFee.cs`

**Problem.** A reference scan of the 93 StoredProcedureToLinq files found ~34 classes with zero references anywhere in the codebase (outside their own file/migrations). Several are unambiguous abandoned versions: sp_LicencePermitSearch_old, sp_NewReport_old, sp_TestReport, sp_MPUReportV2, sp_MPUReport_Seperated_OnineFee. (Some others in the list are EICC/Dashboard/Wine variants that appear to have been superseded by *_Fast equivalents.) Note the typo 'OnineFee' in a committed filename.

**Impact.** Dead code inflates the 93-file 'sprawl', confuses parity checks against Tradenet 2.0, and tempts maintainers to copy the wrong (stale) version — a real trap given the documented MPU/Wine 'Fast' migrations. It also slows build and IDE navigation.

**Recommendation.** Delete the clearly-dead _old/_V2/_Seperated/Test files now. For the remaining unreferenced classes, confirm against routing then remove. Going forward, delete superseded versions in the same commit that introduces the replacement rather than renaming with _old.

> 🔍 **Verifier note.** No runtime/security/correctness impact — this is purely maintainability dead code, so Medium sits at the upper bound for this category. However it is defensible here because: (1) ~35 of 93 files (~38%) are dead, and (2) the project's own memory notes explicitly warn of a 'stale version trap' from MPU/Wine Fast migrations, which this dead code directly creates. I leave severity at Medium. Minor caveat on methodology: my full-directory scan keyed on filename-base = primary class name, which is a sound heuristic here since each file's static class matches its filename; the 5 explicitly cited files were verified by direct class-name grep (not just filename), so the core claim is rock-solid. One adjacent observation outside the finding's scope: the LIVE sp_LicencePermitSearch (no _old) also has no controller references, but that does not affect this finding. Recommendation to delete the clearly-dead _old/V2/Seperated/Test files immediately is safe and correct; the remaining superseded-by-Fast files should be removed after a final routing confirmation as the finding advises.


<a id="f-apifilter-unguarded-datetime-convert"></a>
#### 🟡 MEDIUM — Dynamic filter does an unguarded Convert.ToDateTime on user input (FormatException -> 500)

**ID:** `apifilter-unguarded-datetime-convert` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Input validation / Robustness &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Model/APIResult.cs:221-233`
- `Backend/Model/APIResult.cs:299-316`

**Problem.** ApplyFilter, when the filter column name Contains("Date"), calls Convert.ToDateTime(filterQuery) twice with no try/parse guard. Any non-date filterQuery throws FormatException, which (given no global handler) surfaces as an unhandled 500. The column-name-contains-'Date' heuristic is also brittle (matches e.g. 'UpdateDate', 'Validate'). The dynamic Where/OrderBy use System.Linq.Dynamic.Core with column names gated by IsValidProperty reflection, which mitigates injection, but the value path is unvalidated.

**Impact.** A malformed filter value from the SPA crashes the request with a 500 instead of a clean 400; combined with no logging, intermittent report failures are hard to diagnose.

**Recommendation.** Use DateTime.TryParse and return/ignore on failure (or surface a 400). Match date columns by actual property type (typeof(T).GetProperty(...).PropertyType) rather than a substring of the name.

**Example:**

```
// current
if (filterColumn.Contains("Date")) { var endate = Convert.ToDateTime(filterQuery); ... }
// fixed
if (IsDateProperty(filterColumn)) { if (!DateTime.TryParse(filterQuery, out var d)) return source; ... }
```

> 🔍 **Verifier note.** Adjusting severity down to Low (finding is otherwise fully Confirmed). Mitigating factors: the endpoints are [Authorize]-gated, so only an authenticated admin can trigger it; the realistic caller is the SPA's own date picker which normally sends valid ISO dates; there is no data loss, no injection (column gated by reflection), no security impact. The outcome is purely a 500-instead-of-400 robustness/diagnosability defect. All technical claims in the finding are accurate; only the Medium rating is slightly generous for an auth-gated, hard-to-trigger crash. Locations are correct: APIResult.cs:221-233 is the unguarded date block; APIResult.cs:299-316 is IsValidProperty (the cited injection mitigation).


<a id="f-swallowed-exceptions-and-leaked-messages"></a>
#### 🟡 MEDIUM — Broad catch blocks swallow context, leak raw exception messages, and there is almost no logging on the request path

**ID:** `swallowed-exceptions-and-leaked-messages` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Exception handling / Observability &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/AuthController.cs:39-42 (real message leak); Backend/Model/APIResult.cs:235-248 (bare catch fallback); Backend/Controllers/UploadController.cs:94-100 (swallowed exception, but message is NOT actually returned to client)`
- `Backend/Controllers/AuthController.cs:39-42`
- `Backend/Controllers/UploadController.cs:94-100`
- `Backend/Model/APIResult.cs:235-248`

**Problem.** AuthController.Login catches Exception and returns StatusCode(500, ex.Message) — internal error text returned to clients and never logged. UploadController does the same. ApiResult.ApplyFilter wraps its Contains() filter in a bare `catch {}` that silently falls back to an equality filter, hiding genuine errors. Only 2 files in the entire backend (both in Service/ExcelExport) inject ILogger; controllers and the LINQ report layer perform no logging at all, so production failures outside the Excel queue are invisible.

**Impact.** Operators get no telemetry for failed logins or report queries; attackers get stack/SQL detail via leaked messages; the silent catch in ApplyFilter can mask a misconfigured filter as 'no results', producing wrong reports with no trace.

**Recommendation.** Inject ILogger<T> and log exceptions with context; return a generic ProblemDetails (no ex.Message) to clients. Replace the bare catch in ApplyFilter with a specific, logged fallback or upfront type detection. Consider a global exception-handling middleware so the 145 controllers don't each reinvent error handling.

> 🔍 **Verifier note.** Net: the underlying issues are real (genuine message leak on the unauthenticated login endpoint, bare catch in ApplyFilter, near-total absence of request-path logging, no global exception handler across ~167 controllers). Medium severity is appropriate for the aggregate observability/leak gap. Corrections: UploadController does NOT leak ex.Message to clients (message is added to a dict that is never returned); only AuthController is a true client-facing leak; controller count is 167 not 145; AccountSummaryReportController's ex.Message use is internal inspection, not a leak. Recommendation (ILogger injection, ProblemDetails instead of ex.Message, replace bare catch with typed/logged handling, global exception middleware) remains sound.


<a id="f-inconsistent-api-result-shapes"></a>
#### 🟡 MEDIUM — Inconsistent API result shapes and return types across controllers

**ID:** `inconsistent-api-result-shapes` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** API consistency &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/UploadController.cs:43`
- `Backend/Controllers/AuthController.cs:25`
- `Backend/Controllers/ChatController.cs:22`
- `Backend/Controllers/ReportLookupsController.cs:43`
- `Backend/Model/APIResult.cs:12`

**Problem.** Endpoints return wildly different envelopes: report controllers return ApiResult<T> (paged, with TotalCount/HasNextPage), ReportLookupsController returns bare List<T>/record, ChatController returns raw List<ChatModel>, AuthController returns Ok(TokenModel) or NotFound() with no body, and UploadController returns System.Net.Http.HttpResponseMessage (the WCF/HttpClient type, not an ASP.NET Core IActionResult) which serializes incorrectly through MVC. Error encoding is equally inconsistent (BadRequest(string) vs NotFound() vs StatusCode(500, ex.Message) vs a 'dict' that is built but never returned).

**Impact.** Frontend must special-case each endpoint; the HttpResponseMessage return in UploadController will not produce the intended status/body in ASP.NET Core, so its success/error signaling is effectively broken. Inconsistency increases bug surface and onboarding cost.

**Recommendation.** Standardize on ActionResult<T> / ProblemDetails for errors. Replace HttpResponseMessage with proper IActionResult (Ok/BadRequest/StatusCode). Consider a shared response envelope or rely consistently on ApiResult<T> for collections.

> 🔍 **Verifier note.** Severity Medium is defensible. The genuinely functional defect is isolated to one image-upload endpoint (HttpResponseMessage return + unreturned dict), not a core report path; the broader "consistency" concern is real but mostly a maintainability/onboarding cost rather than a runtime bug. The recommendation (replace HttpResponseMessage with proper IActionResult, standardize on ActionResult<T>/ProblemDetails) is sound. Locations are accurate; only the ChatController body-vs-signature phrasing should be tightened (it returns Ok(List<ChatModel>), an un-enveloped list, not a raw List<ChatModel> return type).


<a id="f-bysection-cross-join-in-memory"></a>
#### 🟡 MEDIUM — ImportLicenceBySectionReportController materializes all rows then nested-loops sections x currencies in memory

**ID:** `bysection-cross-join-in-memory` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Performance / Code smell &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/Report/ImportLicenceBySectionReportController.cs:99-163`

**Problem.** BuildRowsAsync runs the licence query with a trailing .ToList() (materializing every matching detail row), then loops over every Section x every Currency and re-filters/re-sums the in-memory list per cell (LicenceListQry.Where(...).Count() and .Sum() inside a double foreach). This is O(sections * currencies * rows). Notably, the project already has a fully-in-SQL GROUP BY implementation for exactly this (sp_ImportLicenceDetailReport_Fast.SectionGroups / CreateSectionPagedResultAsync) which the Excel path of this same controller uses (GetSectionRowsAsync) — but the interactive Post path does not, so the two paths can produce different numbers and the slow one is the user-facing one.

**Impact.** On large date ranges this loads the entire detail set into web-server memory and does quadratic work, risking slow responses / memory pressure, and creates a UI-vs-Excel discrepancy (a parity bug class the project is actively fighting per CLAUDE.md).

**Recommendation.** Have Post delegate to the existing SQL-grouped sp_ImportLicenceDetailReport_Fast.CreateSectionPagedResultAsync (as the Excel path already effectively does), deleting BuildRowsAsync and the in-memory cross join. This also removes the bespoke 5-minute IMemoryCache layer unique to this controller.

> 🔍 **Verifier note.** One minor imprecision in the recommendation: it says Post should delegate to CreateSectionPagedResultAsync "as the Excel path already effectively does." In fact CreateSectionPagedResultAsync has zero callers in the codebase; the Excel path uses GetSectionRowsAsync. Both are backed by the same SQL-grouped SectionGroups, so the substance (an SQL-grouped impl already exists and the Excel path uses it) is correct — only the exact method name cited is slightly off. The closest existing precedent for the interactive Post path is the sibling Border controller, which uses CreateAggregateResultAsync(..., ReportAggregateDimension.Section, ...). Location and severity are accurate as given.


<a id="f-report-controller-copypaste-sprawl"></a>
#### 🟡 MEDIUM — ~145 report controllers are near-identical copy-paste (157 duplicated TryCreateReportRequest blocks)

**ID:** `report-controller-copypaste-sprawl` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Maintainability / Duplication &nbsp;·&nbsp; **Effort:** Large &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/Report/ (158 controllers; cited ImportLicenceDetailReportController.cs:94-140 and the ExportPermitAmendment vs BorderExportLicenceAmendment pair all verified)`
- `Backend/Controllers/Report/ImportLicenceDetailReportController.cs:94-140`
- `Backend/Controllers/Report/ImportLicenceBySectionReportController.cs:201-247`
- `Backend/Controllers/Report/ExportPermitAmendmentReportController.cs`
- `Backend/Controllers/Report/BorderExportLicenceAmendmentReportController.cs`

**Problem.** There are ~145 controllers under Controllers/Report, almost all 150-200 lines, each repeating the same skeleton: private const ReportKey, the same ctor injecting (TradeNetDbContext, [cache], IExcelExportJobService), an identical Post/Excel pair, the IStreamingExcelReport members, and a private TryCreateReportRequest validator. A diff of two sibling controllers (ExportPermitAmendment vs BorderExportLicenceAmendment) shows they differ ONLY in class/type names, the ReportKey/title literals, and a single FormType string. grep finds 157 copies of `private bool TryCreateReportRequest` and 157 controllers implementing IStreamingExcelReport.

**Impact.** Every cross-cutting change (e.g. adding a new validation rule, an audit field, a date-range cap, or a uniform error shape) must be applied ~145 times by hand, which is exactly how the inconsistencies in this report arose. The CLAUDE.md memory even documents a 'roll out the remaining ~155 report controllers' chore — the duplication is institutionalized.

**Recommendation.** Introduce a generic base (e.g. ReportControllerBase<TRequest, TProcRequest, TResult>) or a shared request-validation/mapping helper that centralizes the date validation, paging extraction, Excel enqueue, and IStreamingExcelReport plumbing. Per-report controllers should declare only ReportKey, title, and the request->procedureRequest projection. This collapses ~25k lines of controller code by a large factor.

> 🔍 **Verifier note.** The finding's count "~145" actually undercounts: there are 158 controllers / 157 duplicated validator+IStreamingExcelReport copies. That strengthens the finding. I downgraded severity from High to Medium: this is real, large-scale, accurately-described duplication, but it is mechanical low-risk boilerplate (validation + paging + Excel-enqueue plumbing) with no correctness or security defect. The recommendation (generic ReportControllerBase / shared validation-mapping helper) is sound. One nuance for any refactor: controllers are not 100% uniform — e.g. ImportLicenceDetailReport injects ICountryCache and uses a country-resolving paged path, while the Amendment controllers do their own paging inline with DefaultPageSize/MaxPageSize/MaxExcelDataRows constants and a ToResult() projection, and the BySection validator hardcodes Type="Oversea" vs the Detail controller's null-coalescing default. A base class would need to parameterize these variations, so the "collapses by a large factor" claim is directionally right but the per-report projection/paging differences are real and must be preserved.


<a id="f-no-dto-validation-attributes"></a>
#### 🔵 LOW — Report request DTOs rely on hand-rolled per-controller checks instead of validation attributes

**ID:** `no-dto-validation-attributes` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Input validation &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Model/ReportQueryRequest.cs:1-13`
- `Backend/Controllers/Report/ImportLicenceDetailReportController.cs:143-155`
- `Backend/Controllers/Report/ImportLicenceDetailReportController.cs:108-124`

**Problem.** ReportQueryRequest and the ~145 per-report request classes carry no DataAnnotations ([Required], [Range], etc.). PageSize/PageIndex are unbounded ints on the request (the MaxPageSize=1000 clamp lives only inside some _Fast SP files, not all paths — e.g. ImportLicenceBySectionReportController.Post uses request.PageSize directly with no upper bound). Date validation is duplicated as imperative if-blocks in every TryCreateReportRequest. Because [ApiController] is present, attribute validation would be enforced automatically, but it is unused.

**Impact.** An oversized PageSize on the uncapped paths can pull large result sets into memory; validation logic is duplicated 145x and can drift between reports (a maintainability and DoS-surface concern rather than an exploit).

**Recommendation.** Add [Range] on PageSize (e.g. 1-1000) and [Required] where appropriate to the shared base request, and let [ApiController] auto-return 400s. Apply the page-size clamp uniformly (centralize in ApiResult/base controller) so no report path is uncapped.

> 🔍 **Verifier note.** Counts were ~157 (not "~145") request classes / ~158 controllers — the finding's "145x" is a slight under-count but in the same ballpark, not material. The DoS framing should say "force serialization of an already-materialized result set" rather than "pull large result sets into memory from the DB" for the ImportLicenceBySection path specifically; the finding already hedges this as non-exploit. No location or severity change needed.


---

### Backend — Performance & Scalability

> The report stack has clearly been reworked for scalability and the "happy paths" are mostly sound: the `_Fast` detail reports use AsNoTracking + DTO projection, push GROUP BY/ORDER BY/OFFSET into SQL, resolve country/lookup CSVs from an in-memory cache (avoiding per-row correlated subqueries), and the async Excel queue streams rows to disk in chunks with a bounded StreamingExcelWriter. The most impactful remaining issues are: (1) several generic detail reports paginate with OFFSET/FETCH but no stable ORDER BY, so pages silently drop or duplicate rows; (2) the "fast" paging path re-runs a separate full COUNT query joining the item fan-out on every page when IncludeTotalCount is true, and the aggregate/Daily reports re-execute the entire SQL GROUP BY (plus a second FX query for Daily) on every page request; (3) the Border `_Fast` paths order a UNION ALL of two heavy multi-join queries by a non-unique CreatedDate, an unstable OFFSET sort; (4) SQL connection pooling (Max Pool Size 100) combined with MARS=False and a 1024-context EF pool is unconfigured; and (5) a large body of legacy non-Fast LINQ that loads full entities with per-row correlated country subqueries still ships in the assembly (mostly dead, but two reports' active paths still buffer a full UNION in memory). None are remote-exploitable, but they degrade large date-range report runs and produce subtly wrong/duplicated paged data.

<a id="f-offset-without-stable-order-by"></a>
#### 🟠 HIGH — OFFSET/FETCH pagination without a stable, unique ORDER BY drops and duplicates rows

**ID:** `offset-without-stable-order-by` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Pagination correctness &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Model/APIResult.cs:112-143`
- `Backend/Model/APIResult.cs:251-273`
- `Backend/Service/Reports/ReportQueryService.cs:25-33`
- `Backend/StoredProcedureToLinq/sp_ReExportReport.cs:70-97`
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs:441-456`

**Problem.** CreateFastPageAsync applies Skip(pageIndex*pageSize).Take(pageSize+1) for OFFSET/FETCH paging, but ApplySort (APIResult.cs:251-273) returns the source UNCHANGED when no sortColumn is supplied — and the generic report path (ReportQueryService.CreatePagedResultAsync) always passes sortColumn=null. Many generic detail report Query() methods (e.g. sp_ReExportReport.Query at sp_ReExportReport.cs:70-97) have no internal `orderby`, so EF emits OFFSET/FETCH over an unordered result set. Even where an ORDER BY exists, it is on a non-unique column: the _Fast detail rows order by `licence.CreatedDate` only (Rows() at sp_ImportLicenceDetailReport_Fast.cs:441), and the item fan-out means many rows share a CreatedDate. SQL Server does not guarantee a stable order for ties across separate OFFSET calls.

**Impact.** Users paging through a report can silently miss rows entirely or see the same row on two pages — for a government trade-licence report this is a data-integrity/audit problem, not just cosmetic. The bug is invisible in testing with small datasets and surfaces exactly when a report has many same-timestamp rows (bulk-issued licences).

**Recommendation.** Append a unique tiebreaker to every paged ORDER BY (e.g. the licence/permit primary key Id), and make the generic fast-page path refuse to page an unordered query. Concretely: in each Rows()/Query() add `.ThenBy(row => row.Id)` (carry the PK into the projection), and in ApplySort fall back to a deterministic key column instead of returning the source unsorted.

**Example:**

```
// sp_ImportLicenceDetailReport_Fast.Rows (current)
return RowsUnordered(db, request).OrderBy(row => row.CreatedDate);
// fixed: add a unique tiebreaker so OFFSET/FETCH is stable
return RowsUnordered(db, request).OrderBy(row => row.CreatedDate).ThenBy(row => row.LicenceItemId);
```

> 🔍 **Verifier note.** Severity High is appropriate and arguably conservative given breadth: the generic unordered-OFFSET path backs 35 controllers and 60/93 SP files lack any ordering, and the high-volume _Fast Import Licence detail report orders only by non-unique CreatedDate with a per-item fan-out that guarantees ties. The recommended fix (carry the licence/permit PK into the projection and add .ThenBy(row => row.Id), plus a deterministic fallback key in ApplySort) is correct and low-risk. Note ImportLicenceDetailFastRow does not currently project an Id, so implementing the tiebreaker requires adding the PK to the projection in OverseaRows/BorderPaThaKaRows/BorderIndividualTradingRows.


<a id="f-border-union-unstable-and-heavy"></a>
#### 🟡 MEDIUM — Border report pages order a UNION ALL of two 11-table joins by a non-unique key

**ID:** `border-union-unstable-and-heavy` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Pagination correctness / query cost &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs:444-456`
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs:621-777`

**Problem.** For request.Type == 'Border', RowsUnordered concatenates BorderPaThaKaRows and BorderIndividualTradingRows (each an 11-table join), and Rows() then orders the UNION ALL by CreatedDate alone before Skip/Take. SQL Server must materialize and sort the entire combined result to satisfy OFFSET/FETCH, and CreatedDate is not unique across the two branches, so the same instability as finding offset-without-stable-order-by applies, compounded by the union. CountBorderPaThaKaAsync + CountBorderIndividualTradingAsync also run two more full counts.

**Impact.** Border licence/permit reports (a major report family) are the slowest to page and the most prone to row skip/dupe across pages, because the cost and the ordering ambiguity both scale with the union of two large joins.

**Recommendation.** Add a deterministic, branch-disambiguating tiebreaker to the ORDER BY (e.g. CreatedDate, then a (source-discriminator, Id) pair carried into the row shape). Consider giving each branch its own contiguous key range or sorting by (CreatedDate, Id) within a single UNION-aware ordering so OFFSET is reproducible.

> 🔍 **Verifier note.** Two minor refinements to the framing, not the verdict: (1) The ordering instability is not exclusively caused by the union — the item fan-out alone already makes CreatedDate non-unique within a single branch, so the same paging-instability bug exists on the Oversea path too (line 441 is shared by all Types). The union compounds cost and collision probability but is not the root cause; the root cause is "no unique tiebreaker in ORDER BY," shared with the sibling finding offset-without-stable-order-by. (2) Practical impact is bounded: page size is capped at MaxPageSize=1000 (line 23) and IncludeTotalCount paths add the two counts, but the slow full sort happens regardless of page. Severity Medium is appropriate — correctness (cross-page skip/dupe) plus avoidable query cost on a major report family, but not data corruption or a hard failure. Locations are accurate.


<a id="f-count-and-groupby-rerun-per-page"></a>
#### 🟡 MEDIUM — Full COUNT and full GROUP BY (plus FX query) re-executed on every page request

**ID:** `count-and-groupby-rerun-per-page` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** EF Core / query cost &nbsp;·&nbsp; **Effort:** Large &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs:44-46 and :299-408 (incl. Daily FX :402-405); Backend/Service/Reports/ReportAggregationService.cs:182-220; Frontend/src/components/My Components/Table/BasicTable.tsx:199`
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs:44-51`
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs:466-545`
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport_Fast.cs:299-408`
- `Backend/Controllers/Report/ImportLicenceDailyReportNewLicenceReportController.cs:43-58`
- `Backend/Service/Reports/ReportAggregationService.cs:182-220`

**Problem.** Two separate cost problems on the hot paging path. (1) When IncludeTotalCount=true, the detail report runs CountRowsAsync (sp_..._Fast.cs:466) as a SECOND full query that re-joins the licence+item fan-out and re-applies every filter, on EVERY page navigation — so each page click costs two full table scans of the filtered set. (2) The aggregate reports (By Section/Method/Country/Company/HSCode/Daily/TotalValue) call GetAggregateRowsAsync, which executes the ENTIRE SQL GROUP BY over the full filtered detail set, returns ALL groups, then pages them in memory (ReportAggregationService.CreatePagedResultFromGroups). For the Daily report this additionally fires ReportUsdConversionService.FillDailyUsdValuesAsync (a second ExchangeRate+Currency query) every time. None of this is cached, so paging a By-Company or Daily report over a wide date range re-runs the full grouping + FX join for every page and every 'Total' footer render.

**Impact.** Large date-range report runs (a year of licences across all sections) hammer the SQL server with full re-scans/re-groupings on each page turn, multiplying DB load by the number of pages a user clicks through and making wide reports feel slow under concurrent analyst use.

**Recommendation.** For detail reports prefer keyset/cursor paging so COUNT can be skipped, or cache the COUNT for a (filter-hash) for the life of a paging session and only recompute when filters change. For aggregate reports, push OFFSET/FETCH into the grouped SQL query (the grouped query is already an IQueryable in SectionGroups) instead of materializing all groups and paging in memory, and compute ColumnTotals with a single SUM query rather than summing a fully-materialized list. Cache the grouped result per filter-hash for a short window so repeated page turns reuse it.

> 🔍 **Verifier note.** BasicTable.tsx:199 always sends includeTotalCount=true so the detail COUNT genuinely re-runs per page. The aggregate 'page in memory' is over the SQL-grouped result, not the detail fan-out — the finding's main exaggeration. ~96 controller references use the aggregate group path, so the modest per-page recompute is widespread. A short-lived per-filter-hash cache would address both halves; pushing OFFSET/FETCH into the generic aggregate GROUP BY is a minor win since SectionGroups already demonstrates it.


<a id="f-legacy-notracking-and-client-eval"></a>
#### 🟡 MEDIUM — Legacy non-Fast LINQ loads full entities with per-row country subqueries and an in-memory UNION buffer

**ID:** `legacy-notracking-and-client-eval` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** EF Core anti-patterns &nbsp;·&nbsp; **Effort:** Large &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Live issue: Backend/StoredProcedureToLinq/sp_WineImportationReport_Fast.cs:47-55 + :137 (Rows() typed IEnumerable forces in-memory Count/Skip/Take before paging). Dead-code issue: Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport.cs:221-226 (in-memory union buffer) and :289-296 (per-row correlated country subquery), reachable only via the also-dead sp_ImportLicenceDaily_Detail_Report.cs. sp_ReExportReport.cs:70-97 pages in SQL (not a memory issue); only valid sub-claim there is missing ORDER BY on OFFSET/FETCH.`
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport.cs:218-231`
- `Backend/StoredProcedureToLinq/sp_ImportLicenceDetailReport.cs:289-296`
- `Backend/StoredProcedureToLinq/sp_WineImportationReport_Fast.cs`
- `Backend/StoredProcedureToLinq/sp_ReExportReport.cs:70-97`

**Problem.** The non-Fast detail Query() methods build their result with NO AsNoTracking and resolve ConsignedCountry/CountryofOrigin via correlated `string.Join(from country in db.Countries where (','+csv+',').Contains(...))` subqueries (sp_ImportLicenceDetailReport.cs:289-296) — one extra LATERAL/APPLY per output row, plus a string-LIKE on the Countries table that cannot use an index. Worse, the Border branch does `.AsEnumerable().Concat(...).OrderBy(...).AsQueryable()` (sp_ImportLicenceDetailReport.cs:221-226), which fully buffers BOTH unioned joins in application memory before sorting — defeating any OFFSET/FETCH. The _Fast versions already fixed all of this via the cache and SQL grouping. The import-licence non-Fast class appears to be dead (no controller references it), but the same buffering pattern is live in sp_WineImportationReport_Fast, and sp_ReExportReport.Query (live) projects without AsNoTracking and without an ORDER BY.

**Impact.** Any report still routed through these legacy methods loads the full filtered set into memory and sorts there, with a non-indexable per-row country lookup — memory pressure and slow queries on large ranges. Carrying the dead non-Fast import-licence code also invites a future caller to reintroduce the slow path.

**Recommendation.** Route every live report through the _Fast/SQL-grouped + ICountryCache path (as the active import-licence controllers already do), delete the confirmed-dead non-Fast classes, and for any remaining live non-Fast query add AsNoTracking, replace the correlated country subquery with cache-based CSV resolution after materialization (ReportLookupCache.ResolveCsv), and remove the `.AsEnumerable()...AsQueryable()` buffering so the union/sort/page runs in SQL.

> 🔍 **Verifier note.** Two framing errors corrected: (1) these are DTO projections, so "loads full entities/NO AsNoTracking" is largely moot — AsNoTracking is a no-op on non-entity projections; (2) sp_ReExportReport.Query is NOT buffered in memory — CreateFastPageAsync runs Skip/Take as SQL OFFSET/FETCH via IAsyncQueryProvider, so it does not "defeat" paging. The genuinely live, fixable defect is sp_WineImportationReport_Fast: change Rows() to return IQueryable for the detail paths (or keep an IQueryable handle) so CreatePagedResultAsync's Count/Skip/Take translate to SQL instead of LINQ-to-Objects. The non-Fast import-licence class and its sole (also-dead) caller sp_ImportLicenceDaily_Detail_Report can be deleted. The recommendation's direction is sound; just note the AsNoTracking advice has no effect on these projection queries.


<a id="f-connection-pool-unconfigured"></a>
#### 🔵 LOW — DbContext pool / SQL connection pool sizing unconfigured with MARS disabled

**ID:** `connection-pool-unconfigured` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Scalability / configuration &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Program.cs:64-67`
- `Backend/appsettings.json:26-27`

**Problem.** AddDbContextPool is used without specifying poolSize (defaults to 1024 pooled contexts), while the underlying SqlClient connection pool defaults to Max Pool Size=100 and the connection strings set MultipleActiveResultSets=False. The Excel queue worker holds a scoped TradeNetDbContext that streams a DB cursor (AsAsyncEnumerable) for the full duration of an export, occupying one physical connection the whole time; concurrent report requests plus exports can exhaust the 100-connection pool. The 1024 EF context pool can also hand out far more contexts than there are physical connections, so backpressure manifests as connection-pool-timeout exceptions rather than graceful queueing.

**Impact.** Under concurrent analyst load plus running exports, requests fail with 'connection pool timeout' / connection exhaustion rather than slowing gracefully; the oversized EF pool masks the real bottleneck. With MARS off, any code that tries to run a second query on a context with an open reader will throw.

**Recommendation.** Set an explicit, coherent ceiling: AddDbContextPool(..., poolSize: e.g. 128) aligned with an explicit `Max Pool Size` in the connection string, and add `Min Pool Size`/`Connect Timeout` tuning. Keep export concurrency (ExcelExportOptions.MaxConcurrency, currently 1) well below the connection budget. Confirm no streaming path needs MARS, or enable it where a context must run overlapping queries.

> 🔍 **Verifier note.** Locations are correct (Backend/Program.cs:64-67 and Backend/appsettings.json:26-27). Supporting evidence: Backend/Service/ExcelExport/ExcelExportWorker.cs:39,82; Backend/Service/ExcelExport/ControllerStreamingExcelReportJobHandler.cs:39; Backend/StoredProcedureToLinq/sp_ImportPermitDetailReport_Fast.cs:115-122; Backend/Service/ExcelExport/ExcelExportOptions.cs:21. The recommendation is reasonable but note poolSize:128 with MaxConcurrency=1 leaves ample headroom; the MARS-enable suggestion is not needed for current code paths since lookups resolve before the stream opens. Note appsettings.json also embeds live DB credentials in plaintext (sa password) — out of scope for this finding but worth flagging separately.


<a id="f-reflection-getvalue-per-cell-excel"></a>
#### 🔵 LOW — Excel writers call PropertyInfo.GetValue via reflection per cell on every export row

**ID:** `reflection-getvalue-per-cell-excel` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Excel export / CPU &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Service/ExcelExport/StreamingExcelWriter.cs:56-78`
- `Backend/Service/Reports/ExcelGenerator.cs:91-95`

**Problem.** Both the streaming writer (AppendRows -> _properties.Select(p => p.GetValue(row))) and the in-memory ExcelGenerator (WriteWorksheet) use reflection PropertyInfo.GetValue once per column per row. For exports approaching the 1,048,575-row sheet cap across ~40 columns that is tens of millions of boxing reflection calls, dominating export CPU. Streaming keeps memory flat (good), but CPU per row is high.

**Impact.** Large exports are CPU-bound and slow; with MaxConcurrency=1 a big export also blocks the single worker, lengthening the queue for other users' exports.

**Recommendation.** Cache a compiled accessor per (type, property) — e.g. build Func<object,object?> delegates once via expression trees or use a source-generated/typed row writer — instead of PropertyInfo.GetValue per cell. Reuse the same compiled accessors across the StreamingExcelWriter and ExcelGenerator.

> 🔍 **Verifier note.** The phrase 'dominating export CPU' is mildly overstated for typical workloads — the reflective GetValue sits alongside XmlWriter element writes and string formatting in WriteCell/FormatValue, which are of similar per-cell cost, and the DB query/zip compression often dominate. The 'tens of millions of calls' is a worst-case at the row cap, not representative of normal exports. These are framing caveats, not errors; the underlying code claim, both locations, and the MaxConcurrency=1 queue-blocking impact all check out. Severity Low remains appropriate, so verdict is Confirmed rather than Adjusted.


<a id="f-countrycache-vs-memorycache-duplication"></a>
#### ⚪ INFO — Two overlapping country caches with different TTLs and a redundant per-request freshness check

**ID:** `countrycache-vs-memorycache-duplication` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Caching correctness &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Service/Reports/CountryCache.cs:40-106`
- `Backend/Service/Reports/ReportLookupCache.cs:28-46`
- `Backend/Controllers/ReportLookupsController.cs:154-160`

**Problem.** Country names are cached in three places with three lifetimes: the singleton CountryCache (1-hour TTL, used by _Fast reports), ReportLookupCache.GetCountryNamesAsync (1-day IMemoryCache, appears unused now that CountryCache exists), and ReportLookupsController.GetCountries (1-day IMemoryCache, for the dropdown). The CountryCache invalidation is purely TTL/request-driven with no explicit invalidation hook, so an edit to the Countries table is invisible for up to an hour in reports and up to a day in the dropdown. EnsureLoadedAsync is also invoked on every _Fast report request (cheap when fresh, but an extra volatile read + branch per call).

**Impact.** Minor staleness skew between the report body and the country dropdown after reference-data edits, and mild code duplication that can drift. No correctness/perf hot-path impact today.

**Recommendation.** Consolidate on the singleton CountryCache (and a similar pattern for the other small lookups), expose an explicit Invalidate() to call when reference data is edited, and remove the now-redundant ReportLookupCache.GetCountryNamesAsync if confirmed unused.

> 🔍 **Verifier note.** Keep severity Info. Correction: GetCountryNamesAsync is NOT unused; it is used by sp_ExportPermitDetailReport_Fast.cs (lines 37/94/116), sp_ImportPermitDetailReport_Fast.cs (37/94/116), sp_ExportLicenceDetailReport_Fast.cs (37/94/116), and sp_ImportLicencePendingDetailReport_Fast.cs (36/91/112). The valid takeaway is consolidation/explicit-invalidation as cleanup, not deletion of GetCountryNamesAsync. The two caches partition reports rather than redundantly wrapping the same path.


---

### Backend — Testing

> The Backend.Tests project (11 source files, ~1,100 lines) is almost entirely a reflection-driven harness that exercises the ~165 report controllers' request-building, EF-to-SQL translatability, and Excel-envelope shape. That breadth is genuinely useful, but it is shallow: it asserts plumbing (routes exist, requests build, query translates, totalCount==0 on an empty DB) rather than report correctness, and it leaves the highest-value code paths completely untested. Zero tests cover authentication/JWT (JWTManagerService, AuthController), the report-aggregation engine (ReportAggregationService), the FX/USD conversion (ReportUsdConversionService), the export-dedup hashing (ExcelExportHasher), or the file-download authorization surface (ExcelExportController) — exactly the security-critical and aggregation-correctness paths the system depends on. Worse, most tests hard-require a live SQL Server (localdb or a shared TradeNetDBTest with hardcoded row-count expectations) with no skip guards, and there is no .NET CI workflow at all, so the suite cannot run in CI and effectively does not gate merges.

<a id="f-no-dotnet-ci-tests-not-run"></a>
#### 🟠 HIGH — No .NET CI pipeline — backend tests never run automatically

**ID:** `no-dotnet-ci-tests-not-run` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Test infrastructure / CI &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/.github/workflows/test.yml`
- `Frontend/.github/workflows/release.yml`
- `Frontend/.github/workflows/commitlint.yml`
- `.github/workflows (absent at repo root)`

**Problem.** There is no .github/workflows directory at the repository root and no workflow anywhere runs `dotnet test`. The only GitHub workflows present are under Frontend/.github/workflows (release, commitlint, test) and none of them reference the Backend or Backend.Tests projects (confirmed by grep for 'dotnet'/'Backend' returning nothing). The entire backend test suite is therefore run only manually on a developer machine, if at all.

**Impact.** Regressions in report aggregation, FX conversion, auth, or query translation can be merged and deployed to a government production system with no automated gate. The considerable effort already invested in the ~165-controller smoke harness yields no protection because nothing forces it to pass before merge/deploy.

**Recommendation.** Add a root-level GitHub Actions workflow that runs `dotnet test Backend.Tests` on every PR. Split the suite into a CI-safe tier (translation + pure-logic + InMemory tests) that runs unconditionally, and a DB-integration tier gated behind a service container or environment flag (see the DB-dependency finding). Fail the build on any test failure.

> 🔍 **Verifier note.** Severity High is appropriate and I am leaving it unchanged. This is a missing-CI-control/process gap rather than an exploitable code vulnerability, so it should not be Critical, but for a government production system that already has a substantial (~762-test) backend suite with zero automated gate before merge/deploy, High is well-justified. Minor caveat: the finder's phrase "~165-controller smoke harness" is plausible (multiple controller smoke-test files present) but I did not count exactly 165 controllers; the existence and ungated state of the harness is what matters and that is confirmed. The recommendation (root-level workflow running dotnet test Backend.Tests, split into CI-safe vs DB-integration tiers) is sound given the docs show DB-integration tests fail without TRADENET_REPORT_TEST_CONNECTION_STRING and specific stored procs.


<a id="f-auth-jwt-untested"></a>
#### 🟠 HIGH — Authentication / JWT issuance (JWTManagerService, AuthController) completely untested

**ID:** `auth-jwt-untested` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Missing coverage / security-critical &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Service/JWTManagerService.cs:30`
- `Backend/Service/JWTManagerService.cs:100`
- `Backend/Controllers/AuthController.cs:25`

**Problem.** JWTManagerService.Authenticate compares Name and Password directly in a LINQ Where (line 32, plaintext password match, no hashing), issues a 1-day HS256 token, and persists a base64-not-encrypted 'refresh token' (line 54). AuthController.Login returns the raw exception message to the client on failure (StatusCode 500, ex.Message, AuthController.cs:41). RefreshToken issues a 31-day token. None of this is tested: grep for JWTManager/Authenticate/RefreshToken in Backend.Tests finds only the unrelated test-user principal. ReportTestUser builds a fake ClaimsPrincipal, so the [Authorize] smoke check (ReportEndpointSmokeTests.cs:13) only verifies the attribute is present, never that the real auth pipeline accepts/rejects anything.

**Impact.** The single most security-sensitive component of a government system has no behavioral coverage. A regression that, e.g., returns a non-null token for a wrong password, mishandles a null/blank JWT:Key (Encoding.UTF8.GetBytes(...??""), line 41 — an empty key would still build a signer), or leaks the DB exception message via Login, would ship undetected. Tests would also have surfaced the plaintext-password and info-leak design issues.

**Recommendation.** Add unit tests for JWTManagerService with a faked ICommonService<User>/<TokenModel>: valid credentials -> non-null token whose claims contain the user Id and Permission/role and a ~1-day expiry; wrong password -> null; verify the issued token validates under the configured key. Add a controller test that Login returns 404 for unknown user and does NOT echo raw exception text on failure. These need no DB (mock the services).

> 🔍 **Verifier note.** Severity High is appropriate for a missing-coverage finding on the single most security-sensitive component (auth/JWT issuance) of a government system, especially since the untested code also harbors real design flaws (plaintext passwords, empty-key fallthrough, exception-message leak) that tests would catch. All cited line numbers are exact. No corrections to location or severity needed.


<a id="f-db-required-tests-no-skip-guards"></a>
#### 🟡 MEDIUM — Most tests hard-require a live SQL Server with no skip guards; suite is not CI-runnable as-is

**ID:** `db-required-tests-no-skip-guards` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Test infrastructure / DB dependency &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend.Tests/ReportSqlServerFixture.cs:13`
- `Backend.Tests/ReportTestHelper.cs:119`
- `Backend.Tests/ReportTestHelper.cs:128`
- `Backend.Tests/ReportEndpointSmokeTests.cs:36`
- `Backend.Tests/ReportSeededDatabaseSmokeTests.cs:10`
- `Backend.Tests/TempSectionValidation.cs:17`

**Problem.** ReportEndpointSmokeTests (POST + Excel for every controller) depend on ReportSqlServerFixture which calls EnsureCreatedAsync against `Server=(localdb)\mssqllocaldb` (ReportTestHelper.CreateSqlServerDbContext, line 122) — localdb exists only on Windows dev machines. ReportSeededDatabaseSmokeTests and TempSectionValidation connect to a real shared `TradeNetDBTest` SQL Server. None of these tests have a Skip guard or [Fact(Skip=...)]; ReportSeededDatabaseSmokeTests.TradeNetDBTest_database_is_available even asserts CanConnectAsync==true, so the whole class fails (not skips) when the DB is absent. On a Linux CI runner or any machine without localdb/the shared DB, these classes error out rather than degrade gracefully.

**Impact.** The suite cannot be run unattended in CI or by a new engineer without first provisioning SQL Server and a seeded TradeNetDBTest. This is the practical reason the tests are not wired into CI, and it makes the suite brittle and unwelcoming.

**Recommendation.** Gate DB-backed classes behind an environment-variable check that skips (not fails) when the server is unreachable — e.g. a custom `[SkippableFact]`/`[SkippableTheory]` (Xunit.SkippableFact) that calls `Skip.IfNot(canConnect)`. Provide the CI-safe tier (translation + pure-logic + EF InMemory) that needs no server, and run the SQL tier against a `mcr.microsoft.com/mssql/server` service container in CI. Prefer Testcontainers for SQL Server over a shared mutable instance.

> 🔍 **Verifier note.** Locations are all correct; only minor offset is that ReportSqlServerFixture EnsureCreatedAsync is on line 18 (the method opens at line 13, which is what was cited). The class-level fail-vs-skip detail for ReportSeededDatabaseSmokeTests is precisely as described. Recommendation (SkippableFact/Skip.IfNot env-gating, Testcontainers for SQL Server, dedicated server-free CI tier) is sound and actionable for this repo.


<a id="f-report-aggregation-service-untested"></a>
#### 🟡 MEDIUM — ReportAggregationService (the report-correctness engine) has zero tests

**ID:** `report-aggregation-service-untested` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Missing coverage / report aggregation &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Service/Reports/ReportAggregationService.cs:87`
- `Backend/Service/Reports/ReportAggregationService.cs:125`
- `Backend/Service/Reports/ReportAggregationService.cs:227`
- `Backend/Service/Reports/ReportAggregationService.cs:290`

**Problem.** ReportAggregationService is 325 lines of pure in-memory logic: grouping by dimension (Section/Method/Country/Company/HSCode/Daily/TotalValue), DISTINCT licence counting (case-insensitive, blank-filtered, line 112-116), Sum(Amount), ordering, paging clamps (PageSize<=0 -> default, capped at MaxPageSize 1000, lines 137-139), and the grand-total footer BuildColumnTotals (lines 227-243). No test references it (grep for 'ReportAggregationService' in Backend.Tests returns nothing). It takes plain IEnumerable<AggregateSourceRow>, so it is trivially unit-testable with no DB.

**Impact.** This is exactly where the documented customer complaints live (missing TOTAL/ColumnTotals rows, distinct-licence-count discrepancies vs the Tradenet 2.0 RDLC, currency-split grouping). A wrong DISTINCT, a sign/rounding error in totals, or an ordering change would silently ship wrong numbers in official trade reports, and the current suite would stay green. The empty-DB smoke tests only ever assert totalCount==0, so they can never catch an aggregation bug.

**Recommendation.** Add a focused unit test class with hand-built AggregateSourceRow lists: (a) DISTINCT licence count ignores duplicate LicenceNo and is case-insensitive; (b) rows in different currencies split into separate groups even for the same Section/Date; (c) BuildColumnTotals sums NoOfLicences and TotalValue across ALL groups, not just the current page; (d) Daily dimension also rolls up TotalUSDValue; (e) paging clamps (PageSize 0 -> 10, >1000 -> 1000, negative PageIndex -> 0); (f) Order ties broken by Currency. Assert exact values against numbers computed by hand from the legacy RDLC logic.

> 🔍 **Verifier note.** Recommendation is sound and actionable as written (hand-built AggregateSourceRow lists covering distinct/case-insensitive licence count, currency split, page-wide column totals, Daily USD rollup, paging clamps, and Currency tie-break). One wording correction for the report: not all smoke tests assert totalCount==0 — ReportSeededDatabaseSmokeTests.RepresentativeCounts asserts fixed non-zero counts (e.g. 3, 52) — but those are total-row-count checks and still do not validate any aggregation output, so the core claim (no test covers the aggregation engine) is intact.


<a id="f-usd-fx-conversion-untested"></a>
#### 🟡 MEDIUM — ReportUsdConversionService FX logic is untested despite intricate currency rules

**ID:** `usd-fx-conversion-untested` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Missing coverage / report aggregation &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Service/Reports/ReportUsdConversionService.cs:43`
- `Backend/Service/Reports/ReportUsdConversionService.cs:114`
- `Backend/Service/Reports/ReportUsdConversionService.cs:131`
- `Backend/Service/Reports/ReportUsdConversionService.cs:140`

**Problem.** ReportUsdConversionService.ConvertToUsd encodes several easy-to-break branches: USD pass-through with Round(,4) (line 125-128); per-100 currencies KRW/JPY divide factor by 100 (line 140-142); missing currency or USD rate falls back to 1 (lines 131-132, 'legacy == null ? 1' default); usdRate==0 returns null to avoid divide-by-zero (line 134); 'lowest Id wins' dedup of duplicate FX rows (lines 94-102). It was reconstructed from the legacy Tradenet 2.0 formula and is the source of the recently shipped 'Total USD Value' column. No test references UsdConversion/ConvertToUsd/FillDailyUsdValues/TotalUSDValue (grep returns nothing).

**Impact.** An error in the KRW/JPY divide-by-100, the rate-fallback, or the rounding would produce financially wrong USD totals on the Daily reports — the precise class of defect that triggered the original complaint and the FX rework. The aggregation already proved that even the developer relies on hand-derived legacy parity; without tests, the next refactor can silently diverge from the RDLC again.

**Recommendation.** Make ConvertToUsd internally visible (or test FillDailyUsdValuesAsync against an EF InMemory ExchangeRates/Currencies set, which is feasible since the query is simple) and add table-driven cases: USD passthrough; a EUR-style normal currency with known rate; KRW/JPY per-100; missing item rate -> factor uses 1; missing USD rate -> 1; usdRate==0 -> null; duplicate rate rows -> lowest Id chosen. Assert exact rounded decimals against the legacy formula.

> 🔍 **Verifier note.** Finding is real and accurately described. The four cited line numbers map correctly to the described behaviors. Recommendation is sound; FillDailyUsdValuesAsync uses a simple EF join (db.ExchangeRates join db.Currencies) that is straightforward to exercise with an InMemory provider, and ConvertToUsd is private static so an [InternalsVisibleTo] or a thin internal wrapper is needed for direct unit testing. Severity downgraded to Medium since it is a coverage gap on currently-correct code rather than a live bug.


<a id="f-excel-export-controller-authz-untested"></a>
#### 🟡 MEDIUM — ExcelExportController download/list/delete authorization (IDOR surface) untested

**ID:** `excel-export-controller-authz-untested` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Missing coverage / security-critical &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Controllers/ExcelExportController.cs:35`
- `Backend/Controllers/ExcelExportController.cs:57`
- `Backend/Controllers/ExcelExportController.cs:81`

**Problem.** ExcelExportController exposes GetJobs (returns ALL jobs to any authenticated user, with 'shared visibility' by design, line 33-42), Download by Guid (streams the file after only a status==Completed + file-exists check, lines 57-79), and Delete by Guid (any user can delete any job, lines 81-95). There are no tests for any of it (grep for ExcelExportController in Backend.Tests returns nothing), and the test harness deliberately scopes ControllerTypes to namespace 'Backend.Controllers.Report' (ReportTestHelper.cs:27), excluding this controller entirely.

**Impact.** These endpoints govern who can download or destroy generated trade-report exports. The current shared-visibility/no-owner-check behavior is an object-level-authorization (IDOR) decision that is neither pinned by a test nor flagged. A change to FilePath handling in IExcelExportFileStore, or a regression that drops the Completed/Exists guard, could expose or delete other users' exports, and nothing would catch it.

**Recommendation.** Use EF InMemory ApplicationDbContext + a fake IExcelExportFileStore to test: Download returns NotFound for unknown id, Conflict when status!=Completed, 410 when the file is missing, and File(...) only when both pass; Delete returns NotFound/NoContent correctly and removes the row. Add at least one explicit test documenting the intended authorization model (shared vs per-user) so the policy is a conscious, asserted decision.

> 🔍 **Verifier note.** Locations are accurate (lines 35, 57, 81). The controller's true line ranges are GetJobs 35-42, Download 57-79, Delete 81-95. One minor inaccuracy in the finding's impact text: a path-traversal regression is already guarded by ExcelExportFileStore.FullPath (lines 80-90); the real untested risk is the object-level authorization model (any authed user can list/download/delete any user's export by Guid), not traversal. This does not change the verdict or severity. The shared-visibility behavior is documented as intentional in code comments (lines 15-18, 33), but the Delete-any-job capability is not obviously deliberate and is the strongest argument for adding an explicit policy-asserting test.


<a id="f-smoke-tests-assert-plumbing-not-correctness"></a>
#### 🟡 MEDIUM — Endpoint smoke tests assert empty/zero results, not report correctness

**ID:** `smoke-tests-assert-plumbing-not-correctness` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Test quality / weak assertions &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend.Tests/ReportEndpointSmokeTests.cs:36`
- `Backend.Tests/ReportEndpointSmokeTests.cs:49`
- `Backend.Tests/ReportSeededDatabaseSmokeTests.cs:21`

**Problem.** The broad per-controller theories run against an EMPTY EnsureCreated database and assert only that totalCount==0 (ReportEndpointSmokeTests.cs:43-44) and that Excel returns a PK zip or a queued job (lines 58-68). The seeded-DB theory Report_post_endpoint_generates_seeded_database_page only asserts totalCount>=0 (ReportSeededDatabaseSmokeTests.cs:33) — which is true for literally any int and can never fail. The one meaningful data assertion (Representative_report_modules_match_seeded_database_row_counts) hardcodes expected counts (e.g. MemberRegistration==52, CompanyProfile==3) against a shared mutable TradeNetDBTest, so it breaks whenever that DB's data changes and only checks TotalCount, never field values or aggregation math.

**Impact.** These tests verify wiring (controller constructs, query translates, envelope shape) but cannot detect a wrong column, a wrong group key, a wrong sum, or a missing TOTAL row — i.e. the actual reported defects. The green suite gives false confidence in report correctness, and the hardcoded-count test is brittle against an external DB.

**Recommendation.** Move correctness assertions to deterministic, in-memory unit tests over ReportAggregationService/ReportUsdConversionService (see those findings). Replace the hardcoded shared-DB counts with a self-seeded fixture (Testcontainers or InMemory) so expected values are owned by the test, and assert on actual row field values for at least one representative report per family. Strengthen totalCount>=0 to a concrete expected count.

> 🔍 **Verifier note.** Severity Medium is appropriate: this is a test-quality/coverage gap (no production defect itself), so below High, but it directly disables the regression net for the exact bug class being shipped, so above Low/Info. Minor location nit: the array entry ReportSeededDatabaseSmokeTests.cs:21 is the method-declaration line; the actual vacuous assertion is line 33 (which the finding's body text correctly cites). Not downgrading for this. The recommendation (deterministic in-memory/Testcontainers unit tests over ReportAggregationService/ReportUsdConversionService asserting real field values, plus a self-seeded fixture owning expected counts) is the correct fix.


<a id="f-createcontroller-silent-fallback-masks-coverage"></a>
#### 🔵 LOW — Test harness silently falls back to a default constructor, masking dependency gaps

**ID:** `createcontroller-silent-fallback-masks-coverage` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Test quality / harness brittleness &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend.Tests/ReportTestHelper.cs:76`
- `Backend.Tests/ReportTestHelper.cs:233`
- `Backend.Tests/ReportTestHelper.cs:241`

**Problem.** CreateController picks the longest constructor whose parameters are ALL in a fixed allowlist (TradeNetDbContext/ICountryCache/IMemoryCache/IExcelExportJobService) and otherwise falls back to `[db]` (line 94). If a controller is later given a new dependency outside that allowlist, the harness will either pick a different/smaller constructor or pass a lone db and likely throw at Activator.CreateInstance — but only for that one parameterized case, with a generic message. Separately, PopulateRequest fills every request with neutral values (empty strings, 0, FromDate=year 2000, ToDate=year 2100, lines 241-295), so reports are always exercised with the widest possible date range and blank filters — edge cases like inverted date ranges, oversized PageSize, or filter-injection strings are never generated.

**Impact.** Coverage silently narrows as the code evolves: a new controller dependency or a request field with validation logic can slip through with the harness still 'passing' for the cases it can build. The uniform neutral payload also means no test ever drives the request-validation or filtering branches with adversarial input.

**Recommendation.** Make CreateController throw a descriptive failure (listing the unsatisfiable parameters) instead of falling back to `[db]`, so a new dependency is a loud test failure. Add a small number of explicit, non-reflection tests that drive a representative controller with edge-case requests (FromDate>ToDate, PageSize beyond the 1000 cap, a FilterQuery containing SQL/LINQ-dynamic metacharacters) and assert the documented behavior.

> 🔍 **Verifier note.** Two minor imprecisions in the finding, neither material: (1) the parenthetical "(empty strings, 0, ...)" -- PageSize is actually set to 10 (line 261), not 0; only other ints/longs/decimals are 0. (2) The line-72 anchors are fine but the `[db]` fallback the prose discusses is line 94 (the finding text itself correctly says "line 94"), and the 241-295 range is the CreateValue helper while PopulateRequest is 223-239. Severity Low is appropriate: this is a test-harness brittleness / coverage finding, not a runtime or security bug; the silent-fallback path cannot trigger today since the allowlist covers 100% of current controllers. Confirming as a valid, correctly-rated Low finding.


<a id="f-excel-hasher-and-filestore-untested"></a>
#### 🔵 LOW — ExcelExportHasher dedup logic and file-store path handling untested

**ID:** `excel-hasher-and-filestore-untested` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Missing coverage / export queue &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Service/ExcelExport/ExcelExportHasher.cs:29`
- `Backend/Service/ExcelExport/ExcelExportFileStore.cs`
- `Backend/Service/ExcelExport/ExcelExportJobService.cs`

**Problem.** ExcelExportHasher.ComputeHash is pure, deterministic logic that canonicalizes a request JSON by dropping grid-only fields (pageIndex/pageSize/sortColumn/sortOrder/filterColumn/filterQuery/includeTotalCount) and lower-casing+sorting keys (lines 18-61) to produce the dedup key for the export queue. It has no test. The export queue (ExcelExportJobService) and file store (path resolution / Exists / OpenRead / Delete) — which the only Excel coverage stubs out via FakeExcelExportJobService (ReportTestHelper.cs:310) — are also untested.

**Impact.** If canonicalization changes (e.g. a new grid-only field is added to ReportQueryRequest but not to IgnoredFields), two identical exports would hash differently and the dedup would silently break, regenerating duplicate large exports; conversely an over-broad ignore set could collapse genuinely different requests to one file and serve the wrong data. Path handling in the file store is a download-safety concern that nothing exercises.

**Recommendation.** Add pure unit tests for ComputeHash: two requests differing only in ignored grid fields hash identically; requests differing in a real filter (e.g. FromDate, Type) hash differently; key ordering is canonical. Add tests for ExcelExportFileStore Exists/OpenRead/Delete using a temp directory, including a missing-file path, to lock down download safety.

> 🔍 **Verifier note.** Severity Low is correct: this is a missing-coverage / regression-risk finding, not an active defect. The current IgnoredFields set matches ReportQueryRequest exactly and the file store has a traversal guard, so the code is correct today. The finding accurately characterizes the gap, locations, and impact. One small wording caveat: the file store's path handling is not entirely unguarded — FullPath rejects paths that escape the storage root — so the recommended test should assert that guard rather than imply it is missing. Locations and severity stand as reported.


<a id="f-query-translation-coverage-narrow"></a>
#### 🔵 LOW — EF query-translation tests cover a small slice of 93 SP-to-LINQ queries

**ID:** `query-translation-coverage-narrow` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Missing coverage / query translation &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend.Tests/ReportQueryTranslationTests.cs:9`
- `Backend.Tests/ReportQueryTranslationTests.cs:73`
- `Backend/StoredProcedureToLinq`

**Problem.** ReportQueryTranslationTests is the strongest CI-safe class: it builds queries and calls ToQueryString() (compile-only, no DB connection) to prove EF can translate them, including the empty/'Unknown' FormType switch-default branches. But it only covers a curated handful — sp_HSCodeReport, the 6 aggregate reports x 8 form types, sp_PendingReport, and 5 empty-branch cases — out of 93 files in StoredProcedureToLinq. The many *_Fast detail queries and the CreatePagedResultAsync/CreateSectionPagedResultAsync paths are not translation-checked here.

**Impact.** A LINQ change in an uncovered query that EF can no longer translate to SQL (a common .NET upgrade hazard, and EF Core was bumped to 9.0.6) would throw only at runtime in production rather than being caught by the cheap, DB-free translation test that already exists for its siblings.

**Recommendation.** Generalize the translation theory to enumerate every public static Query(TradeNetDbContext, *Request) method via reflection (the harness already locates them in ReportTestHelper.CreateQuery) and assert ToQueryString() succeeds for each with a neutral request. This stays CI-safe (no open connection) and scales coverage from a dozen to all 93 with little code.

> 🔍 **Verifier note.** Finding is accurate and well-evidenced. Caveat on the recommendation: 'scales coverage from a dozen to all 93' overstates reach — the proposed reflection-over-Query approach covers the report Query(db,request) methods (a subset of the 93; the directory includes search/_old/.StoredProcedure helper files), and would NOT automatically cover the _Fast CreatePagedResultAsync/CreateSectionPagedResultAsync paths since those don't expose a public Query method. The finder does separately call those out as an additional gap, which is correct, but the numeric '93' is loose. Net: keep as-is; treat the coverage-count as approximate.


---

### Frontend — Security

> The React/Vite admin frontend for a government trade-reporting system has serious client-side auth and data-handling weaknesses. The JWT and user identity/permission are stored in localStorage (XSS-exfiltratable), authentication state is derived entirely from client-controlled localStorage with no token-expiry/integrity check, and admin route protection is purely cosmetic (the real gate is the backend, but several routes aren't even wrapped). Most damaging: the admin user-list screen renders a "password" column, so the backend returns user passwords to the browser, and the login page console.logs submitted credentials. There is no Content-Security-Policy, hardcoded third-party infrastructure (public MQTT broker, UAT QR API) is wired in, and login defaults to demo credentials. The previously-flagged SheetJS CVE is NOT exploitable here because xlsx is used only for export (write), never to parse untrusted uploads.

<a id="f-passwords-rendered-in-userlist"></a>
#### 🔴 CRITICAL — Admin user list renders a cleartext 'password' column from the API

**ID:** `passwords-rendered-in-userlist` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Sensitive Data Exposure &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/pages/User/UserList.tsx:29 (frontend, accurate). Backend root cause is Backend/Model/User.cs:21 (public Password property with no [JsonIgnore]) plus Backend/Controllers/UserController.cs / BaseAPIController.cs returning the raw entity — NOT BasicHttpServices.ts:7, which is only a generic GET helper with no password-specific logic.`
- `Frontend/src/pages/User/UserList.tsx:29`
- `Frontend/src/services/BasicHttpServices.ts:7`

**Problem.** UserList configures BasicTable with displayData={['name', 'password', 'permission', 'isActive', 'id']}. For the table to display a 'password' value, the GET /User endpoint must serialize each user's password field into the JSON response, and the frontend then renders it directly in a visible table column. This means user account passwords (whether cleartext or a reversible/identifiable hash) are sent over the wire to every admin who opens the user list and are visible on screen, in the browser DOM, in network logs, and in any browser/proxy cache.

**Impact.** Any operator (or anyone who compromises an operator session, or who can read the response via the localStorage-stored token, browser history, or a logging proxy) obtains the passwords of other government accounts. In a Ministry-of-Commerce trade system this is a direct path to full account takeover and lateral movement. If the values are cleartext it is an outright credential breach.

**Recommendation.** Never return password (or hash) fields to the client. Remove 'password' from displayData immediately, and fix the backend User DTO/projection so the password column is never serialized. Passwords should be stored only as a salted strong hash (bcrypt/Argon2) and must never leave the server.

**Example:**

```
// current
displayData={['name', 'password', 'permission', 'isActive', 'id']}
// fixed
displayData={['name', 'permission', 'isActive', 'id']}
// AND backend: exclude PasswordHash from the User list projection/DTO
```

> 🔍 **Verifier note.** The second cited location (BasicHttpServices.ts:7) is not load-bearing — it is a generic axios GET wrapper that does nothing password-specific. The real backend cause is the un-ignored Password property on the User entity combined with the entity-as-DTO BaseAPIController. The finder hedged 'cleartext or reversible hash'; the actual code stores/compares passwords in cleartext (JWTManagerService.cs:32), so this is the worst case. Note BaseAPIController has [AllowAnonymous] at the class level but the Get action has its own [Authorize], so the list itself is auth-gated (every authenticated operator still sees all passwords) — the exposure scope is correctly described in the finding.


<a id="f-jwt-and-identity-in-localstorage"></a>
#### 🟠 HIGH — JWT, user id and permission stored in localStorage (XSS token theft)

**ID:** `jwt-and-identity-in-localstorage` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Authentication / Token Storage &nbsp;·&nbsp; **Effort:** Large &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/context/AuthContext.tsx:50-52`
- `Frontend/src/services/AxiosInstance.ts:10-11`
- `Frontend/src/context/AuthContext.tsx:32-34`

**Problem.** On login the bearer token, userId and permission are written to window.localStorage and the axios request interceptor reads localStorage.getItem('token') to attach Authorization on every call. localStorage is readable by any JavaScript running on the origin, so any XSS (or malicious dependency, of which this app has many: firebase, mqtt, xlsx, lodash, etc.) can read and exfiltrate the JWT and impersonate the operator until expiry.

**Impact.** A single reflected/stored/DOM XSS or a compromised npm dependency lets an attacker steal a valid government-admin session token and replay it from anywhere. Tokens in localStorage are not bound to the device and are not cleared by browser controls the way httpOnly cookies are.

**Recommendation.** Store the session token in an httpOnly, Secure, SameSite cookie set by the backend, and have the API read it from the cookie (with CSRF protection) instead of an Authorization header populated from localStorage. If a header is mandatory, keep the token only in memory (module-scope/Context) and use a short-lived access token + httpOnly refresh cookie. At minimum, add a strict CSP (see separate finding) to reduce XSS exploitability.

> 🔍 **Verifier note.** Locations and code are all accurate. One small framing caveat: this is an exposure that amplifies an XSS/supply-chain compromise rather than an independently exploitable bug, so High (not Critical) is the right rating. The cited dependency list is verified against Frontend/package.json. The logout() at lines 67-73 does clear localStorage, but that does not mitigate the XSS-read window. Worth confirming whether a CSP exists (finding references a separate CSP finding) — if absent, this stays High.


<a id="f-credentials-logged-to-console"></a>
#### 🟡 MEDIUM — Login/SignUp/PasswordReset log submitted form values (including password) to console

**ID:** `credentials-logged-to-console` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Sensitive Data Exposure &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/pages/authentication/SignIn.tsx:39`
- `Frontend/src/pages/authentication/SignUp.tsx:46`
- `Frontend/src/pages/authentication/PasswordReset.tsx:33`

**Problem.** onFinish does console.log('Success:', values) where values is the entire form payload — including the plaintext password (and email) the user just typed. This runs in the production build (no env guard, and Vite does not strip console.log by default).

**Impact.** Plaintext passwords are written to the browser console on every login/signup/reset. They are then visible in shared/recorded screens, captured by any error-monitoring or console-forwarding tooling, and trivially read by any other script on the page. Combined with the localStorage token storage, this widens credential exposure.

**Recommendation.** Remove these console.log calls (and the onFinishFailed logs). Add esbuild/terser drop: ['console','debugger'] in vite.config.ts so stray logs never reach production, and audit for other console.log of sensitive objects.

> 🔍 **Verifier note.** Two caveats. (1) Scope nuance: PasswordReset logs only the email, not a password — the finding's blanket "plaintext passwords on every login/signup/reset" overstates that one location. SignIn and SignUp are the genuine password-logging cases. (2) Blast radius is client-side: the password is written only to the browser console of the user who typed it (relevant for screen-share/recording and any console-forwarding/error-monitoring SDK on the page), not transmitted to a backend log. This is a real hygiene/sensitive-data-exposure issue but the victim is essentially the keyboard owner, so Medium is at the upper edge of defensible; Low-to-Medium is reasonable. Note also SignUp appears to be unwired demo scaffolding (no backend call, 5s setTimeout navigate, social-login buttons), which further limits practical impact there. Keeping severity at Medium given the live SignIn flow.


<a id="f-no-content-security-policy"></a>
#### 🟡 MEDIUM — No Content-Security-Policy or hardening response headers

**ID:** `no-content-security-policy` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Missing Hardening / XSS Defense &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/index.html:1-45`

**Problem.** index.html ships no CSP meta tag, and there is no evidence of CSP/X-Frame-Options/Referrer-Policy being set anywhere. The page also loads third-party origins (Google Fonts) and the app at runtime connects to a public MQTT broker and external image/QR hosts. With the JWT sitting in localStorage, the absence of CSP means any injected script has free rein to read it and call out to attacker domains.

**Impact.** No defense-in-depth against XSS: an injected script can read localStorage (token, passwords from the user list), exfiltrate to any host, and the app can be framed for clickjacking. For a government admin portal this is a significant gap.

**Recommendation.** Serve a strict CSP from the hosting layer (or a meta tag as a fallback): default-src 'self'; connect-src 'self' <api-origin> wss://<broker>; img-src 'self' data: <image-host>; script-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'. Also add X-Content-Type-Options: nosniff and a Referrer-Policy. Tighten connect-src once the MQTT/QR endpoints are moved in-house.

> 🔍 **Verifier note.** Severity Medium is well-calibrated and should stay: there is no demonstrated XSS sink in the codebase, so CSP/headers here are defense-in-depth rather than mitigation of a known injection — do not escalate. The strongest concrete impact is that the JWT (and a rendered password column in UserList) live in localStorage with no CSP to constrain script execution or connect-src, so any future XSS = trivial token/password exfiltration to any host, plus clickjacking via missing frame-ancestors/X-Frame-Options. Practical fix belongs in the production nginx (currently stock nginx:alpine in Frontend/dockerfile with no config), per the recommendation; the index.html meta tag is a fallback only.


<a id="f-public-mqtt-broker-chat"></a>
#### 🟡 MEDIUM — Chat uses a hardcoded public MQTT broker with a static shared topic

**ID:** `public-mqtt-broker-chat` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Insecure Third-Party Dependency / Data Exposure &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/Chat/ChatBox.tsx:43`
- `Frontend/src/components/Chat/ChatBox.tsx:39`
- `Frontend/src/components/Chat/ChatBox.tsx:84-87`

**Problem.** ChatBox connects to mqtt.connect('wss://broker.emqx.io:8084/mqtt') — a free public test broker — with no auth, and publishes to a fixed global topic 'chat-app-room2' using retain:true. The published payload is the room id (JSON.stringify(message.room)). Anyone on the internet can subscribe to that topic on the public broker.

**Impact.** Room identifiers for the government chat feature are broadcast to a shared public broker that any third party can subscribe to, leaking activity/metadata and which conversations are active. An external actor can also publish to the same topic to trigger every client's chat reload (the message handler refetches on any valid JSON), enabling a cheap DoS/notification-spoof. Reliance on a free public broker is also an availability risk.

**Recommendation.** Replace the public broker with an authenticated, self-hosted/private MQTT (or WebSocket) endpoint over TLS with per-user credentials and per-room topics scoped/authorized server-side. Do not use retain on broadcast control messages, and validate the publisher server-side rather than refetching on any inbound message.

> 🔍 **Verifier note.** Minor: the in-code message handler already parses-before-refetch (line 52-53 comment), a slight improvement over the finding's framing, but it does NOT mitigate the spoof vector since an attacker can trivially send valid JSON. The retain:true on a control message is also genuinely wrong — it persists the last room id on the broker for any future subscriber. Locations are accurate; line 43, line 39, and 84-87 all match. An equivalent second publish block exists at lines 107-109 (sendImageMessage) worth noting but not required to confirm the finding.


<a id="f-hardcoded-uat-qr-fallback"></a>
#### 🔵 LOW — Hardcoded UAT/localhost fallbacks in runtime config

**ID:** `hardcoded-uat-qr-fallback` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Configuration / Information Disclosure &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/config.ts:2-4`

**Problem.** config.ts falls back to https://localhost:8000/api/, https://localhost:8000/Image/ and the live UAT host https://uatapi.ecomreg.gov.mm/QR/ when the VITE_* env vars are missing. The QR fallback in particular hardcodes a real government UAT endpoint into the shipped bundle.

**Impact.** If env vars are not set at build time the app silently points at localhost (broken in prod) or at the UAT environment, risking calls to the wrong/UAT backend and disclosing the internal UAT hostname to anyone reading the JS bundle. Mismatched protocols could also cause mixed-content failures.

**Recommendation.** Fail fast (throw) when required VITE_* vars are unset instead of falling back, or use empty/relative defaults. Remove the hardcoded UAT host from source and supply it only via build-time env per environment.

> 🔍 **Verifier note.** Minor nuance: qrUrl is not referenced anywhere outside config.ts in the current codebase, so its only realistic impact is hostname disclosure in the bundle, not live mis-routing. baseUrl/imageUrl are the ones actually used at runtime and carry the localhost-in-prod breakage risk. Recommendation (fail-fast on unset required vars, remove hardcoded UAT host, ship a .env.example) is sound. Mixed-content note is theoretical since all fallbacks use https.


<a id="f-unprotected-admin-routes"></a>
#### 🔵 LOW — Timeline, Test and Certificate routes sit outside ProtectedRoute

**ID:** `unprotected-admin-routes` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Broken Access Control &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/routes/routes.tsx:100-107 (Certificate), :200-214 (/Timeline), :215-233 (/Test); contrast with ProtectedRoute children at :109-172`
- `Frontend/src/routes/routes.tsx:100-107`
- `Frontend/src/routes/routes.tsx:200-233`
- `Frontend/src/routes/routes.tsx:109-172`

**Problem.** Only /dashboards, /User and /Report are children of the <ProtectedRoute/> element. The 'Certificate', 'Certificate/:id', '/Timeline/Detail' and '/Test/New' '/Test/Edit/:id' routes are declared as siblings of ProtectedRoute, so they render for fully unauthenticated visitors. /Timeline and /Test mount DashboardLayout (the authenticated admin shell), and a leftover /Test debug page is shipped in the production route table.

**Impact.** Unauthenticated users can reach the admin layout and the Timeline detail / Test pages, exposing internal UI and triggering whatever data fetches those components perform. Shipping a /Test route in production is an unnecessary attack-surface and information-disclosure risk.

**Recommendation.** Move Timeline (and any other authenticated screens) under the <ProtectedRoute/> children block, and remove the /Test route from the production router entirely. Confirm the Certificate route is intentionally public and contains no sensitive data (currently it is static placeholder markup).

> 🔍 **Verifier note.** Severity lowered from Medium to Low. The exposed Timeline and Certificate pages are static placeholder UI with no data fetch; only /Test triggers a fetch (ChatList via token-bearing axios, so backend auth still governs actual data exposure). Real defects worth fixing: (1) inconsistent route protection - move Timeline/Test under ProtectedRoute, and (2) remove the leftover /Test debug route from the production router. Note that SPA client-side route gating is not a true security boundary regardless; the actual protection of sensitive data lives in the backend API auth, which this finding does not assess.


<a id="f-client-side-auth-state-spoofable"></a>
#### 🔵 LOW — isAuthenticated derived from client-controlled localStorage with no token validation

**ID:** `client-side-auth-state-spoofable` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Broken Access Control &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/context/AuthContext.tsx:22-34,77 and Frontend/src/routes/ProtectedRoute.tsx:8`
- `Frontend/src/context/AuthContext.tsx:22-34`
- `Frontend/src/context/AuthContext.tsx:77`
- `Frontend/src/routes/ProtectedRoute.tsx:8`

**Problem.** isAuthenticated is computed as !!user, where user is initialized purely from localStorage.getItem('userid') and localStorage.getItem('permission'). ProtectedRoute renders the admin UI whenever auth.isAuthenticated is truthy. The token is never decoded, its signature/expiry is never checked, and the 'permission' string is taken at face value. Anyone can open devtools and run localStorage.setItem('userid','x'); localStorage.setItem('permission','Admin') to render all protected admin screens client-side.

**Impact.** All client-side route guards and any permission-based UI gating can be bypassed by setting two localStorage keys. The frontend leaks the structure/contents of admin pages and any data that loads without a server-side authZ check. Real security therefore depends entirely on the backend enforcing authZ on every endpoint; if any endpoint trusts the client, this becomes a full bypass.

**Recommendation.** Treat the client guard as UX-only and ensure the backend authorizes every request. Additionally, validate the JWT on the client (decode + check exp) before treating the user as authenticated, and clear state on expiry. Do not derive role/permission from a mutable localStorage value; derive it from the verified token claims returned by the server.

> 🔍 **Verifier note.** Recommendation in the finding is sound (treat client guard as UX-only, ensure backend authorizes every request, validate JWT/exp client-side, derive role from verified claims). The most important mitigation it calls for already exists in the backend, which is why severity drops. Note the AuthController login (Backend/Controllers/AuthController.cs) is correctly [AllowAnonymous]; BaseAPIController is annotated [AllowAnonymous] at class level but each action overrides with [Authorize], so data actions are protected. Worth a quick confirmation that report controllers do not also re-open [AllowAnonymous] at action level, but the grep showed they carry [Authorize].


<a id="f-demo-creds-prefilled-login"></a>
#### ⚪ INFO — Login form pre-filled with demo@email.com / demo123

**ID:** `demo-creds-prefilled-login` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Insecure Default &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/pages/authentication/SignIn.tsx:121-125`
- `Frontend/src/pages/authentication/SignIn.tsx:121-125`

**Problem.** The SignIn form initialValues hardcode email: 'demo@email.com', password: 'demo123', remember: true. This is shipped to production.

**Impact.** Suggests a demo/seed account may exist with these well-known credentials; if such an account was ever created on the backend it is an immediate weak-credential foothold. At minimum it is unprofessional for a government portal and primes credential-guessing.

**Recommendation.** Remove the prefilled credentials (set empty initialValues). Verify no demo@email.com account with a weak password exists in any deployed database.

> 🔍 **Verifier note.** Could not inspect any deployed/live database from here, so the recommendation to confirm no real demo@email.com account exists remains valid as an operational check — the code-level search just shows nothing in-repo creates one. Backend dir exists but contains no seeding tied to these creds.


<a id="f-fileupload-window-write-sink"></a>
#### ⚪ INFO — useFileUpload opens a new window and writes image markup built from user input

**ID:** `fileupload-window-write-sink` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Potential XSS Sink &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/hooks/useFileUpload.tsx:22-37 (note: this hook is dead/unused; live preview path is useFileUploadWithoutCrop.tsx:77-82, which uses plain window.open, no document.write)`
- `Frontend/src/hooks/useFileUpload.tsx:22-37`

**Problem.** onPreview constructs src from envConfig.imageUrl + a file name / FileReader data URL, builds an Image element, then does window.open(src) followed by imgWindow.document.write(image.outerHTML). document.write of dynamically-constructed markup into a new window is a classic XSS sink; the src is partly derived from user-controlled file names and the (untrusted) configured image URL.

**Impact.** If an attacker controls a file name or the image URL value, crafted content reflected through outerHTML/document.write could execute script in the opened window context. Lower severity because exploitation depends on the upstream image source, but it is fragile and unnecessary.

**Recommendation.** Avoid document.write entirely. Preview images via the Ant Design <Image> preview component or by setting an <img src> on a sanitized, validated URL. Whitelist allowed extensions and never inject user-derived strings into raw HTML.

> 🔍 **Verifier note.** Two real reasons to downgrade: the cited hook has zero importers (dead code), and the document.write payload is image.outerHTML built from an Image whose src is set via the DOM property — the browser attribute-encodes it, and the URL scheme is fixed by the configured prefix. The recommendation (drop document.write, use AntD Image preview / sanitized src, whitelist extensions) is reasonable as a cleanup, but this should be Info/code-quality rather than a security finding. If anything is acted on, simply delete the unused useFileUpload.tsx.


<a id="f-sheetjs-cve-not-exploitable-note"></a>
#### ⚪ INFO — xlsx (SheetJS 0.18.5) is used only for export, not for parsing untrusted files

**ID:** `sheetjs-cve-not-exploitable-note` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Dependency Risk (assessed, not exploitable) &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:44`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:242-243`

**Problem.** package.json pins xlsx ^0.18.5, which has known prototype-pollution (CVE-2023-30533) and ReDoS (CVE-2024-22363) advisories. However those vulnerabilities live in the read/parse path (XLSX.read / readFile on attacker-supplied workbooks). A repo-wide search shows the app only calls XLSX.utils.table_to_book and XLSX.writeFile to generate downloads; there is no XLSX.read/readFile/sheet_to_json on untrusted input. So the CVEs are not currently reachable.

**Impact.** No direct exploit today because no untrusted .xlsx is parsed. Residual risk is supply-chain/version hygiene and the possibility that a future feature adds spreadsheet import.

**Recommendation.** Upgrade to the maintained SheetJS distribution (0.20.x from the official CDN; the npm 0.18.5 is end-of-life) for hygiene, and add a guard/code-review rule so that if file import is ever added it parses with a patched version and validates input.

> 🔍 **Verifier note.** Everything in the finding checks out as written; no correction to location or severity needed. One minor caveat: the recommendation to upgrade involves moving off the npm registry to the SheetJS CDN tarball, which is a non-trivial install/CI change (no drop-in npm 0.20.x exists), so the "hygiene fix" is slightly more involved than a version bump — but this does not affect the correctness or severity of the finding.


---

### Frontend — State & Data Fetching

> The frontend has effectively no shared state-management or data-fetching strategy. Redux is used only for a single `theme` slice; all server data (155 report pages, lookups, exports, dashboard) is fetched with hand-rolled axios calls and local `useState`, with no RTK Query, no caching/dedup, no request cancellation, and no normalization. The most material problems are: a race condition in the central `BasicTable` fetch (no abort, no response-staleness guard) that can show one report's data under another report's filters; auth token + permission stored in `localStorage` (XSS-exfiltratable, and the token is injected into every axios request); widespread swallowed errors that hide backend failures from operators; and the complete absence of a React error boundary, so any render exception blanks the entire admin app. There are also several smaller correctness bugs (stale-closure in `useFetchData`, a null-deref crash path in a dashboard card, redundant JSON round-tripping of every response).

<a id="f-auth-token-in-localstorage"></a>
#### 🟠 HIGH — JWT, user id and permission stored in localStorage and injected into every request

**ID:** `auth-token-in-localstorage` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** State persistence / auth storage &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/context/AuthContext.tsx:22-65`
- `Frontend/src/context/AuthContext.tsx:50-52`
- `Frontend/src/services/AxiosInstance.ts:10-13`

**Problem.** On login the bearer token, `userId`, and `permission` are written to `localStorage` (AuthContext.tsx:50-52) and re-read on every app start (lines 22-34). The request interceptor reads `localStorage.getItem('token')` and attaches it as `Authorization` on every outgoing request (AxiosInstance.ts:10-11), including when there is no token (it sends `Bearer null`). localStorage is readable by any JavaScript on the origin, so a single XSS (and this app renders untrusted report strings, runs MQTT/Firebase, and disables several CSP-relevant protections) exfiltrates a long-lived government-admin token. The client also trusts `permission` from localStorage for UI gating, which is user-editable.

**Impact.** Any XSS or malicious dependency can steal a privileged admin JWT and impersonate the operator against the Ministry backend. Because the token is in localStorage (not an httpOnly cookie) it survives tab close and is trivially scriptable. The `Bearer null` case also means unauthenticated requests are sent with a malformed header rather than none.

**Recommendation.** Prefer httpOnly+Secure+SameSite cookies for the session token so JS cannot read it. If localStorage must stay, at minimum: guard the interceptor to only attach the header when a real token exists, treat client-side `permission` as a hint only (server must authorize every endpoint), and add a strict CSP. Clear all three keys consistently on logout (logout already does this) and on 401.

**Example:**

```
// current
const token = localStorage.getItem('token');
config.headers.Authorization = `Bearer ${token}`; // sends 'Bearer null' when absent

// fixed
const token = localStorage.getItem('token');
if (token) config.headers.Authorization = `Bearer ${token}`;
```

> 🔍 **Verifier note.** Two minor overstatements that do NOT change the verdict: (1) The finding says the client "trusts permission from localStorage for UI gating," but ProtectedRoute.tsx gates only on isAuthenticated (= !!user), not on the permission value itself; the broader point still holds because isAuthenticated is derived purely from client-side localStorage. (2) The recommendation's "clear all three keys ... on 401" is already implemented — App.tsx:134-151 has a response interceptor that calls auth.logout() on HTTP 401, which removes all three keys. Also, the actual localStorage key is 'userid' (lowercase), written/read consistently, not 'userId' as the prose loosely implies — no functional bug. Locations cited are accurate.


<a id="f-no-request-cancellation-race"></a>
#### 🟡 MEDIUM — Report grid fetch has no cancellation; rapid filter/page changes race and can show stale/wrong data

**ID:** `no-request-cancellation-race` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Data fetching / race condition &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/My Components/Table/BasicTable.tsx:204-234`
- `Frontend/src/Report/Page/GenericReportPage.tsx:652-662`
- `Frontend/src/Report/Page/GenericReportPage.tsx:711-722`

**Problem.** BasicTable's load effect (lines 204-234) fires whenever `query` or `refreshKey` changes and calls `fetchData(query)` (the GenericReportPage `fetchRows` POST). There is no AbortController, no axios CancelToken, and no per-request sequence/staleness guard. `setData(result)` runs in `.then` regardless of whether a newer request has since been issued. On a government report with large result sets, switching pages quickly, hitting Filter repeatedly, or changing page size while a slow query is in flight means responses can resolve out of order: the response for an older filter/page overwrites the grid that the user is now looking at. grep confirms `AbortController`/`CancelToken`/`signal` appear nowhere in src.

**Impact.** An operator can be shown trade figures (transaction amounts, company lists, licence totals) that belong to a different filter or page than the one currently selected on screen. For a regulatory reporting tool this is a correctness/integrity problem, not just a UX glitch: decisions or exports may be made against mismatched data. Also wasted backend load from un-cancelled in-flight queries.

**Recommendation.** Cancel the previous request before issuing a new one and ignore stale responses. Minimal fix: create an AbortController per effect run, pass `signal` through `fetchData`->axios, abort in the effect cleanup. Belt-and-suspenders: capture a monotonically increasing request id and only `setData` if it is still the latest. Strategically, migrate report fetching to RTK Query (or TanStack Query), which gives request dedup, cancellation-on-unmount, and caching for free.

**Example:**

```
// current (BasicTable.tsx)
const result = fetchData ? await fetchData(query) : await fetch!(...);
setData(result ?? emptyPage<T>());

// fixed
useEffect(() => {
  const ctrl = new AbortController();
  (async () => {
    try { const r = await fetchData(query, ctrl.signal); if(!ctrl.signal.aborted) setData(r); }
    catch (e) { if(!ctrl.signal.aborted) setError(parseErr(e)); }
  })();
  return () => ctrl.abort();
}, [query, refreshKey]);
```

> 🔍 **Verifier note.** Locations are all correct. Note the legacy `fetch!(buildLegacyUrl(...))` path in BasicTable (line 223) has the same race, so a fix belongs in the BasicTable load effect itself (a request-id/staleness guard there covers both fetchData and fetch paths without needing to thread a signal through every report's fetchRows). A staleness guard in the effect is actually the more robust minimal fix here than AbortController alone, since axios cancellation would still need the guard to prevent a late .then from calling setData.


<a id="f-swallowed-errors"></a>
#### 🟡 MEDIUM — Backend errors are swallowed into generic strings; details discarded with empty catch blocks

**ID:** `swallowed-errors` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Error handling / observability &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/My Components/Table/BasicTable.tsx:226-227`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:257-258`
- `Frontend/src/hooks/useFormActions.tsx:45-48`
- `Frontend/src/hooks/useFormActions.tsx:57-60`
- `Frontend/src/Report/Page/GenericReportPage.tsx:639-643`

**Problem.** Catch blocks discard the actual error and replace it with a fixed message. BasicTable's grid catch is `} catch { setError('Failed to load table data.'); }` (line 226) — the error object isn't even bound, so the backend's status/message is unavailable. useFormActions shows a static `Modal.error('Something went wrong...')` for both Post and Put failures. The company-name lookup catch silently blanks the field. The Excel catch is generic. There is no central error normalization and (apart from the 401 interceptor) no logging.

**Impact.** Operators cannot tell a validation error from a server outage from a permission denial — every failure looks identical. Support/debugging on a government system becomes guesswork because the real error (400 with a server message, 500, network) is thrown away client-side. Genuine data problems can be hidden behind a bland 'Failed to load table data.'

**Recommendation.** Bind the caught error, extract `error.response?.status` and `error.response?.data` via a shared `parseApiError` helper, and surface a meaningful message (and a retry affordance) while logging the raw error. Distinguish 4xx (show server message) from 5xx/network (generic + retry). Consolidate this in the RTK Query baseQuery / a shared interceptor rather than per-call.

> 🔍 **Verifier note.** Minor inaccuracy in the recommendation, not the finding: this codebase uses axios (services/AxiosInstance.ts + a per-mount interceptor in App.tsx), not RTK Query — there is no baseQuery/createApi anywhere. The consolidation advice still holds but should target the axios response interceptor. Also worth noting the catches differ slightly in quality: useFormActions' New/Edit paths bind `ex` but never log it, whereas onFinishStepper and the Delete path do `console.error(ex)`; the BasicTable catches don't even bind the error. None of this changes the verdict or severity.


<a id="f-no-rtk-query-no-caching"></a>
#### 🟡 MEDIUM — All server data fetched manually with useState; no caching, dedup, or retry (Redux used only for theme)

**ID:** `no-rtk-query-no-caching` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Architecture / data fetching &nbsp;·&nbsp; **Effort:** Large &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/redux/store.ts:12-32`
- `Frontend/src/Report/Page/GenericReportPage.tsx:552-662`
- `Frontend/src/services/BasicHttpServices.ts:7-53`
- `Frontend/src/hooks/useFetchData.tsx:1-28`

**Problem.** The Redux store contains only a `theme` slice (store.ts:12-14). grep for `createApi`/`createAsyncThunk`/`fetchBaseQuery` returns nothing — `@reduxjs/toolkit` is installed but not used for data. Every data interaction is a bespoke axios/fetch call with local `loading`/`error`/`data` useState triplets duplicated across BasicTable, ExportsDrive, GenericReportPage lookups, the form hooks, and useFetchData. Lookups (`ReportLookups/*`) are cached only in component-local `lookupOptions` state (GenericReportPage.tsx:511-609), so they are re-fetched fresh on every navigation to a report and never shared between reports.

**Impact.** No cross-component caching or request dedup means the same lookup lists (countries, business types, etc.) are fetched repeatedly; no automatic retry/backoff on transient failures; loading/error logic is reimplemented inconsistently (and sometimes wrongly) in every page, raising maintenance cost and bug surface across 155 report pages.

**Recommendation.** Adopt RTK Query (already have the dependency) or TanStack Query for all report/lookup/export fetching. Define endpoints once with a shared baseQuery (token injection, baseUrl, error normalization) and get caching, dedup, cancellation, and consistent loading/error state for free. At minimum, hoist lookup caching above the page (a single shared cache) so it survives navigation.

> 🔍 **Verifier note.** Minor imprecision: the "155 report pages" figure is loose — the config files (reportConfigs.ts) contain ~134 controllerName entries; the ~155 number is the broader report-controller count used elsewhere in the project (CLAUDE.md says ~134, memory says ~155). The order of magnitude and the architectural point are sound, so this does not change the verdict. Locations are all accurate; the line ranges match. The useFetchData url-staleness bug (useCallback deps [] vs effect deps [url]) is extra supporting evidence for the "sometimes wrongly" claim and is itself a latent defect worth noting.


<a id="f-redundant-json-roundtrip"></a>
#### 🔵 LOW — Every API response is round-tripped through JSON.parse(JSON.stringify()) before use

**ID:** `redundant-json-roundtrip` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Performance / data handling &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/services/BasicHttpServices.ts:10-11`
- `Frontend/src/services/BasicHttpServices.ts:17-18`
- `Frontend/src/services/BasicHttpServices.ts:24-25`
- `Frontend/src/services/BasicHttpServices.ts:37-38`
- `Frontend/src/services/BasicHttpServices.ts:44-45`
- `Frontend/src/hooks/useFormload.tsx:19-20`

**Problem.** Axios already parses the JSON body into `resp.data`. Every helper then does `JSON.parse(JSON.stringify(data))` to produce the return value, and useFormLoad does it a second time on the already-parsed result. This is a pure-overhead deep clone of the entire payload.

**Impact.** For large report/company payloads this doubles serialization work on the main thread for no benefit, and it silently drops anything non-JSON-serializable. It also obscures intent and discards typing. Minor but pervasive (every read/write goes through these helpers).

**Recommendation.** Return `resp.data` directly (it is already a parsed object). If a defensive clone is ever needed, use `structuredClone`. Remove the duplicate parse in useFormLoad.

**Example:**

```
// current
const data = await resp.data;
const responseData = JSON.parse(JSON.stringify(data));
return responseData;

// fixed
return resp.data;
```

> 🔍 **Verifier note.** Severity Low is correct (borderline Info). One caveat on the IMPACT framing, not the bug: these helpers are imported by only 3 files (hooks/useFormload.tsx, hooks/useFormActions.tsx, pages/User/UserList.tsx). They drive form-load and user-CRUD flows, which are single-record/small payloads — NOT the large report grids. The big report tables (BasicTable.tsx / reportConfigs) do their own fetching and do not route through BasicHttpServices. So the finding's phrasing 'every API response' / 'large report/company payloads doubles serialization work' is overstated; the realistic payloads here are small, which makes the perf impact negligible and reinforces Low. The duplicate parse in useFormLoad operates on a single form record. The bug is genuine and the fix is a clean, low-risk cleanup, but its practical cost is smaller than the impact text implies.


<a id="f-no-error-boundary"></a>
#### 🔵 LOW — No React error boundary anywhere; a single render throw blanks the entire admin app

**ID:** `no-error-boundary` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Error handling / resilience &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/App.tsx:114 (RouterProvider has no top-level wrapper); residual gap only on routes lacking errorElement, e.g. Frontend/src/routes/routes.tsx:101-107 (/Certificate)`
- `Frontend/src/App.tsx:30-117`
- `Frontend/src/layouts/app/App.tsx:159-292`

**Problem.** grep for `ErrorBoundary`, `componentDidCatch`, `getDerivedStateFromError` returns zero hits across src. `RouterProvider` is rendered directly under `ConfigProvider`/`HelmetProvider` with nothing to catch render-time exceptions. Report rendering does plenty that can throw on unexpected backend shapes — e.g. custom `render` functions and `value.toString()` in BasicTable (renderCell line 280), money/date parsing in GenericReportPage, and the dashboard card null-deref noted separately.

**Impact.** If any report page throws while rendering (a malformed row, a null where an object is expected, a parsing edge case), React unmounts the whole tree and the operator sees a blank white screen with no recovery path other than a full reload — losing their place and filter state. For an internal government tool used daily, this is a real availability problem.

**Recommendation.** Add a top-level ErrorBoundary around `RouterProvider` (and ideally a per-route one around report content) that renders a fallback with a 'reload / go back' action and logs the error. React Router's `errorElement` per route is a low-effort option that also catches loader errors.

> 🔍 **Verifier note.** Verified: react-router-dom installed version 7.6.2 (Frontend/node_modules/react-router-dom/package.json), react ^19.1.0. errorElement present on 8 routes incl. /Report, /User, /dashboards, /Timeline, /Test, /auth, /. ErrorPage uses useRouteError, console.error, and renders an antd Result with BackBtn/RefreshBtn. Recommendation to add a top-level ErrorBoundary around RouterProvider is still reasonable as defense-in-depth for provider/chrome-level throws, but the daily report-rendering availability concern that drives the High rating is already mitigated.


<a id="f-usefetchdata-stale-closure"></a>
#### 🔵 LOW — useFetchData has a stale-closure bug, no cancellation, and bypasses auth/baseUrl

**ID:** `usefetchdata-stale-closure` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Hooks / data fetching &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/hooks/useFetchData.tsx:8-23`
- `Frontend/src/hooks/useFetchData.tsx:8-23`

**Problem.** `fetchData` is memoized with `useCallback(..., [])` (empty deps) but closes over `url`; the effect depends on `[url]` and calls the stale `fetchData`. Because `fetchData` never changes identity, the lint-disabled effect re-runs on url change but executes a callback that captured the very first `url` — so subsequent url changes fetch the original url. It also uses the raw `fetch` API (not the axios instance), so it sends no auth token and ignores `baseUrl`, and it has no cancellation, so unmount mid-flight triggers a setState-after-unmount and there is no out-of-order protection. Currently consumers only point it at static `../mocks/*.json` fixtures (Pricing/Team/Faqs/Activity/dashboards), so the impact is presently contained, but the hook is in `hooks/index.ts` and is a trap for anyone who points it at a real API.

**Impact.** Today: dashboard/marketing pages fetch leftover mock JSON (dead/placeholder data shipped to a production admin). If reused for real endpoints it will silently fetch the wrong URL, send no auth, and leak setState-after-unmount warnings/races.

**Recommendation.** Add `url` to the useCallback deps (or inline the async in the effect), switch to `axiosInstance` so auth/baseUrl apply, and add AbortController cleanup. Separately, remove or clearly quarantine the `/mocks/*.json` consumers so placeholder data isn't shipped.

**Example:**

```
// current
const fetchData = useCallback(async () => { await fetch(url); ... }, []);
useEffect(() => { fetchData(); }, [url]);

// fixed
useEffect(() => {
  const ctrl = new AbortController();
  (async () => { try { const r = await axiosInstance.get(url, {signal: ctrl.signal}); setData(r.data); } catch(e){ if(!ctrl.signal.aborted) setError(e);} finally { setLoading(false);} })();
  return () => ctrl.abort();
}, [url]);
```

> 🔍 **Verifier note.** Core technical claims (stale closure, raw fetch bypassing axios auth/baseUrl, no cancellation, mock-only consumers, exported in index.ts) all verified. Inaccuracy: there is no eslint-disable in the file. The "fetches wrong url" scenario is currently unreachable because all 12 consumers pass constant string literals, so the stale closure never actually diverges. Recommendation (add url to deps or inline, use axiosInstance, add AbortController, quarantine mock consumers) is valid for hardening, but impact today is contained to placeholder data — hence Low, not Medium.


<a id="f-unbounded-pagesize-client-export"></a>
#### 🔵 LOW — 1000-row page size plus DOM-scraping Excel fallback can load/serialize very large datasets on the main thread

**ID:** `unbounded-pagesize-client-export` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Performance / data fetching &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/My Components/Table/BasicTable.tsx:497`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:236-244`

**Problem.** `pageSizeOptions` offers up to 1000 rows per page, and the local Excel fallback (`exportClientTableToExcel`) builds a workbook by scraping the rendered DOM table via `XLSX.utils.table_to_book`. For wide trade reports a 1000-row page is a large synchronous fetch+render, and the DOM-scrape export only captures the currently rendered page (so it silently exports a partial dataset when used instead of the server `onExcel` path).

**Impact.** Selecting 1000 rows on a heavy report can cause noticeable UI jank/freeze during render and during the synchronous XLSX build. The DOM-based export can also produce incomplete exports without warning if a report ever falls back to it.

**Recommendation.** Cap the interactive page size lower (e.g. 100/200) and rely on the server-side async Excel job (already implemented) for full exports; or stream/virtualize large grids. Ensure the DOM-scrape fallback is never used for reports that have a server export, or remove it.

> 🔍 **Verifier note.** Recommendation is partly redundant: the server-side async export already exists and is wired everywhere except UserList. The actionable residue is (a) optionally lowering the max interactive page size from 1000 (affects all reports' render path) and (b) either giving UserList a server export or accepting that its DOM scrape exports only the current page. Locations are accurate: BasicTable.tsx:497 and BasicTable.tsx:236-244 (fallback gating at 246-250).


<a id="f-dashboard-card-null-deref"></a>
#### ⚪ INFO — Dashboard cards crash on fetch error (null-deref) instead of showing the error

**ID:** `dashboard-card-null-deref` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Error handling / correctness &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/dashboard/default/LatestOrdersCard/LatestOrdersCard.tsx:57 and Frontend/src/components/dashboard/default/RecentUsersCard/RecentUsersCard.tsx:48`
- `Frontend/src/components/dashboard/default/LatestOrdersCard/LatestOrdersCard.tsx:54-60`
- `Frontend/src/components/dashboard/default/RecentUsersCard/RecentUsersCard.tsx`

**Problem.** The Alert renders `error?.toString() || ordersDataError.toString()`. `ordersDataError` comes from useFetchData and is `null` on success. The branch only renders when `ordersDataError || error` is truthy, but if the parent `error` prop is truthy while `ordersDataError` is null, `error?.toString()` could be falsy (e.g. empty string) and then `ordersDataError.toString()` dereferences null and throws. Combined with the absence of an error boundary (separate finding), this throw blanks the page.

**Impact.** An edge-case render crash on the dashboard. Low likelihood given current mock data, but it is an unguarded null deref that, without an error boundary, takes down the whole app.

**Recommendation.** Use optional chaining on both: `error?.toString() ?? ordersDataError?.toString() ?? 'Unknown error'`. Apply the same fix in RecentUsersCard.

> 🔍 **Verifier note.** Confidence High. The fix as recommended is fine to apply (cheap, correct). If the team treats dead UI-template components as out of scope, this could equally be Rejected; I chose Adjusted-to-Info because the buggy expression genuinely exists. Note the location range in the report (54-60) points to the whole Alert block; the actual deref is the second operand on line 57 (LatestOrders) / line 48 (RecentUsers).


<a id="f-persist-no-whitelist"></a>
#### ⚪ INFO — redux-persist configured with no whitelist/blacklist (persists entire root reducer)

**ID:** `persist-no-whitelist` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** State persistence &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/redux/store.ts:17-23`

**Problem.** `persistConfig` sets `key: 'root'` with no `whitelist`/`blacklist` (grep confirms neither appears anywhere), so the whole root reducer is persisted to localStorage. Today the store only holds `theme`, so the blast radius is currently nil. But this is the pattern future slices will inherit: any new slice (user, report filters, fetched data) will be auto-persisted to localStorage by default, including potentially sensitive or large data.

**Impact.** No current data exposure, but a latent footgun: as soon as auth/user/report data moves into Redux it will be silently written to localStorage (same XSS-readable store as the token), with no normalization and unbounded growth.

**Recommendation.** Explicitly set `whitelist: ['theme']` now so persistence is opt-in. When sensitive slices are added, keep them out of the whitelist (or persist to a more appropriate store), and consider normalizing/limiting any persisted server data.

**Example:**

```
const persistConfig: PersistConfig<RootState> = {
  key: 'root', storage, version: 1,
  whitelist: ['theme'], // opt-in; never auto-persist auth/data
};
```


---

### Frontend — Performance

> Dominant problem is zero code-splitting; supporting issues in findings.

<a id="f-no-route-code-splitting"></a>
#### 🟠 HIGH — No route-level code splitting: 159 report pages + 336 KB config in one bundle

**ID:** `no-route-code-splitting` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Bundle size &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/Report/reportRoutes.tsx:1-321`
- `Frontend/src/routes/routes.tsx:169`
- `Frontend/src/Report/config/reportConfigs.ts:13793`

**Problem.** reportRoutes.tsx statically imports all 159 report pages; each imports the single 336,535-byte reportConfigs.ts. Zero React.lazy/Suspense. Whole suite + config land in the initial chunk; users parse all 159 reports before login.

**Impact.** Large initial download/parse on every cold load; bloated login TTI; any report edit busts the chunk for all users.

**Recommendation.** Lazy-load report routes (React.lazy/Suspense); split reportConfigs.ts; add Vite manualChunks.

> 🔍 **Verifier note.** Minor nuance, not a defect in the finding: Vite/Rollup auto-splits *dynamic* imports, but every import here is static, so no automatic route splitting occurs. The phrase "users parse all 159 reports before login" is accurate insofar as the report module graph is statically reachable from routes.tsx (same graph as the login page) with no lazy boundary; the exact share landing in the very first chunk depends on Rollup's default chunking heuristics, but absent manualChunks and lazy boundaries it is bundled into the initial load. Locations are all correct: reportRoutes.tsx (159 imports + route table), routes.tsx:169 (children: reportRoutes), reportConfigs.ts:13793 (last line / size).


<a id="f-xlsx-static-import-basictable"></a>
#### 🟡 MEDIUM — Full xlsx statically imported into shared BasicTable; Excel is server-generated

**ID:** `xlsx-static-import-basictable` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Dependencies &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/My Components/Table/BasicTable.tsx:19`
- `Frontend/src/Report/Page/GenericReportPage.tsx:664-709`

**Problem.** Top-level import of xlsx (hundreds of KB) in the shared table forces it into the main chunk, but reports use a server-side Excel job queue, so the client xlsx path is a rarely-hit fallback.

**Impact.** Full SheetJS downloaded/parsed by every user on any table page; dead weight on the critical path.

**Recommendation.** Lazy-load via await import('xlsx') inside an async exportClientTableToExcel.

> 🔍 **Verifier note.** Locations are accurate. Minor correction: the only true client-xlsx consumer that lacks onExcel is UserList.tsx (it renders BasicTable with no onExcel prop), so the client export path is exercised there, not on report pages. This strengthens (not weakens) the finding — the heavy dep is bundled app-wide for a path that is essentially never the main report flow. Severity Medium is reasonable: it is bundle weight on the critical path of a shared component, but there is no correctness/security impact and the fix is a trivial one-line lazy import. Could justifiably be Low; I leave it at Medium given the 864KB cost is real and ships to every table-page user.


<a id="f-bare-vite-build-config"></a>
#### 🟡 MEDIUM — Vite config has no build/chunking/analysis settings

**ID:** `bare-vite-build-config` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Build configuration &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/vite.config.ts:1-11`

**Problem.** Only dev server + React plugin; no build section, no manualChunks, no visualizer, so heavy deps mix with core code in one chunk.

**Impact.** Cannot diagnose bloat; vendor blob re-downloads on any app change.

**Recommendation.** Add manualChunks for vendors and a bundle visualizer.

> 🔍 **Verifier note.** Severity Medium is reasonable but on the generous side: this is an internal government admin/reporting tool rather than a high-traffic public app, so the practical caching/download penalty affects a limited, likely-repeat user base. The technical facts are fully accurate and the fix is cheap, so I am leaving it at Medium rather than downgrading. Worth noting the report's framing "cannot diagnose bloat" is slightly soft — bloat CAN be observed (the 2.5MB chunk is visible), but it cannot be attributed per-dependency without a visualizer, which is the accurate version of that point.


<a id="f-unvirtualized-large-table"></a>
#### 🟡 MEDIUM — Unvirtualized HTML table exposes a 1000-row page size

**ID:** `unvirtualized-large-table` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Rendering &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/My Components/Table/BasicTable.tsx:362-489`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:497`

**Problem.** Hand-rolled table with no virtualization; pagination offers 1000 rows (15,000-25,000 td nodes in one synchronous render). Defaults to 10, but 1000 triggers heavy layout/paint.

**Impact.** Selecting 1000 causes multi-second jank/frozen tab on low-end machines.

**Recommendation.** Cap page size or virtualize; route bulk extraction to server Excel.

> 🔍 **Verifier note.** Verdict Adjusted only because the per-render td-node estimate is inflated for the typical report (median 6 / mean 9 / max 18 data columns, not the implied 15-25). The underlying defect — unvirtualized hand-rolled table with a selectable 1000-row page size that triggers one large synchronous render — is confirmed at the exact cited locations. Locations are accurate; note the same 1000 option is duplicated at Frontend/src/Report/Page/CompanyProfile.tsx:607. Severity Medium retained (opt-in path, default 10, server Excel export already exists as mitigation).


<a id="f-moment-dead-dependency"></a>
#### 🔵 LOW — moment (~70 KB gzip) declared but never imported

**ID:** `moment-dead-dependency` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Dependencies &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:32`
- `Frontend/package.json:32`

**Problem.** 0 moment imports in src (dayjs is used in 10 files). Not in the bundle while unimported, but a maintenance/supply-chain liability and a footgun: a future import silently adds ~70 KB (moment does not tree-shake).

**Impact.** Bloated install, CVE/license surface, latent 70 KB regression trap.

**Recommendation.** Remove moment; standardize on dayjs; add an ESLint ban.

> 🔍 **Verifier note.** Recommendation to remove moment + standardize on dayjs + add an ESLint no-restricted-imports ban is sound and low-risk: rc-picker treats moment as optional and antd is configured around dayjs, and the react-calendar-timeline reference is type-only. Verify nothing in src uses react-calendar-timeline with moment before removal (none found). No current user-facing performance impact; benefit is dependency hygiene and preventing a future 70 KB regression.


<a id="f-persistgate-provider-order"></a>
#### 🔵 LOW — PersistGate wraps Provider (inverted), bypassing persisted-store gating

**ID:** `persistgate-provider-order` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** State hydration &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/main.tsx:12-20`
- `Frontend/src/redux/store.ts:12-35`

**Problem.** PersistGate is mounted outside Provider; redux-persist needs it inside to delay render until rehydration. Persisted state is tiny (theme slice), so harmless today, but the gate is ineffective and a default-theme flash is possible.

**Impact.** Low now; breaks (render-before-hydrate) if persisted state grows.

**Recommendation.** Swap order: Provider outer, PersistGate inside.

> 🔍 **Verifier note.** Minor precision caveat on the finding's wording: PersistGate consumes the persistor from its own prop (not Redux context), so even in the inverted position it still gates rendering of its children (Provider + App). Thus calling the gate flatly "ineffective" is slightly overstated — the more accurate framing is "non-idiomatic ordering that risks a default-theme flash and would be fragile if persisted state grows." Severity Low is appropriate; no change to severity or location.


<a id="f-oversized-public-assets"></a>
#### 🔵 LOW — ~7 MB unoptimized demo images + 188 KB favicon in public/

**ID:** `oversized-public-assets` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Static assets &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/public/grid-3d.jpg, Frontend/public/showcase/ (4.1 MB), Frontend/public/favicon.ico, plus Frontend/public/grid.jpg, Frontend/public/landing-frame.{jpg,png}, Frontend/public/me.jpg (additional unused template leftovers)`
- `Frontend/public/grid-3d.jpg`
- `Frontend/public/showcase/`
- `Frontend/public/favicon.ico`

**Problem.** public/ holds ~7.2 MB of unused template showcase imagery (grid-3d.jpg 1.5 MB; 200-776 KB PNGs) plus a 188 KB favicon, served verbatim with no Vite optimization.

**Impact.** Multi-MB downloads on pages referencing these; oversized favicon fetched every load.

**Recommendation.** Delete unused imagery; shrink favicon; compress logos.

> 🔍 **Verifier note.** Recommendation should be reframed from "compress/shrink for faster downloads" to "delete unused template assets to slim repo/build output." Verified: grep for 'grid-3d' (1 hit, commented out), 'showcase' (0 hits in code), 'favicon.ico' (0 code hits, only README). index.html icon = /moc-logo.png. vite.config.ts has no asset-optimization plugins.


<a id="f-lodash-full-namespace-import"></a>
#### 🔵 LOW — lodash imported as full namespace in 6 starter-template files

**ID:** `lodash-full-namespace-import` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Dependencies &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/dashboard/learning/StudyStatisticsCard/StudyStatisticsCard.tsx:4`
- `Frontend/src/pages/corporate/Faqs.tsx:3`
- `Frontend/src/pages/userAccount/Help.tsx:11`

**Problem.** import * as _ from 'lodash' for one or two methods each pulls the whole lodash main module (~24 KB gzip). All are leftover starter-template demo files.

**Impact.** Tens of KB of unnecessary JS; keeps dead demo code alive.

**Recommendation.** Delete these files or use per-method imports / lodash-es.

> 🔍 **Verifier note.** Severity Low is correct (arguably could be Info). One caveat: because these files are unrouted dead code with no entry path, a correctly configured Vite production build with code-splitting would likely not include them in any shipped chunk, so the "tens of KB of unnecessary JS shipped to users" impact may be overstated — the concrete cost is lingering dead demo code plus an unnecessarily non-tree-shakeable import pattern that would bloat the bundle if any of these files were ever wired into a route. Recommendation (delete the files, or switch to per-method/lodash-es imports) is sound. This does not change the Low rating, so keeping verdict=Confirmed.


<a id="f-basictable-no-row-cell-memo"></a>
#### 🔵 LOW — BasicTable rows/cells not memoized; full re-render on every state change

**ID:** `basictable-no-row-cell-memo` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Re-render optimization &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/My Components/Table/BasicTable.tsx:264-281`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:398-432`

**Problem.** No React.memo (0 in codebase); renderCell/getRowKey/tbody map run fully every render, so any state change re-renders every visible cell. Compounds the unvirtualized-table issue.

**Impact.** Avoidable CPU per interaction proportional to row*column count; meaningful at 100-1000 rows.

**Recommendation.** Memoize a Row component; wrap renderCell/getRowKey in useCallback.

> 🔍 **Verifier note.** Severity Low is correctly rated and I am leaving it as-is. Mitigating context that caps real-world impact: default pageSize is 10 (line 135), and cells are cheap plain-string renders (value?.toString()), not heavy components — even at the 1000-row max (pageSizeOptions includes 1000, line 497) a full body re-render is a few ms, not a jank source. The "100-1000 rows" framing is plausible only at the top of that range. Minor recommendation nuance: wrapping renderCell/getRowKey in useCallback alone does nothing unless rows are split into a React.memo'd Row component (the memoized-Row part of the recommendation is the actual fix). Compounds the separately-reported unvirtualized-table issue. Files: Frontend/src/components/My Components/Table/BasicTable.tsx (264-281 functions, 398-432 tbody map, line 121 unmemoized export); contributing parent re-render triggers in Frontend/src/Report/Page/GenericReportPage.tsx (lines 524, 726-732).


<a id="f-scrolltotop-smooth-on-every-nav"></a>
#### ⚪ INFO — ScrollToTop runs smooth-scroll on every route change

**ID:** `scrolltotop-smooth-on-every-nav` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Rendering &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/routes/routes.tsx:28-54`

**Problem.** PageWrapper mounts ScrollToTop firing window.scrollTo({behavior:'smooth'}) on each pathname change, competing with layout/paint after a large table renders.

**Impact.** Minor: brief animated scroll + extra layout per navigation.

**Recommendation.** Use behavior:'auto' or react-router ScrollRestoration.

> 🔍 **Verifier note.** Minor caveat: ScrollToTop is mounted at the layout level (per route group like /Report, /User, /Timeline) rather than re-mounted for every individual child route, but since pathname changes on each navigation the useEffect still fires per nav, so the finding holds. The 'extra layout per navigation' impact wording is slightly overstated (scrollTo's smooth animation competes with paint but doesn't synchronously reflow the table), which is why this remains correctly rated Info rather than anything higher.


---

### Frontend — UI / UX & Accessibility

> The report frontend is a config-driven system (GenericReportPage + BasicTable) covering ~134 government trade reports. The Excel-export queue page (ExportsDrive) and the lookup/filter form are well built (loading states, toasts, Popconfirm, skeletons, idle text). However the core report grid has serious UX gaps: it advertises sorting and searching but wires neither, so operators cannot sort or find rows inside reports that return thousands of records. Navigation and i18n are the weakest areas — there is no in-app search over the 134-item sidebar, no internationalization despite a Myanmar/English mandate (and no AntD locale, so all date pickers/pagination render English-only), and the page breadcrumb collapses the current report name to "...". A few correctness/feedback issues round it out: the sign-in form ships hardcoded demo credentials and weak validation, the User list renders a raw password column, and client-side Excel export silently exports only the current page.

<a id="f-userlist-password-column"></a>
#### 🟠 HIGH — User list renders a 'password' column and can export it client-side

**ID:** `userlist-password-column` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Sensitive data display &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/pages/User/UserList.tsx:27-35`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:236-244`

**Problem.** UserList passes displayData={['name', 'password', 'permission', 'isActive', 'id']} (line 29), so the admin user grid renders a column of user passwords directly in the table. Because UserList provides no onExcel, the Excel button falls through to exportClientTableToExcel (BasicTable.tsx:236-244), which serializes the visible DOM table — including the password column — to an .xlsx file.

**Impact.** Anyone who can open the User list sees other users' passwords on screen and can one-click export them to a spreadsheet (shoulder-surfing, screenshots, leaked exports). For a government admin system this is a serious confidentiality exposure even before considering the backend returning the field.

**Recommendation.** Remove 'password' from displayData (never display credentials). Ensure the User API does not return the password field at all. If a credential-management UI is needed, use a reset-password action rather than showing the value.

> 🔍 **Verifier note.** Severity is fair at High. Note an aggravating factor beyond the finding's scope: passwords are stored in plaintext in the DB (User.cs constructor/property, no hashing) and returned by the generic API with no DTO/JsonIgnore — so the recommendation should go further than just removing 'password' from displayData. Add [JsonIgnore] or a DTO projection on the User read path, and ideally hash credentials at rest. Also note BaseAPIController is decorated [AllowAnonymous] at the class level though the Get action has [Authorize], so auth does gate the list endpoint (action-level attribute wins), but the mixed decoration is fragile.


<a id="f-no-table-sort-or-search"></a>
#### 🟠 HIGH — Report grid advertises sorting/searching but wires neither — no way to sort or find rows in large reports

**ID:** `no-table-sort-or-search` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Table UX &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/My Components/Table/BasicTable.tsx:21`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:39-40`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:60`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:64-65`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:157-176`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:191-202`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:376-395`
- `Frontend/src/Report/Page/GenericReportPage.tsx:800-801`

**Problem.** BasicTable defines a full sort/search API (SortOrder type, column.sortable/searchable/filterKey/sortKey, extraFilters, initialSortColumn/initialSortOrder props) and computes searchableColumns/firstDataColumn, but none of it reaches the rendered UI. The query object hardcodes sortColumn:'' and sortOrder:'' (lines 195-196) and filterQuery is destructured value-only with no setter (`const [filterQuery] = useState('')`, line 176), so it can never change. The <thead> renders plain non-interactive <th> headers (376-395) with no click handlers, sort arrows, or search box. extraFilters, sortable, initialSortColumn and initialSortOrder are never referenced in the component body. GenericReportPage passes initialSortColumn/initialSortOrder (800-801) that are silently ignored.

**Impact.** Across ~134 reports — many returning hundreds to thousands of rows (page sizes go up to 1000, BasicTable.tsx:497) — an operator cannot click a column to sort, cannot type to find a company/licence, and cannot reorder by date or amount. They must page through manually or re-export to Excel and search there. This is a major productivity loss for daily ministry reporting work and makes wide financial reports nearly unusable in-app.

**Recommendation.** Either wire the existing API (make <th> clickable to toggle sortColumn/sortOrder and feed them into the query; add a per-column search input that sets filterColumn/filterQuery via a real setState), or replace the hand-rolled <table> with AntD <Table> (as ExportsDrive.tsx already does) which gives sorting, column filters, and sticky headers for free. If sorting/search will genuinely never be supported, delete the dead props so they don't mislead callers.

> 🔍 **Verifier note.** Severity High is defensible but borderline-Medium. Mitigations exist that the finding understates: every report has a substantial pre-load filter form (date ranges, business-type dropdowns, etc.) that narrows results before fetch, plus per-report Excel export for offline sort/search, plus pagination with total counts. This is a productivity/UX gap, not a correctness or data-integrity bug. Given the breadth (all ~135 reports), the misleading dead props (initialSortColumn set in 128 configs but ignored), and real pain on wide multi-thousand-row financial reports, High is acceptable; I would not object to Medium. The recommendation to wire the existing API or migrate to AntD Table is sound and the ExportsDrive precedent is real. One nuance: GenericReportPage forwards query.sortColumn/filterQuery to the backend (L308-311), so the backend likely supports these params (buildLegacyUrl L108-116 also forwards them) — meaning only the front-end UI wiring is missing, making the fix lower-risk than the finding implies.


<a id="f-huge-unsearchable-report-nav"></a>
#### 🟡 MEDIUM — 134-report sidebar has no search/filter, and clicks log to console

**ID:** `huge-unsearchable-report-nav` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Navigation &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/Report/reportNavItems.tsx:223-242`
- `Frontend/src/layouts/app/SideNav.tsx:31-36`
- `Frontend/src/layouts/app/SideNav.tsx:88-97`

**Problem.** reportNavItems builds a single inline AntD Menu of ~134 reports grouped into 23 expandable categories (reportNavItems.tsx:223-242). The SideNav (88-97) renders this menu with no search box or filter to locate a report by name. Every menu click also fires console.log('click ', e) (SideNav.tsx:32) and the menu onClick handler in App layout uses console.log for collapse debugging too.

**Impact.** Finding a specific report among 134 means knowing which of 23 categories it lives in and scrolling/expanding — slow and error-prone for daily use, especially given many near-identical names (e.g. Import vs Border Import Licence variants). Stray console logging clutters the console in production.

**Recommendation.** Add a search/filter input above the report menu that filters items by title (the config already exposes config.title), or provide a searchable command-palette to jump to a report. Remove the debug console.log calls.

> 🔍 **Verifier note.** Minor inaccuracy in the finding's wording: the App-layout console.log calls (App.tsx:115/122) are inside a useEffect on [collapsed], not a menu onClick handler as the problem text implies ("the menu onClick handler in App layout"). The substance (stray collapse-debug console.log in the App layout) is still correct. The genuine menu onClick console.log is in SideNav.tsx:32, which is correctly cited. Locations are all accurate. The config does expose config.title (used at reportNavItems.tsx:212 for the label), so the recommended title-based filter is feasible.


<a id="f-reset-and-logout-no-confirmation"></a>
#### 🔵 LOW — Filter Reset wipes applied filters and Logout signs out with no confirmation

**ID:** `reset-and-logout-no-confirmation` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Feedback / destructive actions &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/Report/Page/GenericReportPage.tsx:717-722`
- `Frontend/src/Report/Page/GenericReportPage.tsx:777-779`
- `Frontend/src/layouts/app/App.tsx:70-83`

**Problem.** resetFilters (717-722) immediately clears the form back to defaults, clears applied filters, and resets the grid to the idle state with no confirmation and no toast — a user who spent time setting many filters on a wide report can lose them in one click. The header Logout menu item (App.tsx:70-83) logs the user out instantly (only a 'signing you out' loading toast) with no confirm step; ExportsDrive by contrast correctly Popconfirms its delete action.

**Impact.** Accidental Reset forces re-entering complex filter sets; accidental Logout interrupts work and forces re-authentication. Inconsistent with the app's own Popconfirm pattern used for export deletion.

**Recommendation.** Add a confirm (Popconfirm/Modal.confirm) to Logout, and consider confirming Reset only when filters have been applied (or show an undo toast). Align destructive-action affordances across the app.

> 🔍 **Verifier note.** Minor nuance: Logout is partially self-signaling (menu label 'logout' + danger styling), so the strongest part of the finding is the Reset case and the cross-app inconsistency with the established Popconfirm pattern. Severity Low is appropriate; recommendation (add Popconfirm/Modal.confirm to logout, optionally confirm/undo on Reset only when filters applied) is reasonable and consistent with the existing ExportsDrive pattern.


<a id="f-breadcrumb-collapses-current-page"></a>
#### 🔵 LOW — Page breadcrumb shows '...' instead of the report name, using the raw URL segment

**ID:** `breadcrumb-collapses-current-page` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Navigation &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/PageHeader/PageHeader.tsx:16, Frontend/src/components/PageHeader/PageHeader.tsx:31-43`
- `Frontend/src/components/PageHeader/PageHeader.tsx:16`
- `Frontend/src/components/PageHeader/PageHeader.tsx:31-52`

**Problem.** PageHeader builds breadcrumbs from location.pathname.split('/'), i.e. the controllerName URL segment (e.g. 'ImportLicenceTotalValueLicencesReport'), not the human title. For the last crumb it renders '...' whenever the text length > 10 and only puts the real text in a hover Tooltip (lines 36-43). Because virtually every report controllerName exceeds 10 characters, the breadcrumb for nearly every page reads 'Home / Report / ...'. Line 16 also has a dead/buggy initializer (`[...'Home', ...]` spreads the string into individual letters) that is immediately overwritten by the effect.

**Impact.** Operators lose the breadcrumb as an orientation aid — every report looks identical ('...') in the breadcrumb, so users cannot tell at a glance which report they are on, and middle crumbs show machine names like 'Report' rather than readable categories. Confusing for a system with 134 similarly named reports.

**Recommendation.** Show the actual report title for the current crumb (the page already has config.title) and never truncate it to '...'; if space is a concern use CSS ellipsis with the full text still in the DOM/title attribute. Map URL segments to human labels for intermediate crumbs. Remove the dead line-16 initializer.

> 🔍 **Verifier note.** All code claims hold up exactly. The dead line-16 initializer, the '...'-when->10-chars truncation, the hover-only Tooltip, and the raw-URL-segment breadcrumb source are all real. Severity adjusted down to Low because the page's human title is always rendered immediately below the breadcrumb (config.title via PageHeader line 736 -> Typography.Title), so the orientation impact the finding leans on is materially mitigated and the defect is cosmetic/non-functional. The finder's note that middle crumbs show 'Report' rather than a readable category is accurate but minor. Fix is low-effort and worthwhile (use config.title for the current crumb, CSS ellipsis instead of '...', map segments to labels, drop the dead line-16 init).


<a id="f-signin-hardcoded-demo-creds"></a>
#### 🔵 LOW — Sign-in form prefills hardcoded demo credentials and has weak validation/feedback

**ID:** `signin-hardcoded-demo-creds` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Form UX &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/pages/authentication/SignIn.tsx:121-125 (prefilled creds), :133-141 (email field validation), :39,52-57,64 (toast + console.log); related AuthContext.tsx:36-65 (login swallows error, prevents specific failure messaging)`
- `Frontend/src/pages/authentication/SignIn.tsx:121-125`
- `Frontend/src/pages/authentication/SignIn.tsx:133-141`
- `Frontend/src/pages/authentication/SignIn.tsx:38-58`

**Problem.** The login form ships initialValues { email: 'demo@email.com', password: 'demo123', remember: true } (121-125), so the production sign-in page renders prefilled credentials. The email field is a plain <Input> with only a 'required' rule — no email-format validation and no type/autoComplete='username' (133-141). On failure the handler shows a generic 'Login fail' toast (lines 52-57) with no reason, and logs values/errors to console (39, 64).

**Impact.** A government login screen presenting prefilled demo credentials looks unprofessional and untrustworthy and can confuse real users (they may submit demo creds). Lack of email validation lets malformed input hit the API; the vague 'Login fail' gives users no idea whether it was a wrong password, locked account, or server error; console logging of submitted values is a hygiene concern.

**Recommendation.** Remove the prefilled initialValues (leave fields blank). Add an email-type validation rule and autoComplete attributes. Surface the server's actual error reason in the toast (e.g. invalid credentials vs. server unavailable) and drop the console.log of credentials.

> 🔍 **Verifier note.** The recommendation to "surface the server's actual error reason" is correct but cannot be done in SignIn.tsx alone — AuthContext.login() must be changed to return the error/status instead of a bare boolean. Severity reduced from Medium to Low because these are UX/professionalism/hygiene issues with no functional or security breakage. Confidence High: code matches the finding verbatim.


<a id="f-client-excel-exports-current-page-only"></a>
#### 🔵 LOW — Client-side Excel export silently exports only the current page, not the full report

**ID:** `client-excel-exports-current-page-only` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Export UX &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/My Components/Table/BasicTable.tsx:236-262 (exportClientTableToExcel + handleExcel fallback; button label line 330) and Frontend/src/pages/User/UserList.tsx:27-35 (the only live caller of the fallback). MemberRegistrationReport.tsx should be REMOVED from locations — it passes onExcel and uses the server-side path, not the buggy DOM path.`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:236-262`
- `Frontend/src/pages/User/UserList.tsx:27-35`
- `Frontend/src/Report/Page/MemberRegistrationReport.tsx`

**Problem.** When a caller does not pass onExcel, handleExcel calls exportClientTableToExcel, which runs XLSX.utils.table_to_book on the rendered DOM table (236-244). The DOM only contains the current page's rows (pagination is server-side), so the resulting workbook contains just that page. There is no warning that the export is partial, and the button label is the same 'Excel' as the full server-side queue export used by GenericReportPage. UserList is one live caller that uses this path.

**Impact.** An operator on page 1 of a multi-thousand-row list clicks Excel and gets a 10-row file believing it is the whole dataset — silent data loss that could lead to incorrect government reporting decisions.

**Recommendation.** Route all exports through the server-side job queue (as GenericReportPage does), or, for the few client-side cases, fetch the full dataset before building the workbook and/or clearly label the button 'Export current page'. Disable the fallback for paginated tables.

> 🔍 **Verifier note.** The recommendation is still reasonable (route through server queue, or relabel/disable the client fallback for paginated tables). Practically, the cleanest fix is to make the fallback safe or remove it, since the only consumer (UserList) is paginated server-side. Note also: UserList renders a 'password' column, which is a separate concern not in scope here but worth flagging. Remove MemberRegistrationReport.tsx from the finding's locations — it is not affected.


<a id="f-table-a11y-and-sticky-header"></a>
#### 🔵 LOW — Report table lacks header scope/caption for screen readers and has no sticky header on tall/wide grids

**ID:** `table-a11y-and-sticky-header` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Accessibility &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/My Components/Table/BasicTable.tsx:362-395`
- `Frontend/src/components/My Components/Table/style.css:52-56`
- `Frontend/src/components/My Components/Table/style.css:112-139`

**Problem.** BasicTable renders a raw <table> with <th> cells that have no scope="col" attribute and no <caption> (362-395), so screen readers cannot reliably associate data cells with their column headers. The table container is overflow-x:auto (style.css:52-56) but there is no position:sticky on thead and no sticky first column, and reports can have many wide columns (reportConfigs.ts defines 1200+ columns across reports). With up to 1000 rows per page (BasicTable.tsx:497) the header scrolls out of view.

**Impact.** Assistive-technology users get an unlabeled grid of numbers; sighted users scrolling a wide/tall financial report lose the column headers and row context, increasing the chance of misreading values in a government report.

**Recommendation.** Add scope="col" to header <th> and a visually-hidden <caption> with the report title; apply position:sticky; top:0 to thead (and optionally sticky the No/first column) so headers stay visible while scrolling.

> 🔍 **Verifier note.** Severity Low is appropriate and accurate: this is an accessibility/UX enhancement, not a functional or security defect, and the app is an internal government admin reporting tool rather than a public-facing site. Every concrete claim in the finding holds up against the actual code. The two CSS line ranges are both relevant (52-56 = overflow container; 112-139 = the ReportViewer thead styling where a position:sticky;top:0 rule would be added). The recommendation (scope="col", visually-hidden caption, sticky thead/first column) is sound. No corrections to location needed.


<a id="f-dark-theme-dead-and-tables-hardcode-light"></a>
#### 🔵 LOW — Dark theme is supported in code but has no UI toggle, and report tables hardcode light colors

**ID:** `dark-theme-dead-and-tables-hardcode-light` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Theming &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/redux/theme/themeSlice.ts:15-21`
- `Frontend/src/App.tsx:90-93`
- `Frontend/src/components/My Components/Table/style.css:24-31`
- `Frontend/src/components/My Components/Table/style.css:43-56`
- `Frontend/src/components/My Components/Table/style.css:112-120`

**Problem.** A toggleTheme reducer exists and App.tsx switches AntD between darkAlgorithm/defaultAlgorithm (90-93), but toggleTheme is never dispatched anywhere in the UI (no theme switch control), so dark mode is unreachable dead code. Separately, the report table CSS hardcodes light values (.container background #ffffff, th background #FAFAFA/#e4e4e4, color #000000 — style.css:24-31, 43-56, 112-120), so even if dark mode were enabled the report grids would render black-on-white, ignoring the theme.

**Impact.** A persisted Redux/redux-persist 'dark' value (or any future toggle) would produce an inconsistent, partly-broken UI where chrome is dark but report tables stay white. Low impact today because the toggle is unreachable, but it is latent debt and confusing dead code.

**Recommendation.** Either remove the unused dark-theme code path, or expose a theme toggle and convert the hardcoded table colors in style.css to AntD theme tokens (CSS variables) so the grid follows the active algorithm.

> 🔍 **Verifier note.** Severity Low is appropriate and the finding does not overstate impact — it explicitly notes the toggle is unreachable today and impact is latent debt/dead code. Cited line ranges all match real code. Minor nuance: the colors that most directly govern the report grid are in the .table-container block (style.css:112-139, partially cited at 112-120), while :24-31/:43-56 cover the more generic global table and .container wrapper; both are accurate, so no location correction is needed. Recommendation (remove dead path, or expose a toggle plus tokenize the table CSS) is sound.


<a id="f-no-i18n-and-no-antd-locale"></a>
#### 🔵 LOW — No internationalization and no AntD locale despite Myanmar/English requirement — all UI chrome is English-only

**ID:** `no-i18n-and-no-antd-locale` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** i18n &nbsp;·&nbsp; **Effort:** Large &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/App.tsx:32-94; Frontend/src/components/My Components/Table/BasicTable.tsx:494-496; Frontend/src/Report/Page/GenericReportPage.tsx:425-471,770-780`
- `Frontend/src/App.tsx:32-94`
- `Frontend/src/Report/Page/GenericReportPage.tsx:425-471`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:494-510`
- `Frontend/src/Report/config/reportConfigs.ts`

**Problem.** There is no i18n framework (no react-i18next/useTranslation/i18n anywhere) and ConfigProvider (App.tsx:32-94) sets no `locale`, so AntD DatePicker, RangePicker, Pagination ('x-y of N total'), Empty, and Popconfirm all render their built-in English strings. All filter labels, button text ('Filter', 'Reset', 'Excel'), report titles, and the state/status option lists in reportConfigs.ts are hardcoded English. Myanmar script appears only once in the entire config — inside a code comment (reportConfigs.ts:6556) — never in user-facing UI. The project mandate (CLAUDE.md) requires Myanmar/English parity with the old Tradenet 2.0 RDLC reports, some of whose headers/columns are Myanmar.

**Impact.** Myanmar-speaking ministry staff get an all-English admin UI with no language option; date pickers and pagination cannot be localized; and reports cannot reproduce the Myanmar column/header text the old system showed, breaking the parity requirement the codebase is explicitly built around.

**Recommendation.** At minimum pass a `locale` to ConfigProvider (AntD ships my_MM / en_US locales) so date/pagination/empty chrome can localize. For real parity, introduce a lightweight i18n layer (react-i18next or a static dictionary keyed off the report config) so titles, filter labels, and column headers can carry both English and Myanmar strings as the RDLC originals do.

> 🔍 **Verifier note.** Real on the no-locale/no-i18n/English-chrome axis (verified verbatim), but impact narrative exaggerated. CompanyProfile.tsx refutes the no-Myanmar-headers and broken-parity claims. Actionable low-priority fix: pass locale (my_MM/en_US) to ConfigProvider to localize date pickers and pagination. reportConfigs.ts was cited without a line range; its only Myanmar (line 6556) is a comment, consistent with the finding.


---

### Frontend — Code Quality

> The frontend is a React 19 / TS 5.8 / Ant Design 5 app built on the "antd-multi-dashboard" template, with a strong, modern strict-mode tsconfig and Prettier. However, code quality is undermined by three structural problems: (1) the lint gate is silently broken — ESLint 9 is installed but configured with a legacy .eslintrc.cjs and an unsupported `--ext` flag, so `npm run lint` errors out without linting and 172 real errors / 9 warnings (mostly `no-explicit-any`, missing-deps, unused vars) go undetected; (2) roughly half the shipped code is dead template demo material (94 dashboard components, 64 stories, 28 mock JSON files, a literal `SideNav copy.tsx`) that accounts for nearly all the `any`/`@ts-ignore` usage and pads the bundle of a government production app; (3) the data layer has genuine bugs and anti-patterns — a `useFetchData` hook with a stale-closure dependency bug that bypasses auth, a `BasicHttpServices` layer that lies to the type system by casting every response to `PaginationType` and deep-clones via `JSON.parse(JSON.stringify())`, and an `AnyObject` escape-hatch type. The folder named with a literal space ("My Components") is an import-fragility hazard. Strict mode passes `tsc` only because `any` and `@ts-ignore` suppress the errors it would otherwise catch.

<a id="f-broken-eslint-gate"></a>
#### 🟡 MEDIUM — ESLint 9 paired with legacy .eslintrc + unsupported --ext flag: lint gate runs nothing

**ID:** `broken-eslint-gate` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Tooling / CI quality gate &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:11`
- `Frontend/.eslintrc.cjs:1`
- `Frontend/package.json:69`

**Problem.** ESLint 9.28.0 is installed (package.json:69) but config lives in the legacy `.eslintrc.cjs` flat-config-incompatible format, and the lint script is `eslint . --ext ts,tsx --report-unused-disable-directives --max-warnings 0`. Running the documented command fails: ESLint v9 dropped `--ext` and requires `eslint.config.js`, so it prints "ESLint couldn't find an eslint.config.(js|mjs|cjs) file" and lints nothing. Only by forcing the deprecated `ESLINT_USE_FLAT_CONFIG=false` env var does it run, and then it reports 181 problems (172 errors, 9 warnings).

**Impact.** The team's only static-analysis quality gate is dead. CI either skips lint or passes it vacuously, so 172 real errors (66+ `no-explicit-any`, unused vars, missing hook deps, empty patterns) accumulate undetected. New regressions in this government reporting app land with no automated review.

**Recommendation.** Migrate to ESLint flat config: create `eslint.config.js` using `typescript-eslint`, `eslint-plugin-react-hooks`, and `eslint-plugin-react-refresh`, delete `.eslintrc.cjs`, and change the script to `eslint src` (no `--ext`). Then triage the 172 errors. Verify `npm run lint` exits non-zero on a seeded violation before trusting CI.

**Example:**

```
// current (package.json) — fails under ESLint 9
"lint": "eslint . --ext ts,tsx --report-unused-disable-directives --max-warnings 0"
// fixed: eslint.config.js (flat) + script
"lint": "eslint src --report-unused-disable-directives --max-warnings 0"
```

> 🔍 **Verifier note.** Core defect is real and fully reproducible (exit-2 failure + 172 errors under legacy mode). Adjusting because: (a) severity should be Medium — pure quality-gate/tooling issue, no functional impact; (b) the "CI passes lint vacuously" framing is inaccurate — verified that no CI workflow runs `npm run lint`, so the lint step simply doesn't exist in CI, compounding the broken local command. Locations are all correct. Also note the legacy config extends plugin:storybook/recommended, so a flat-config migration must include eslint-plugin-storybook (already a devDependency) in addition to the plugins the finder listed.


<a id="f-dead-template-code"></a>
#### 🟡 MEDIUM — Half the codebase is unused antd-multi-dashboard template demo code shipped to production

**ID:** `dead-template-code` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Dead code / maintainability &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/dashboard/`
- `Frontend/public/mocks/`
- `Frontend/src/layouts/app/SideNav copy.tsx:1`
- `Frontend/package.json:2`

**Problem.** The app retains the entire `antd-multi-dashboard` starter template: 94 components under `src/components/dashboard/`, 64 `.stories.tsx` files, 28+ mock JSON files in `public/mocks/`, and demo pages (Projects, corporate, social, marketing, logistics). None are referenced by `src/Report` or `src/routes` (grep returned 0 references from report/routes into `components/dashboard`). All 12 `@ts-ignore` comments and nearly all high-`any` files (CampaignsCard, CampaignsAdsCard, TransactionsCard) live exclusively in this dead template code. `src/layouts/app/SideNav copy.tsx` (285 lines) is an editor-duplicate file imported by nothing. package.json still calls itself `antd-multi-dashboard`.

**Impact.** A government trade-reporting system ships thousands of lines of fake marketing/social/logistics dashboards and mock data, inflating bundle size, attack surface (mock JSON, demo routes), and onboarding confusion. It also pollutes lint/grep results, hiding real issues in noise. Reviewers cannot tell template scaffolding from production code.

**Recommendation.** Delete the unused template tree (`components/dashboard`, demo pages and their routes, `public/mocks`, `SideNav copy.tsx`) and the stories for components you do not maintain. Rename the package and update README. Run the build after removal to confirm nothing real depended on it.

> 🔍 **Verifier note.** Two minor inaccuracies that do not change the verdict: (1) the finding says "94 components under src/components/dashboard" — actual is 47 component .tsx files (118 files total including stories/assets/index). (2) Bundle-size impact is overstated: this is a Vite build, so unimported/unrouted demo components are tree-shaken out of the production JS bundle. The mock JSON in Frontend/public/ IS shipped verbatim (served as static assets), so the attack-surface/footprint concern there is real but small. The dominant and fully-valid impacts are maintainability, onboarding confusion, and lint/grep noise that hides real issues. Medium severity is appropriate for a dead-code/maintainability finding; not security-critical (the demo routes/pages are not even mounted in the router, so "demo routes" attack surface is essentially nil). Recommendation to delete the template tree, rename the package, and rebuild is sound.


<a id="f-any-and-anyobject-escape-hatch"></a>
#### 🟡 MEDIUM — Pervasive `any` (66 sites) and an AnyObject catch-all type defeat strict mode

**ID:** `any-and-anyobject-escape-hatch` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Type safety &nbsp;·&nbsp; **Effort:** Large &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/types/AnyObject.ts:1 (core defect); Frontend/src/services/BasicHttpServices.ts:14-52; Frontend/src/Report/Page/GenericReportPage.tsx:653-654,788; Frontend/src/components/My Components/Table/BasicTable.tsx:33,48,121`
- `Frontend/src/types/AnyObject.ts:1`
- `Frontend/src/hooks/useFetchData.tsx:4`
- `Frontend/src/components/dashboard/default/CampaignsCard/CampaignsCard.tsx:1`

**Problem.** tsconfig has `strict: true` (good), but it is widely circumvented: 66 `: any` annotations across src, a `types/AnyObject.ts` that opens with `/* eslint-disable @typescript-eslint/no-explicit-any */` and defines `{[x:string]:any}`, and 12 `@ts-ignore` comments. `AnyObject` is the return type threaded through the entire data/table layer (`BasicHttpServices`, `BasicTable`, `GenericReportPage`), so report rows are effectively untyped. `tsc --noEmit` currently exits 0 only because these escape hatches suppress the errors strict mode would raise.

**Impact.** The benefit of strict mode is nullified along the core report data path. Field renames, missing properties, and shape mismatches between the .NET backend DTOs and the React tables compile cleanly and only surface as runtime bugs or blank/incorrect report cells.

**Recommendation.** Replace `AnyObject` on real (non-template) paths with generated/handwritten DTO interfaces matching the backend models; make `BasicHttpServices`/`useFetchData`/table generics carry `<T>`. Add `@typescript-eslint/no-explicit-any` as an error in the new flat config and burn down the 66 sites, starting with `src/Report` and `src/services`.

> 🔍 **Verifier note.** Severity Medium is correct: this is a latent-bug/maintainability risk (backend DTO renames or shape drift compile cleanly and surface as blank/incorrect report cells at runtime), not a correctness or security break. The actionable fix is narrower than stated: replace AnyObject with real DTO interfaces on BasicHttpServices/GenericReportPage/BasicTable and thread <T> generics; the dashboard/* any sites are vendor template noise and low-value to burn down. The recommendation should target .eslintrc.cjs (classic config), not a flat config, and could simply promote the existing no-explicit-any warning to error.


<a id="f-duplicate-date-and-dead-moment"></a>
#### 🔵 LOW — moment is a dependency but unused; dayjs date-format helpers duplicated across report pages

**ID:** `duplicate-date-and-dead-moment` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Dependencies / duplication &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:32`
- `Frontend/src/Report/Page/GenericReportPage.tsx:110`
- `Frontend/src/Report/config/reportConfigs.ts:1`

**Problem.** `moment@2.30.1` is declared in package.json but a full-tree grep finds zero `moment` references in `src` — it is pure dead weight (moment is also officially in maintenance mode). The codebase standardized on `dayjs` (10 files), but date-formatting logic is re-implemented ad hoc: a `formatDate` helper in GenericReportPage.tsx:110 plus 36 scattered `dayjs(...).format('YYYY-MM-DD')` call sites, several with their own null/invalid-date handling.

**Impact.** Shipping an unused ~70KB date library bloats the bundle and invites future contributors to reintroduce a second date system (the inconsistency the system prompt warned about). Duplicated format/parse logic means a formatting fix (timezone, invalid-date display) must be applied in many places and is easy to miss, risking inconsistent dates across reports.

**Recommendation.** Remove `moment` from package.json. Extract the `formatDate`/`formatBoolean` helpers into a single `src/utils/format.ts` and import everywhere instead of inlining `dayjs().format(...)`.

> 🔍 **Verifier note.** Severity Low is correct and unchanged — this is pure code hygiene (one unused dev/runtime dependency plus helper duplication), no functional/security/correctness impact. Verdict is Adjusted solely because the "36 call sites" metric is materially overstated (actual: 8 'YYYY-MM-DD' literals, ~26 total dayjs() calls). Minor wording note: package.json declares the range "^2.30.1", not the pinned "2.30.1". Recommendation (drop moment; centralize a shared format util) is sound, with the caveat that callers currently use at least three different output formats, so a single helper must be parameterized, not a drop-in single-format function. Both cited locations (package.json:32, GenericReportPage.tsx:110) are accurate; reportConfigs.ts:1 is the dayjs import line and is also accurate.


<a id="f-usefetchdata-stale-closure-bug"></a>
#### 🔵 LOW — useFetchData has a stale-closure dependency bug, no auth, and no cleanup

**ID:** `usefetchdata-stale-closure-bug` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** React hooks / data fetching bug &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/hooks/useFetchData.tsx:8-23 (latent only; all 12 call sites pass constant /mocks/*.json URLs; no src/Report/ page uses it)`
- `Frontend/src/hooks/useFetchData.tsx:8`
- `Frontend/src/hooks/useFetchData.tsx:19`
- `Frontend/src/hooks/useFetchData.tsx:23`

**Problem.** `fetchData` is `useCallback(async () => { fetch(url) ... }, [])` — memoized with an empty dependency array while closing over `url`, so it permanently captures the first `url`. The effect `useEffect(() => fetchData(), [url])` re-runs when `url` changes but invokes the stale `fetchData`, so it always refetches the original URL. ESLint flags this (`react-hooks/exhaustive-deps`). It also uses raw `fetch` (not `axiosInstance`), so no JWT Authorization header is attached, and there is no AbortController cleanup, so unmount/url-change mid-flight causes a setState-after-unmount and possible race. `data`/`error` are typed `any`.

**Impact.** Any component relying on this hook to load data for a changing URL silently shows stale or wrong data, and the request is unauthenticated against a JWT-protected API. Race conditions can render one report's data under another's view.

**Recommendation.** Add `url` to the `useCallback` deps (or inline the fetch in the effect), switch to `axiosInstance`, add an `AbortController` and guard state updates in cleanup, and type the result generically (`useFetchData<T>`).

**Example:**

```
// current
const fetchData = useCallback(async () => {
  const response = await fetch(url); // stale url, no auth
  setData(await response.json());
}, []);              // <-- empty deps bug
useEffect(() => { fetchData(); }, [url]);
// fixed
useEffect(() => {
  const ctrl = new AbortController();
  axiosInstance.get<T>(url, { signal: ctrl.signal })
    .then(r => setData(r.data)).catch(setError).finally(() => setLoading(false));
  return () => ctrl.abort();
}, [url]);
```

> 🔍 **Verifier note.** Confirmed the exact code (useCallback empty deps + useEffect [url]), the `any` typing, raw fetch, and missing cleanup. Adjusted down because: (1) every call site uses a constant URL, so the stale-closure refetch bug is unreachable; (2) the hook only fetches public static mock JSON, not the JWT-protected report API, so the "unauthenticated" impact does not apply (an Authorization header would be inappropriate there); (3) no real report page uses this hook. Valid recommendation if these template pages are kept, but it is not a functional defect in the report system.


<a id="f-http-service-type-lying"></a>
#### 🔵 LOW — BasicHttpServices casts every response to PaginationType and deep-clones via JSON round-trip

**ID:** `http-service-type-lying` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Type safety / data layer &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/services/BasicHttpServices.ts:7,14,21,28,41,48`
- `Frontend/src/services/BasicHttpServices.ts:7`
- `Frontend/src/services/BasicHttpServices.ts:14`
- `Frontend/src/services/BasicHttpServices.ts:21`

**Problem.** Every helper does `const responseData: PaginationType = JSON.parse(JSON.stringify(data)); return responseData;`. (1) The `JSON.parse(JSON.stringify())` deep-clone is pointless work on every API call and corrupts non-JSON-safe values (Dates become strings, `undefined` keys dropped). (2) `GetSingle`, `Post`, `Put`, `Delete`, `ToggleUserActive` are declared `Promise<AnyObject>` but internally annotate the parsed result as `PaginationType` — the type system is told the shape is a paginated list even when a single object or void is returned. This is type-lying that strict mode cannot catch because `AnyObject` is `[x:string]:any`.

**Impact.** Callers get no real type checking on API responses; a typo in a field name or a backend shape change compiles cleanly and fails at runtime. The redundant clone adds CPU/GC cost on large report payloads. Date fields silently degrade to strings, which can corrupt the very report data this system exists to render.

**Recommendation.** Make the helpers generic (`Get<T>(url): Promise<T>`), return `resp.data` directly (axios already parses JSON), and drop the `JSON.parse(JSON.stringify())` clone. Replace the `AnyObject`-returning signatures with concrete DTO types.

**Example:**

```
// current
export const Get = async (url: string): Promise<PaginationType> => {
  const resp = await axiosInstance.get(url);
  return JSON.parse(JSON.stringify(resp.data)); // pointless clone
};
// fixed
export const Get = async <T>(url: string): Promise<T> =>
  (await axiosInstance.get<T>(url)).data;
```

> 🔍 **Verifier note.** Real issue, mis-scoped impact. Key correction: the clone/cast only affects User CRUD screens (3 callers), not the report grid, which bypasses BasicHttpServices entirely (GenericReportPage.tsx:654). The 'Dates become strings / corrupts report data' claim does not hold — axios already delivers plain JSON (no Date objects, no custom transformResponse in AxiosInstance.ts) and reports don't route through this file. Note also that callers compound the redundancy: useFormload.tsx:19-21 wraps the already-cloned GetSingle result in ANOTHER JSON.parse(JSON.stringify(...)). Fix is low-risk and worth doing for type hygiene, but it is not a Medium-severity runtime risk.


<a id="f-console-logs-in-prod"></a>
#### 🔵 LOW — 51 console.log/error statements left in production source, including auth and 401 flows

**ID:** `console-logs-in-prod` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Logging hygiene / leftover debug code &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/context/AuthContext.tsx:62`
- `Frontend/src/layouts/app/App.tsx:139`
- `Frontend/src/hooks/useFormActions.tsx:121`
- `Frontend/src/components/My Components/StepForm/StepForm.tsx:37`

**Problem.** 36 files contain 51 `console.*` calls. `AuthContext.login` swallows login failures into `console.log(ex)` (AuthContext.tsx:62), the 401 interceptor logs `console.log('401 Unauthorized: Logging out...')` (App.tsx:139), and `useFormActions` logs full response objects (useFormActions.tsx:121). Several are pure debug leftovers (`console.log(collapsed)`, `console.log(page)`, `console.log(stepContext.applicationNo)`).

**Impact.** On a government system handling trade data, logging response objects and auth/session events to the browser console leaks information to anyone with devtools and clutters production logs. Swallowing the login exception into a console.log hides real auth errors from monitoring.

**Recommendation.** Remove debug `console.*` calls; route genuine error logging through a single logger that is stripped/no-ops in production (or configure Vite `esbuild.drop: ['console']` for prod builds). In `AuthContext.login`, surface the error to the caller/monitoring instead of silently logging.

> 🔍 **Verifier note.** Minor wording nuance: the finding says the login exception is "swallowed" hiding errors "from monitoring" — the catch does return false so the caller (SignIn) can react and the app isn't broken; the real downside is no structured/remote error reporting, not a silent UI failure. Recommendation (remove debug calls, route real errors through a logger, configure Vite esbuild.drop:['console'] for prod, surface login error to caller) is sound and the right fix. Note many of the 51 are dead template code (e.g. SideNav copy.tsx) that could just be deleted.


<a id="f-folder-literal-space-my-components"></a>
#### 🔵 LOW — Core shared components live under a folder with a literal space ("My Components")

**ID:** `folder-literal-space-my-components` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Project structure / import fragility &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/components/My Components/Table/BasicTable.tsx:1`
- `Frontend/src/Report/Page/GenericReportPage.tsx:23`
- `Frontend/src/main.tsx:9`

**Problem.** The most-used shared primitives (BasicTable, BasicForm, AjaxButton, StepForm, TableAction) live in `src/components/My Components/` — a directory name containing a space. Imports read `'../../components/My Components/Table/BasicTable'`. Spaces in path segments are fragile across tooling (URL-encoding to `My%20Components` in some resolvers/source maps, shell scripts, Docker COPY, certain CI runners) and the name is non-descriptive ("My").

**Impact.** Import-resolution and build breakage that is environment-dependent and hard to diagnose; one of these paths already showed up URL-encoded in tooling. Future contributors and codegen tools can silently mis-resolve. The vague name signals leftover scaffolding in a production app.

**Recommendation.** Rename to a no-space, descriptive folder such as `src/components/common/` (or set up a path alias like `@common/`), update the ~6 importing files, and add an alias in `tsconfig.json` + `vite.config.ts` to avoid deep `../../` chains.

> 🔍 **Verifier note.** Verified facts: folder + space exists; 6 importers (main.tsx, GenericReportPage.tsx, ListOfDirectors.tsx, MemberRegistrationReport.tsx, UserPage.tsx, UserList.tsx); no tsconfig/vite aliases. Refuted: no "My%20Components" string anywhere in the tree; no Dockerfile. Recommendation (rename to common/ or add @common alias + update importers) is sound. Severity Low is appropriate; verdict is Adjusted only because the "already showed up URL-encoded in tooling" impact claim is not substantiated by the codebase.


<a id="f-missing-hook-deps"></a>
#### 🔵 LOW — Multiple useEffect/useCallback dependency-array bugs flagged by react-hooks

**ID:** `missing-hook-deps` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** React hooks correctness &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/Report/Page/GenericReportPage.tsx:708`
- `Frontend/src/components/Chat/ChatBox.tsx:68`
- `Frontend/src/hooks/useFetchData.tsx:23`

**Problem.** `react-hooks/exhaustive-deps` reports missing dependencies in several places: `GenericReportPage` useCallback missing `normalizeReportFilters`, `ChatBox` useEffect missing `FixedRoomNo`, plus the `useFetchData` case above and others (`isMobile`, `fetchData`, `dd`, `url`). Because the lint gate is broken (see broken-eslint-gate), none are surfaced in CI.

**Impact.** Effects/callbacks can run with stale values, producing subtle data-staleness and re-render bugs (e.g., the Excel-export callback or chat room subscription using outdated state). These are exactly the bugs that masquerade as 'customer complaints' about wrong report output.

**Recommendation.** After fixing the ESLint config, resolve each `exhaustive-deps` warning by either adding the dependency or wrapping the referenced function in `useCallback`/moving it inside the effect. Do not blanket-suppress; verify the Excel and report-filter paths specifically.

> 🔍 **Verifier note.** Finding is factually correct (locations, line numbers, missing-dep names all reproduced empirically) and Low severity is appropriate. Recommendation is sound. Caveat: the real-world impact is narrower than stated — the report/Excel path warning is benign (static config deps), useFetchData only gets constant literal URLs into mock data, and only the ChatBox edge case is an actual latent bug. Treat as code hygiene to clean up after fixing the eslint gate, not as a likely cause of wrong report output. I did not modify any tracked files (used a temporary flat config that was removed).


<a id="f-xlsx-vulnerable-version"></a>
#### 🔵 LOW — xlsx (SheetJS) 0.18.5 from npm registry has unpatched known advisories

**ID:** `xlsx-vulnerable-version` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Dependency risk &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:44`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:19`

**Problem.** `xlsx@0.18.5` is resolved from the public npm registry (package-lock confirms `registry.npmjs.org/xlsx/-/xlsx-0.18.5.tgz`). This registry line is affected by published advisories: Prototype Pollution (GHSA-4r6h-8v6p-xvw6) and ReDoS (GHSA-5pgg-2g8v-p4x9), with no fix available on npm — SheetJS ships fixes only via their self-hosted CDN. The library is imported directly in `BasicTable.tsx` (`import * as XLSX from 'xlsx'`) and used for client-side report export.

**Impact.** Although Excel generation has largely moved server-side (per the async job queue), the client still bundles a vulnerable parser. If any code path parses untrusted .xlsx/structured input client-side, prototype pollution or ReDoS is reachable; at minimum `npm audit` flags this and it fails security review for a government system.

**Recommendation.** Either move to the patched SheetJS distribution from their CDN (`https://cdn.sheetjs.com/xlsx-latest/...`) pinned in package.json, or, since exports are now server-side, remove the direct `xlsx` import from `BasicTable.tsx` and drop the dependency entirely. Re-run `npm audit` to confirm.

> 🔍 **Verifier note.** Locations are correct and verified. The dependency is present and flagged by tooling, but the vulnerable code paths (workbook parsing) are not used — usage is write-only (table_to_book + writeFile on app-generated DOM). Keep as a low-priority/audit-cleanup item; preferred fix is dropping the now-redundant client import since exports are server-side. If retained, pin SheetJS's CDN build rather than the npm registry tarball.


<a id="f-oversized-report-configs"></a>
#### 🔵 LOW — reportConfigs.ts is a single 13,793-line file holding 134 report definitions

**ID:** `oversized-report-configs` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Maintainability / file size &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/src/Report/config/reportConfigs.ts:1 (13,793 lines, 134 reports); Frontend/src/Report/config/newReportConfigs.ts:1 (420 lines). Note: GenericReportPage.tsx referenced in prose lives at Frontend/src/Report/Page/GenericReportPage.tsx, not Frontend/src/Report/GenericReportPage.tsx.`
- `Frontend/src/Report/config/reportConfigs.ts:1`
- `Frontend/src/Report/config/newReportConfigs.ts:1`

**Problem.** `reportConfigs.ts` is 13,793 lines (134 `apiRoute` entries) in one module, and it imports from a parallel `newReportConfigs.ts` (420 lines) — two overlapping config files with no clear boundary (the 'new' naming is a smell that an in-progress migration was left half-done). The next-largest hand-written file is `GenericReportPage.tsx` at 811 lines.

**Impact.** Editing any single report means loading and diffing a 13.7k-line file; merge conflicts are near-certain when multiple people touch reports, and the parallel old/new config split makes it ambiguous where a given report's truth lives (directly relevant to the report-parity work this repo does). IDE performance and review quality both degrade.

**Recommendation.** Split report configs into per-domain modules (e.g., one file per report group) re-exported from an index, and consolidate `newReportConfigs.ts` into the same scheme so there is one canonical location per report. This can be mechanical since each config is a self-contained object.

> 🔍 **Verifier note.** Recommend the report keep the finding but: (a) drop/fix the "next-largest is GenericReportPage" line (true next-largest is MyanmarRegion.ts at 2,296 lines, itself worth a maintainability note), and (b) reframe newReportConfigs.ts as a separate, cleaner factory-style module aggregated via spread rather than an "overlapping/ambiguous-truth" duplicate. The merge-conflict and editing-friction impact is plausible for this repo given active multi-commit report work, but stays Low because no functional/parity behavior is affected — the parity-work risk is indirect (large diffs), not a correctness hazard.


---

### DevOps, Build & Deployment

> The deployment setup for this government trade-reporting system has serious supply-chain and secrets-management gaps. The most severe issue is that Backend/appsettings.json is committed and currently tracked in git (in HEAD) containing live production SQL Server 'sa' credentials (Server 203.81.66.111,14330, password Pr0fessi0nal@IM2022) for both TemplateDB and TradeNetDB, plus a trivially-guessable JWT signing key — a .gitignore entry was added later but never untracked the file, so the secrets remain in every clone and in history (commit 07d95d8). Containers are single-stage, run as root, have no healthchecks/resource limits, and the Frontend Dockerfile is actually broken (drops build tooling and copies the wrong output dir). There is no CI security scanning, no structured logging/monitoring/alerting, and Swagger UI is served unconditionally in production. Deployment relies on a manual PowerShell robocopy to a Windows share with no environment isolation, secret store, or rollback strategy.

<a id="f-prod-sa-creds-committed-in-git"></a>
#### 🔴 CRITICAL — Live production SQL 'sa' credentials and JWT key committed and still tracked in git

**ID:** `prod-sa-creds-committed-in-git` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Secrets Management &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/appsettings.json:26`
- `Backend/appsettings.json:27`
- `Backend/appsettings.json:10`
- `.gitignore:162`

**Problem.** Backend/appsettings.json is tracked in git (git ls-files lists it; it is present in HEAD) and contains the production database connection strings with the SQL Server 'sa' superuser account in cleartext: Server=203.81.66.111,14330;User ID=sa;Password=Pr0fessi0nal@IM2022 for both TemplateDB and TradeNetDB, plus a JWT signing key of literally 'This is my supper secret key for jwt'. The .gitignore entry 'Backend/appsettings.json' on line 162 was added AFTER the file was committed, and .gitignore does not untrack already-tracked files, so the secrets remain live in the working tree, in HEAD, and across history. Commit 07d95d8 ('fix: update TemplateDB connection string for production environment') is the diff that introduced the production 'sa' password — proving these are real production credentials, not placeholders. The server is also reachable on a public IP with a non-standard port (203.81.66.111,14330).

**Impact.** Anyone with read access to the repo (current/former contributors, anyone who cloned it, any CI/mirror, or anyone who obtains a single clone) gets full DBA control of the live Myanmar Ministry of Commerce trade databases over the public internet: read/modify/delete all trade, licence, and permit data, and via xp_cmdshell-class abuse potentially OS-level access on the DB host. The weak JWT key lets an attacker mint valid admin tokens and bypass all API authentication. This is a total compromise of the system's confidentiality, integrity, and availability and cannot be remediated by deleting the file alone — the credentials must be rotated.

**Recommendation.** 1) Immediately rotate the 'sa' password and the JWT key, and stop using 'sa' for the app — create a least-privilege SQL login. 2) git rm --cached Backend/appsettings.json so it is no longer tracked, commit, then purge it from history (git filter-repo / BFG) and force-push, treating the old secrets as permanently burned. 3) Move all secrets to environment variables / .NET user-secrets in dev and a secret store (e.g. environment-injected config or a vault) in prod; keep only a non-secret appsettings.json + appsettings.Example.json in the repo. 4) Restrict the DB server firewall so it is not exposed to the public internet.

**Example:**

```
// committed in git, Backend/appsettings.json
"TemplateDB":"Server=203.81.66.111,14330;...;User ID=sa;Password=Pr0fessi0nal@IM2022;...",
"JWT": { "Key": "This is my supper secret key for jwt" }

// fixed: keep secrets out of the repo entirely
"ConnectionStrings": { "TemplateDB": "" }   // injected via env / secret store
// + run once:  git rm --cached Backend/appsettings.json  &&  rotate sa password & JWT key
```

> 🔍 **Verifier note.** Severity Critical is correct: tracked-in-HEAD SQL 'sa' (superuser) credentials for a government trade DB on a public IP, plus a trivially guessable JWT signing key used to mint and validate tokens. The remediation (rotate, git rm --cached + purge history, move to env/user-secrets, firewall the DB) is sound. One caveat on the impact text: the xp_cmdshell OS-level escalation is plausible-but-hypothetical (sa permits it if xp_cmdshell is enabled; I did not verify server-side config). This does not weaken the core finding. Server reachability on 203.81.66.111,14330 was inferred from config, not network-tested. Locations are accurate; the JWT-key location is line 10 (finding cited :10), connection strings on :26/:27, gitignore on :162.


<a id="f-frontend-dockerfile-broken-build"></a>
#### 🟠 HIGH — Frontend production Dockerfile is broken: drops build tooling and copies wrong output dir

**ID:** `frontend-dockerfile-broken-build` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Build Reproducibility &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/dockerfile:11`
- `Frontend/dockerfile:17`
- `Frontend/dockerfile:21`

**Problem.** The production Frontend image runs `npm ci --only=production`, which omits devDependencies — but vite, typescript (tsc), and @vitejs/plugin-react-swc are all devDependencies, and the build script is `tsc && vite build`. So `npm run build` on line 17 cannot succeed. Even if it did, line 21 copies `--from=0 /app/build/` into nginx, but Vite outputs to `dist/` (confirmed: Frontend/dist exists, no Frontend/build). The image as written produces no served content / fails to build.

**Impact.** The documented production container image cannot be built or, if the build step is silently ignored, serves an empty site. This forces operators into the ad-hoc deploy.ps1 robocopy path, undermining reproducible/container-based deployment and making rollbacks and environment parity impossible.

**Recommendation.** Use a full install for the build stage and copy the correct output dir, and convert to a real multi-stage build with an explicit Node version pin: `RUN npm ci` (not --only=production), then `COPY --from=build /app/dist/ /usr/share/nginx/html`. Add a tested nginx.conf for SPA routing.

**Example:**

```
# current (broken)
RUN npm ci --only=production
RUN npm run build
COPY --from=0 /app/build/ /usr/share/nginx/html

# fixed
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build
FROM nginx:alpine
COPY --from=build /app/dist/ /usr/share/nginx/html
```

> 🔍 **Verifier note.** Severity High is defensible but sits at the upper edge. The broken artifact is real and reproducible-deploy/parity goals are undermined, but a working alternative deploy path (deploy.ps1 robocopying Frontend/dist) means there is no live outage. Reasonable to keep High given the documented prod compose references this broken image; could equally be Medium since the container path is aspirational rather than the active production mechanism. Locations are all accurate (dockerfile:11, :17, :21). No nginx.conf exists in Frontend (recommendation to add one for SPA routing is valid).


<a id="f-swagger-served-in-production"></a>
#### 🟡 MEDIUM — Swagger UI and OpenAPI spec served unconditionally in production

**ID:** `swagger-served-in-production` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Attack Surface / Deployment Config &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Program.cs:109`
- `Backend/Program.cs:110`
- `Backend/Properties/launchSettings.json:6`

**Problem.** app.UseSwagger() and app.UseSwaggerUI() are called outside the IsDevelopment() guard, so the full API surface (every report, export, and admin endpoint) is documented and interactively callable at /swagger in every environment including production. launchSettings additionally sets launchUrl to 'swagger'.

**Impact.** An unauthenticated attacker can enumerate the entire government API, parameter shapes, and error behavior from production, dramatically lowering the cost of attacks — especially combined with the trivial JWT key (they can craft tokens and exercise every endpoint directly from the Swagger UI).

**Recommendation.** Wrap UseSwagger/UseSwaggerUI inside if (app.Environment.IsDevelopment()), or gate them behind authentication and a non-public route in production.

**Example:**

```
// current: always on
app.UseSwagger();
app.UseSwaggerUI(...);

// fixed
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI(...);
}
```

> 🔍 **Verifier note.** Recommendation is sound: wrap UseSwagger/UseSwaggerUI in if (app.Environment.IsDevelopment()) or gate behind auth + non-public route. Note ASPNETCORE_ENVIRONMENT in launchSettings is 'Development' for local profiles; production environment is set by the host/deployment, and with no guard Swagger is served regardless. The launchSettings:6 location is technically correct but only affects local browser launch, not production exposure — the real defect is purely Program.cs:109-115. The 'unauthenticated attacker can exercise every endpoint directly from Swagger' wording is only true when combined with the separate weak-JWT-key finding; on its own the report/export endpoints reject unauthenticated calls.


<a id="f-compose-hardcoded-sa-password"></a>
#### 🟡 MEDIUM — Hardcoded SQL SA password and personal host path baked into docker-compose

**ID:** `compose-hardcoded-sa-password` &nbsp;·&nbsp; **Phase:** P0 &nbsp;·&nbsp; **Category:** Secrets Management &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `docker-compose.dev.yml:11`
- `docker-compose.dev.yml:14`
- `docker-compose.dev.yml:29`

**Problem.** docker-compose.dev.yml hardcodes the MSSQL SA password (SA_PASSWORD=Saobran131994) directly in the committed compose file and repeats it in the backend ConnectionStrings__DefaultConnection env. It also bind-mounts the database data directory from a developer-specific absolute path (/Users/saobranaung/Database/Template/DB), and uses TrustServerCertificate=True. This file is tracked in git.

**Impact.** A second, different SQL admin password is leaked in the repo. The personal-path volume mount means the compose file is non-portable and will silently fail or mount nothing for any other operator/CI, and bind-mounting raw mssql data dirs across host OSes (this is a macOS path) commonly corrupts the database. TrustServerCertificate=True disables TLS validation, enabling MITM on the DB channel.

**Recommendation.** Move SA_PASSWORD and connection strings into a git-ignored .env consumed via ${SA_PASSWORD}; use a named Docker volume (e.g. mssql-data:) instead of a host-specific absolute bind mount; remove TrustServerCertificate or pin a real CA. Rotate the leaked dev password.

**Example:**

```
# current
environment:
  - SA_PASSWORD=Saobran131994
volumes:
  - /Users/saobranaung/Database/Template/DB:/var/opt/mssql/data

# fixed
environment:
  - SA_PASSWORD=${SA_PASSWORD:?set in .env}
volumes:
  - mssql-data:/var/opt/mssql/data
```

> 🔍 **Verifier note.** All three cited locations (lines 11, 14, 29) are exact and correct. The recommendation (move to git-ignored .env via ${SA_PASSWORD}, use a named volume instead of host bind mount, drop TrustServerCertificate, rotate the dev password) is sound. One caveat for the report: the more impactful secrets exposure is actually Backend/appsettings.json (production SA password Pr0fessi0nal@IM2022 committed in plaintext, also duplicated under Backend/bin/... build output) — that is a separate, higher-severity finding the team should be aware of if not already filed.


<a id="f-cors-allowcredentials-localhost-wildcards"></a>
#### 🟡 MEDIUM — CORS allows credentials with localhost wildcards and a trailing-slash production origin

**ID:** `cors-allowcredentials-localhost-wildcards` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Deployment Config &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Program.cs:135`
- `Backend/Program.cs:149`
- `Backend/Program.cs:153`

**Problem.** The single global CORS policy registers many localhost origins (including the pattern 'http://localhost:*') alongside production origins and finishes with .AllowAnyMethod().AllowAnyHeader().AllowCredentials(). The WithMethods chain also redundantly adds '*'. The same policy (including localhost) is applied in production since there is no environment branch.

**Impact.** Permitting localhost and wildcard-port origins with AllowCredentials in the production policy lets a malicious page running locally (or any attacker who can get a victim to a localhost-bound listener, common in desktop/mobile webview scenarios) make authenticated cross-origin calls to the government API and read responses. Mixing dev origins into the prod policy widens the trust boundary unnecessarily.

**Recommendation.** Define separate CORS policies per environment: in production restrict origins to the exact HTTPS production domains only (no localhost, no '*' methods), and drop AllowCredentials unless cookie auth is actually used (this API uses bearer tokens). Remove the duplicate WithMethods('*').

> 🔍 **Verifier note.** One technical correction to the impact narrative: ASP.NET Core's WithOrigins does NOT support wildcard ports. "http://localhost:*" is treated as a literal origin string matched by exact (case-insensitive) comparison, so it does not actually match arbitrary localhost ports — no browser sends Origin: http://localhost:* . The real exposure comes from the explicitly listed localhost origins (http://localhost, :3000, :5173, :8100), not from a functioning port wildcard. The finder's phrase "the pattern 'http://localhost:*'" overstates this. Severity stays Medium but with a caveat on exploitability: because this API uses bearer tokens rather than cookies, AllowCredentials() does not cause the browser to auto-attach ambient credentials, so a malicious localhost page would still need a valid token to read authenticated responses; the practical attack surface is narrower than the "make authenticated cross-origin calls and read responses" framing implies. Still a legitimate defense-in-depth/hardening issue for a government API (dev/localhost origins should not be in the production policy, and AllowCredentials should be dropped), and the trailing-slash production origin at line 140 is dead weight (origins never include a trailing slash). Locations are accurate as cited.


<a id="f-containers-run-as-root-no-multistage"></a>
#### 🟡 MEDIUM — All containers run as root; backend uses dev SDK image with no multi-stage or .dockerignore

**ID:** `containers-run-as-root-no-multistage` &nbsp;·&nbsp; **Phase:** P2 &nbsp;·&nbsp; **Category:** Container Hardening &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/Dockerfile.dev:2`
- `Backend/Dockerfile.dev:21`
- `Backend/Dockerfile.dev:31`
- `Frontend/dockerfile:20`

**Problem.** No Dockerfile declares a USER directive, so every container runs as root. The only backend Dockerfile (Dockerfile.dev) is built FROM the full dotnet/sdk:8.0 image, runs `dotnet watch run` (a dev server) and `dotnet ef database update` at container start, and there is no .dockerignore in Backend (confirmed) so the entire context — including the secret-laden appsettings.json, bin/, obj/, and EF tooling — is copied into the image. There is no production multi-stage runtime (aspnet) image at all.

**Impact.** A container breakout or RCE in the app runs with root privileges. Shipping the SDK + EF migration tooling + source + secrets in the image massively enlarges the attack surface and image size, and auto-applying `dotnet ef database update` at startup means a compromised or misconfigured container can mutate the live schema. Running `dotnet watch` (hot-reload dev server) as the deployable artifact is not production-safe.

**Recommendation.** Create a proper multi-stage production Dockerfile: build on sdk:8.0, publish, then run on the slim aspnet:8.0 runtime; add `USER app` (or a created non-root user); add a Backend/.dockerignore excluding bin/, obj/, appsettings*.json, .git; do not run dotnet watch or auto EF migrations in production (run migrations as a gated, separate step).

> 🔍 **Verifier note.** Title's 'no multi-stage' applies to Backend only; the Frontend dockerfile IS multi-stage (node->nginx) and is cited only for the root-user issue (line 20 nginx stage), which is valid. All four locations are accurate. Caveat: these are explicitly dev artifacts (Dockerfile.dev, docker-compose.dev.yml) and no production backend Dockerfile exists in the repo — the finding's risk hinges on the dev image being what gets deployed (a reasonable inference since it is the only backend image present). Could justify a higher rating because real live sa credentials are committed in appsettings.json, but that secret-exposure angle is likely covered by a separate hardcoded-secrets finding; Medium for the container-hardening dimension is sound.


<a id="f-no-ci-security-scanning"></a>
#### 🟡 MEDIUM — No CI security scanning or backend CI; main branch excluded from tests

**ID:** `no-ci-security-scanning` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** CI/CD &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/.github/workflows/test.yml:6-12 (branches-ignore main/master on push+pull_request), test.yml:18-20 (Node 16/18 matrix), test.yml:36 (npm test step — file is only 37 lines, so cited :39 overshoots), release.yml:18 (Node 20.18.0)`
- `Frontend/.github/workflows/test.yml:7`
- `Frontend/.github/workflows/test.yml:39`
- `Frontend/.github/workflows/release.yml:1`

**Problem.** The only CI workflows are frontend-only (changeset release, commitlint, test). There is no dependency/secret/SAST scanning (no CodeQL, npm audit, dotnet, Trivy, gitleaks, Dependabot). test.yml is gated with branches-ignore: [main, master] and pull_request branches-ignore main — so nothing runs on the main branch or PRs into it. test.yml targets Node 16/18 while the app builds on Node 20 (release.yml), and runs `npm test` though no test script exists in package.json. There is no CI at all for the .NET backend or its Backend.Tests project.

**Impact.** Vulnerable dependencies, leaked secrets (such as the committed appsettings.json), and backend regressions ship undetected. A secret scanner in CI would have caught the production sa credentials before merge. The main branch — what actually deploys — has zero automated gating.

**Recommendation.** Add gitleaks/secret-scanning and Dependabot to the repo; add CodeQL for JS and C#; run frontend build/lint and `dotnet build`/`dotnet test` on PRs into main on the supported Node 20 / .NET 8 versions; fix or remove the dead `npm test` step.

> 🔍 **Verifier note.** Confirmed at Medium. This is a legitimate CI-hygiene / defense-in-depth gap, not a directly exploitable vulnerability — the leaked sa credentials are a separate standalone finding; missing CI scanning is the preventative control that would have caught them. For an internal government admin tool, Medium is appropriate. The only correction is the line number: test.yml is 37 lines, so location :39 should be :36 (the npm test step) and the branches-ignore gating is at lines 6-12 (covering both push and pull_request, not just one). The substance of every claim holds.


<a id="f-no-monitoring-logging-alerting"></a>
#### 🟡 MEDIUM — No structured logging, monitoring, or alerting; default console logging only

**ID:** `no-monitoring-logging-alerting` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Observability &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/API.csproj:11`
- `Backend/Program.cs:97`
- `Backend/appsettings.json:2`

**Problem.** The backend has no logging/monitoring packages (no Serilog, Application Insights, OpenTelemetry, or Sentry in API.csproj) and Program.cs adds none — only the framework's default console logger configured at Information level. There is no audit logging of who runs which trade reports/exports, no centralized log sink, and no alerting. The Excel export queue and DB-mutating operations have no externally observable health/audit trail.

**Impact.** For a government system handling sensitive trade data, the absence of audit and centralized logging means a breach (e.g. via the leaked sa creds) would be undetectable and uninvestigable, and operators have no visibility into failures, abuse, or the background export worker's health. No alerting means outages and attacks go unnoticed.

**Recommendation.** Add structured logging (Serilog) with a centralized sink, add request/audit logging for report and export endpoints (user id, report, params), and integrate at least basic health and error alerting (e.g. ASP.NET HealthChecks + an APM/monitoring backend).

> 🔍 **Verifier note.** Severity Medium is appropriate: this is an observability/audit/defense-in-depth gap that amplifies other findings (notably the hardcoded sa credentials in appsettings.json) rather than a directly exploitable vulnerability, so not Critical/High on its own. Cited locations are accurate representative anchors (API.csproj:11 = start of the single ItemGroup of PackageReferences; Program.cs:97 = builder.Build() with no prior logging config; appsettings.json:2 = Logging block). Minor nuance: the workers do use the default ILogger, so it is not literally 'no logging at all' — but the claim is specifically about structured/centralized logging, monitoring, alerting, and audit logging, all of which are genuinely absent.


<a id="f-no-healthchecks-no-resource-limits"></a>
#### 🔵 LOW — No container healthchecks or resource limits; restart policies inconsistent

**ID:** `no-healthchecks-no-resource-limits` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Container Orchestration &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `docker-compose.dev.yml:19`
- `docker-compose.dev.yml:35`
- `Frontend/docker-compose.production.yml:1`
- `Backend/Dockerfile.dev:29`

**Problem.** No compose service or Dockerfile defines a HEALTHCHECK, and no service sets CPU/memory limits (no deploy.resources / mem_limit). depends_on is used without condition: service_healthy, so backend starts before the DB is actually accepting connections (only ordering, not readiness). Restart policies are inconsistent: db has restart: unless-stopped but the backend and frontend services in docker-compose.dev.yml have none.

**Impact.** Failed or hung containers are not detected or restarted, a single runaway report/export can starve the host of memory/CPU, and the backend can crash-loop on startup because it boots before SQL Server is ready. This reduces availability and makes incident recovery manual.

**Recommendation.** Add HEALTHCHECK (e.g. an ASP.NET /health endpoint and an nginx/curl check), add depends_on condition: service_healthy for the DB, set mem_limit/cpus (or deploy.resources.limits) per service, and apply a consistent restart policy across all services.

> 🔍 **Verifier note.** Locations are accurate. Note line 19 in the finding points at the `backend:` service key (missing restart) and line 35 at `frontend:` (missing restart); both are correct. depends_on without condition appears at lines 32-33 and 43-44. The db `restart: unless-stopped` is line 17. The most actionable sub-issue is readiness/restart on the backend given its CMD runs an EF migration at startup; resource limits and frontend healthchecks are secondary. Severity Low is correct given the dev/small-deployment scope; no change recommended.


<a id="f-https-hsts-gaps"></a>
#### 🔵 LOW — HTTPS/HSTS configuration gaps: AllowedHosts wildcard, duplicate redirect, dev SSL trust

**ID:** `https-hsts-gaps` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Transport Security &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/appsettings.json:8`
- `Backend/Program.cs:117`
- `Backend/Program.cs:159`
- `Frontend/dockerfile:24`

**Problem.** AllowedHosts is '*' (no host filtering). app.UseHttpsRedirection() is called twice (lines 117 and 159) which is harmless but indicates copy-paste drift, while UseHsts() only runs in non-development with no max-age/preload tuning. The nginx frontend image listens only on port 80 with no TLS config, relying entirely on an undocumented external reverse proxy for HTTPS. Connection strings use TrustServerCertificate=True throughout.

**Impact.** Wildcard AllowedHosts enables host-header attacks (cache poisoning, password-reset/link poisoning patterns). Without an explicit, tuned HSTS policy and with TLS termination left implicit, there is risk of downgrade/MITM if the proxy is misconfigured. TrustServerCertificate=True disables DB TLS validation, allowing MITM on the database channel.

**Recommendation.** Set AllowedHosts to the explicit production hostnames; remove the duplicate UseHttpsRedirection; configure HstsOptions (MaxAge, IncludeSubDomains, Preload); document/codify TLS termination; replace TrustServerCertificate=True with a validated CA certificate for SQL connections.

> 🔍 **Verifier note.** Severity Low is correct and should stay. Caveats for the report: (1) The duplicate UseHttpsRedirection is cosmetic, not a security defect. (2) The AllowedHosts="*" impact is largely theoretical here — no link-poisoning or cache surface exists in this codebase, so it is closer to Info-level hygiene. (3) The strongest real item is TrustServerCertificate=True on remote, internet-exposed DB connections (203.81.66.111) — that is the part worth prioritizing. (4) Out of scope but adjacent and more severe: lines 26-27 hardcode the SQL 'sa' account with a plaintext password in source-controlled appsettings.json, and line 10 has a hardcoded weak JWT signing key — those belong to a secrets-management finding, not this transport one.


<a id="f-deploy-script-manual-robocopy-no-env-isolation"></a>
#### 🔵 LOW — Production deploy is a manual PowerShell robocopy to a Windows share with no env isolation or rollback

**ID:** `deploy-script-manual-robocopy-no-env-isolation` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Deployment Process &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `deploy.ps1:33`
- `deploy.ps1:38`
- `deploy.ps1:50`
- `deploy.bat:4`

**Problem.** Deployment is a developer-run PowerShell script that git pulls, dotnet publishes Release, and robocopies the output to P:\WEBSITES\... shares. deploy.bat launches it with -ExecutionPolicy Bypass. It excludes appsettings.json/web.config from the copy (/XF), so production config is whatever already exists on the target with no versioning. There is no ASPNETCORE_ENVIRONMENT set (defaults to Production, which is fine, but is implicit), no build artifact pinning, no health verification, no atomic swap, and no rollback path. The frontend target folder name is misspelled ('tradenenet-admin-frontend').

**Impact.** Deployments are non-reproducible and operator-dependent: an in-place robocopy over a live site can serve half-deployed files, there is no way to roll back to a known-good build, and the excluded-but-unmanaged appsettings.json means prod secrets live untracked on the box (and the committed copy still leaks them). -ExecutionPolicy Bypass habituates running unsigned scripts.

**Recommendation.** Move to artifact-based CI/CD: build once in CI, produce a versioned artifact, deploy via atomic swap (publish to a new folder + repoint) with a health check and documented rollback. Manage production appsettings via a secret store, set ASPNETCORE_ENVIRONMENT explicitly, and fix the target folder typo.

> 🔍 **Verifier note.** Severity Low is defensible and I did not inflate it. The deployment risks (non-atomic in-place robocopy over a live site can serve half-deployed files, no rollback, operator-dependent/non-reproducible) are real but operational. The more severe issue (sa-credential and JWT-key leak in committed appsettings.json, verified present) is referenced here only as compounding context and is properly the subject of a separate secrets-in-source finding; keeping this devops finding at Low avoids double-counting that severity. The /XF exclusion of appsettings.json is intentional (so a local publish does not overwrite prod config), but it does mean prod config is unversioned on the target — a legitimate point. No corrections needed to location or severity.


---

### Dependencies & Supply Chain

> This government trade-reporting admin system has serious supply-chain hygiene gaps. An offline `npm audit` against the committed lockfile reports 32 advisories on the frontend alone (2 Critical, 12 High, 15 Moderate). The most impactful are in the production runtime path: `xlsx@0.18.5` (the well-known prototype-pollution + ReDoS SheetJS CVEs, used directly in the Excel export code), `axios@1.9.0` (a long list of HTTP-client advisories including SSRF/proxy-credential-leak/prototype-pollution, plus it pulls the Critical `form-data` boundary CVE), `lodash@4.17.21` (prototype pollution / `_.template` code injection), and `protobufjs@7.5.3` pulled transitively by `firebase@11.9.1` (Critical arbitrary-code-execution / DoS). There is no dependency scanning anywhere in the repo — the only CI present is leftover template workflows that test on EOL Node 16/18 and do not run any audit. There is also dead weight: `moment@2.30.1` is a direct dependency with zero imports in the source (the app uses `dayjs` everywhere), and heavyweight single-use deps (firebase, mqtt) inflate the bundle and the vulnerability surface. The backend .csproj versions are internally consistent (EF Core 9.0.6 on net8.0 with JwtBearer 8.0.17 is supported, not a true skew) but have no automated vulnerability scanning either.

<a id="f-axios-vulnerable-1-9-0"></a>
#### 🟠 HIGH — axios 1.9.0 (production HTTP client) carries 20+ advisories incl. SSRF, proxy-credential leak, prototype pollution

**ID:** `axios-vulnerable-1-9-0` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Known-vulnerable dependency &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:26`
- `Frontend/package-lock.json (node_modules/axios version 1.9.0)`

**Problem.** axios is declared "^1.9.0" and resolves to 1.9.0, the app's primary API client. npm audit flags it High with a very long advisory list affecting <1.16.0, including NO_PROXY hostname-normalization SSRF (GHSA-3p68-rc4w-qgx5), Proxy-Authorization credential leak across redirects (GHSA-p92q-9vqr-4j8v, GHSA-j5f8-grm9-p9fc), multiple prototype-pollution gadgets enabling response tampering / credential injection / MitM via config.proxy (GHSA-pf86-5x62-jrwf, GHSA-35jp-ww65-95wh, GHSA-3g43-6gmg-66jw), DoS via missing data-size checks (GHSA-4hjh-wcwx-xvwj), and ReDoS via cookie-name injection (GHSA-hfxv-24rg-xrqf). axios 1.9.0 also depends on the Critical-vulnerable form-data 4.0.2 (see separate finding).

**Impact.** All admin/report API traffic flows through this client. The proxy/NO_PROXY and Proxy-Authorization advisories are directly relevant to a server-side or proxied deployment of a government system, where a credential leak or SSRF can pivot into internal networks. The prototype-pollution gadgets let crafted server responses tamper with subsequent requests. Because the caret range stops below the patched line, a fresh install keeps the vulnerable version.

**Recommendation.** Upgrade axios to >=1.16.0 (latest 1.x) and re-pin: "axios": "^1.16.0". This is a non-breaking minor for the 1.x line and simultaneously resolves the transitive form-data Critical. Run `npm audit fix` afterward and re-run audit to confirm.

> 🔍 **Verifier note.** Impact narrative is partially overstated for THIS app's context. The codebase is a browser-side Vite/React SPA (vite build, @vitejs/plugin-react-swc); AxiosInstance.ts uses axios.create() with localStorage tokens and no proxy/Node adapter, and grep found zero proxy config anywhere in src. Consequently the most alarming impacts the finding emphasizes — NO_PROXY/proxy SSRF "pivot into internal networks," Proxy-Authorization credential leak across redirects, maxContentLength/maxBodyLength stream bypasses, formDataToStream CRLF — are Node.js HTTP-adapter-only and are NOT exploitable in a browser bundle. The finding's phrase "directly relevant to a server-side or proxied deployment of a government system" does not match how this client actually runs. The genuinely applicable residual risk in-browser is the prototype-pollution gadgets (response tampering / header injection / XSRF token leakage) and the DoS/ReDoS items. Also, the npm form-data package isn't used directly in source (browser uses native FormData), though it remains in the dependency tree.  Recommendation is slightly stale: audit shows advisories affecting up to <1.15.1 and an incomplete-fix NO_PROXY advisory (GHSA-pjwm-pj3p-43mv) still affecting 1.15.x, so the patched target should be the latest 1.x (>=1.15.x is insufficient per current advisories), not ^1.16.0 specifically. Upgrading to latest axios 1.x and running npm audit fix remains the correct action and also clears the transitive form-data Critical.


<a id="f-no-dependency-scanning"></a>
#### 🟠 HIGH — No dependency scanning (Dependabot/Renovate/audit) anywhere; only leftover template CI on EOL Node

**ID:** `no-dependency-scanning` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Supply-chain process &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/.github/workflows/test.yml:18-22 (Node 16.x / 18.x matrix)`
- `Frontend/.github/workflows/release.yml:20 (npm install --force)`
- `(repo root: no .github, no dependabot.yml, no renovate.json — confirmed by find)`

**Problem.** There is no Dependabot config, no Renovate config, and no `npm audit` / dotnet vulnerability scan in any pipeline. The only CI present is template leftover under Frontend/.github/workflows: test.yml runs on EOL Node 16.x and 18.x, release.yml runs `npm install --force` (which bypasses peer-dependency safety and can pull unexpected versions), and there is no workflow at the repository root covering the backend at all. The 32 advisories surfaced by a manual offline audit are therefore completely invisible to the team's process.

**Impact.** Vulnerable packages (including the 2 Criticals above) accumulate undetected. `--force` installs in release CI can silently change the dependency tree away from the audited lockfile. Building/testing against Node 16/18 (both end-of-life) means the CI environment itself is unsupported and unpatched. For a government system this is an unmanaged supply-chain risk.

**Recommendation.** Add a repo-root .github/dependabot.yml covering both the npm (Frontend) and nuget (Backend) ecosystems; add an `npm audit --audit-level=high` and `dotnet list package --vulnerable --include-transitive` gate to CI; replace `npm install --force` with `npm ci` against the committed lockfile; and bump the CI Node matrix to a supported LTS (20.x/22.x). Delete the unused template workflows if they are not adapted.

> 🔍 **Verifier note.** Severity is borderline. The finding's "High" rests partly on cross-referenced "2 Criticals / 32 advisories" from a separate vulnerable-dependency finding, not on this finding's own evidence; on its own merits this is a supply-chain *process/hygiene* gap (no scanning + EOL CI Node), not a directly exploitable code defect. Practical exploitability is also tempered: test.yml only runs on non-main branches and would error on the nonexistent `npm test` script, so the "building against EOL Node" impact is largely theoretical, and `npm install --force` in release.yml runs only on push to main. I'd rate this Medium. That said, all factual claims are accurate; if the org weights "unmanaged supply chain for a government system" heavily, High is defensible. One minor wording nit: release.yml is not pure "template leftover" — changesets is genuinely configured — but the cited code (the `--force` line) is correct. The recommendation (repo-root dependabot.yml for npm+nuget, npm audit + dotnet list package --vulnerable gates, replace npm install --force with npm ci, bump Node matrix to 20.x/22.x) is sound and actionable.


<a id="f-react-router-7-advisory"></a>
#### 🟡 MEDIUM — react-router-dom 7.6.2 carries router advisories (DoS / pre-render data spoofing)

**ID:** `react-router-7-advisory` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Known-vulnerable dependency &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:41 (react-router-dom ^7.6.2)`
- `Frontend/package-lock.json (react-router-dom resolved via react-router)`

**Problem.** npm audit flags react-router-dom (Moderate) and its underlying react-router (High) with the range 7.0.0-pre.0 - 7.11.0 covering the installed 7.6.2. The React Router 7.x advisories include a DoS and a pre-render/data-spoofing issue.

**Impact.** The router is core to every admin page. The advisories enable client-side DoS and, depending on usage of data routers/loaders, response/data spoofing affecting what operators see.

**Recommendation.** Upgrade react-router-dom to >7.11.0 (latest 7.x patched line) and re-run audit. This is within the same major, so it should be a low-risk bump; smoke-test routing afterward.

> 🔍 **Verifier note.** Two caveats. (1) Fix version is understated: the recommendation "Upgrade to >7.11.0" is insufficient because several advisory ranges extend beyond it (DoS <7.15.0, single-fetch DoS <7.14.0, turbo-stream RCE <=7.14.1, protocol-relative open redirect <7.14.1). The actual patched line is >=7.15.0. Recommend bumping react-router-dom to >=7.15.0 (still within major 7, low-risk) and re-running audit. (2) Real exploitability for THIS app is on the lower end of the advisory set: it is a pure client-side SPA — vite.config has no SSR config, and src/routes/routes.tsx defines zero loaders/actions (grep count = 0) and only one client-side Navigate. So the worst server/framework-mode advisories (SSR ScrollRestoration XSS, __manifest DoS, single-fetch DoS, turbo-stream RCE) are not exercised; the residually-applicable ones are client-side XSS-via-untrusted-paths and open-redirect. Medium severity is therefore appropriate-to-slightly-generous but not wrong; leaving severity at Medium.


<a id="f-form-data-critical-transitive"></a>
#### 🔵 LOW — Critical form-data 4.0.2 (unsafe random boundary) pulled transitively by axios

**ID:** `form-data-critical-transitive` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Known-vulnerable transitive dependency &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package-lock.json (node_modules/form-data @ line 8872-8885, version 4.0.2, declared by node_modules/axios @ line 6563-6573 as form-data ^4.0.0)`
- `Frontend/package-lock.json (node_modules/form-data 4.0.2, via axios@1.9.0)`

**Problem.** npm audit reports form-data as Critical (GHSA-fjxv-7rqg-78g4): versions >=4.0.0 <4.0.4 choose the multipart boundary using an unsafe (predictable) random function. `npm ls form-data` confirms the path antd-multi-dashboard -> axios@1.9.0 -> form-data@4.0.2.

**Impact.** A predictable multipart boundary lets an attacker who can influence part of a multipart body craft content that injects additional parts (parameter/part smuggling) into outbound requests. In a file/report-upload flow this can corrupt or spoof submitted data. It is rated Critical by the advisory database.

**Recommendation.** This is fixed transitively by upgrading axios to >=1.16.0 (which depends on form-data >=4.0.4). If staying on axios 1.9.x temporarily, add an npm `overrides` entry forcing form-data >=4.0.4. Then re-run npm audit to confirm the Critical clears.

**Example:**

```
// package.json — interim override if axios upgrade is deferred
"overrides": {
  "form-data": ">=4.0.4"
}
```

> 🔍 **Verifier note.** Verified at lockfile lines: axios 6563-6573, form-data 6570 (edge) and 8872-8885 (resolved node 4.0.2). The vulnerable version, the transitive path via axios@1.9.0, and the suggested fix are all factually correct. Severity adjusted Critical -> Low because (1) browser SPA uses native FormData, not the npm package, so the vulnerable path is not exercised at runtime, and (2) no direct multipart/upload code exists in src to enable the described smuggling. Still recommend remediating (axios >=1.16.0 or overrides form-data >=4.0.4) to clear npm audit hygiene.


<a id="f-moment-unused-direct-dep"></a>
#### 🔵 LOW — moment 2.30.1 is a direct dependency with zero source imports (app uses dayjs)

**ID:** `moment-unused-direct-dep` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Unused / duplicate-purpose dependency &nbsp;·&nbsp; **Effort:** Trivial &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:32 (moment)`
- `Frontend/package.json:28 (dayjs)`
- `Frontend/src (grep for moment imports returns nothing; dayjs imported in 10 files)`

**Problem.** package.json lists both moment ^2.30.1 and dayjs ^1.11.13. A source-wide grep finds zero `import ... moment` / `from 'moment'` references, while dayjs is imported across ~10 report/page files and is also what AntD 5 uses internally (via rc-picker 4.11.3). The moment entries elsewhere in the lockfile are optional peer deps of unrelated libs, not real usage. moment is therefore dead weight, and it is also in long-term maintenance mode (no new features, project recommends migrating off it).

**Impact.** Unnecessary bundle weight (moment + its locale data is large and not tree-shakeable) and an extra maintenance/vulnerability surface for a library that does nothing. It also creates confusion about which date library is canonical.

**Recommendation.** Remove moment from dependencies (npm uninstall moment) and standardize on dayjs, which AntD already requires. Verify the build/storybook still pass.

> 🔍 **Verifier note.** Severity Low is correct — this is dependency hygiene, not a correctness/security bug. One caveat: the stated "unnecessary bundle weight" impact is slightly overstated; since nothing in src imports moment, Vite/Rollup tree-shaking will not include it in the production bundle. The genuine costs are install-time/node_modules weight, an extra audit/vulnerability surface, and confusion over which date library is canonical. Recommendation (npm uninstall moment, standardize on dayjs) is sound; verifying build + storybook afterward is appropriate since this app uses Storybook.


<a id="f-lodash-prototype-pollution"></a>
#### 🔵 LOW — lodash 4.17.21 (direct) vulnerable to prototype pollution and _.template code injection

**ID:** `lodash-prototype-pollution` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Known-vulnerable dependency &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:31 (direct dep lodash ^4.17.21); Frontend/package-lock.json:10086-10091 (node_modules/lodash 4.17.21)`
- `Frontend/package.json:31`
- `Frontend/package-lock.json (node_modules/lodash 4.17.21)`

**Problem.** lodash is a direct dependency at 4.17.21. Newer advisories now cover this version: prototype pollution in _.unset/_.omit (GHSA-xxjr-mmjv-4gpg, GHSA-f23m-r3pf-42rh, affected <=4.17.23) and code injection via _.template import key names (GHSA-r5fr-rjxr-66jc, affected <=4.17.23). 4.17.21 was historically the 'safe' pin but is now flagged High by npm audit.

**Impact.** If any of the affected functions process attacker-influenced keys/paths (common in report-filter / object-merge code), the app is exposed to prototype pollution and, for _.template usage, client-side code injection.

**Recommendation.** Upgrade to the patched lodash release (>4.17.23 once published) or, as remediation today, replace the handful of lodash uses with native JS / per-method packages and drop the full lodash dependency. Add an `overrides`/resolution if a transitive copy lingers. Audit the codebase for _.template, _.unset, _.omit usage specifically.

> 🔍 **Verifier note.** Recommendation is sound: upgrade lodash to >=4.18.0 (the fix per CVE-2026-4800). Minor factual error in the finding: the prototype-pollution advisories (GHSA-f23m-r3pf-42rh, GHSA-xxjr-mmjv-4gpg) are Moderate, not High; only the _.template advisory (GHSA-r5fr-rjxr-66jc) is High, and _.template is not used here. Because the lodash-using files are template boilerplate, the team should also consider dropping the dependency entirely as suggested. No reachable exploit path exists in current source.


<a id="f-storybook-version-skew-dev"></a>
#### 🔵 LOW — Storybook devDependency version skew (mixed 8.6.x and 9.0.x packages) yielding High/Moderate audit hits

**ID:** `storybook-version-skew-dev` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Version skew / dev-dependency hygiene &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:49-53 (@storybook/addon-essentials/blocks/manager-api/theming ^8.6.14, addon-interactions ^8.6.14)`
- `Frontend/package.json:51,54-56,72,76 (@storybook/addon-links 9.0.9, @storybook/react/react-vite ^9.0.9, storybook ^9.0.9)`

**Problem.** The Storybook toolchain mixes core 9.0.x with several addons pinned to 8.6.x (addon-essentials, addon-interactions, blocks, manager-api, theming). Storybook 9 is a breaking major over 8; mixing the two trees is unsupported and is what produces the `storybook` (High) and `@storybook/addon-essentials`/`addon-actions` (Moderate) audit entries. This is dev-only tooling, not shipped to users.

**Impact.** Flaky/broken Storybook builds and audit noise. No production runtime exposure since these are devDependencies, but the version mismatch makes `npm install` fragile and obscures real findings.

**Recommendation.** Align the whole Storybook toolchain on a single major (move all @storybook/* to 9.x using the `npx storybook@latest upgrade` migration, or pin everything to 8.x if staying). This clears the related audit entries and stabilizes dev builds.

> 🔍 **Verifier note.** Substance is correct; only nit is that the finder's line-range labels are slightly imprecise — e.g. the "49-53" 8.6.x range actually includes line 51 (@storybook/addon-links) and 52 (addon-onboarding) which are 9.0.x, and the 9.0.x group should also include line 52 (addon-onboarding ^9.0.9) and line 72 (eslint-plugin-storybook ^9.0.9). Net: same set of packages, mislabeled by a line or two — does not change the verdict. Severity Low is appropriate (dev-only tooling, zero production exposure); could even be Info, but Low is defensible since it can break the storybook dev/build scripts and pollutes audit output.


<a id="f-backend-no-vuln-scan"></a>
#### 🔵 LOW — Backend NuGet packages have no automated vulnerability scanning

**ID:** `backend-no-vuln-scan` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Supply-chain process &nbsp;·&nbsp; **Effort:** Small &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Backend/API.csproj:11-31 (packages); repo-wide gap: no Directory.Build.props, no root .github CI, no dependabot.yml — only Frontend/.github/workflows/* exists and is node-only`
- `Backend/API.csproj:11-31`
- `Backend.Tests/Backend.Tests.csproj:10-16`

**Problem.** The backend references several runtime packages (AWSSDK.S3 4.0.2, EF Core 9.0.6 stack, JwtBearer 8.0.17, System.Linq.Dynamic.Core 1.6.6, SkiaSharp 3.119.0, Swashbuckle 9.0.1) with no `dotnet list package --vulnerable` step, no NuGet audit in CI, and no Dependabot nuget ecosystem entry. The version pairing (net8.0 + JwtBearer 8.0.17 + EF Core 9.0.6) is supported by Microsoft, so this is not a true incompatibility, but the absence of scanning means any future NuGet advisory (e.g., in System.Linq.Dynamic.Core, which has had injection-style concerns, or SkiaSharp native CVEs) would go unnoticed. System.Linq.Dynamic.Core in particular is security-sensitive because it evaluates dynamic LINQ expressions.

**Impact.** Server-side dependency vulnerabilities accumulate undetected. Given the backend handles trade data and JWT auth, an unscanned advisory in the auth/serialization/dynamic-query stack could be exploitable without the team being aware.

**Recommendation.** Enable NuGet auditing (set <NuGetAudit>true</NuGetAudit> and <NuGetAuditMode>all</NuGetAuditMode> in the csproj / Directory.Build.props), add `dotnet list package --vulnerable --include-transitive` as a CI gate, and add a nuget ecosystem block to dependabot.yml. Specifically review System.Linq.Dynamic.Core usage to ensure no user input reaches dynamic LINQ string parsing.

> 🔍 **Verifier note.** Caveats on the recommendation: (1) The 'review System.Linq.Dynamic.Core usage' action item is largely already satisfied — ApplySort allowlists the sort column via IsValidProperty before dynamic parsing, so no obvious user-controlled string reaches the LINQ parser; the team should still audit the Where(...) call sites (APIResult.cs:226-244) for completeness, but the core sort path is guarded. (2) The locations are accurate but incomplete — the finding is really about repo-level files that do NOT exist (no root .github, no dependabot.yml, no Directory.Build.props), so any remediation lands on new files plus the two csproj files. (3) NuGetAudit defaults to true on .NET 8 SDK during restore but only warns (not errors) by default and only covers direct deps unless NuGetAuditMode=all is set — so the explicit hardening the finder recommends is still meaningful.


<a id="f-xlsx-sheetjs-cve"></a>
#### 🔵 LOW — xlsx (SheetJS) pinned to vulnerable 0.18.5 with prototype-pollution + ReDoS CVEs, used in live export path

**ID:** `xlsx-sheetjs-cve` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Known-vulnerable dependency &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:44; Frontend/package-lock.json:13695-13718; Frontend/src/components/My Components/Table/BasicTable.tsx:19,242-243 (sole consumer); reachable via Frontend/src/pages/User/UserList.tsx (no onExcel → client-side XLSX path)`
- `Frontend/package.json:44`
- `Frontend/package-lock.json:13695 (node_modules/xlsx version 0.18.5)`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:19`
- `Frontend/src/components/My Components/Table/BasicTable.tsx:242-243`

**Problem.** package.json declares "xlsx": "^0.18.5" and the lockfile resolves exactly 0.18.5 from the npm registry. This version is affected by CVE-2023-30533 (Prototype Pollution, advisory GHSA-4r6h-8v6p-xvw6, fixed in 0.19.3) and CVE-2024-22363 (ReDoS, advisory GHSA-5pgg-2g8v-p4x9, fixed in 0.20.2). Crucially these versions are NOT published to npm — the maintainer pulled SheetJS from npm, so the latest you can get from npmjs.org is the vulnerable 0.18.5; the fix is only available from the SheetJS CDN (cdn.sheetjs.com). The library is not dead weight — it is actively invoked in the client-side Excel export at BasicTable.tsx:242-243 via XLSX.utils.table_to_book(...) and XLSX.writeFile(...), exactly the parse/build code paths the CVEs target.

**Impact.** When report data containing attacker-influenced cell content is rendered into the HTML table and exported, the SheetJS workbook builder can be driven into prototype pollution or catastrophic ReDoS in the operator's browser. In a government reporting context the table content originates from trade-form submissions (BusinessType/company names, etc.), so an unprivileged data submitter could plant a payload that fires in an admin's browser when they export — a stored-input-to-admin attack surface. No upgrade is reachable via npm, so a plain `npm update` will never fix it, masking the risk.

**Recommendation.** Stop sourcing xlsx from npm. Either (a) migrate to the maintained SheetJS CDN tarball (xlsx@^0.20.3 from https://cdn.sheetjs.com/xlsx-0.20.3/xlsx-0.20.3.tgz) and pin that URL in package.json, or (b) switch the export to a maintained alternative such as exceljs, which avoids the SheetJS distribution problem entirely. Since the export is the only use, exceljs is a clean swap for BasicTable.tsx.

**Example:**

```
// package.json (current — vulnerable, npm cannot fix)
"xlsx": "^0.18.5"

// fixed — pin the patched CDN build
"xlsx": "https://cdn.sheetjs.com/xlsx-0.20.3/xlsx-0.20.3.tgz"
```

> 🔍 **Verifier note.** Verdict Adjusted (not Rejected): the dependency facts are all correct and the npm-cannot-fix claim is the strongest part of the finding (verified directly against the registry). The downgrade to Low is because exploitability for THIS app is essentially nil — the two CVEs live in the read/parse path (XLSX.read/SSF), and the codebase contains zero read/parse calls; it only builds workbooks from a DOM table and writes them out. So the dramatic "attacker plants payload in a trade form → fires in admin browser on export" chain does not hold against the actual sinks. Keep it as a hygiene/known-vulnerable-dep item and remediate cheaply (single call site). If an Excel/CSV IMPORT feature using XLSX.read is added later, re-escalate to High immediately.


<a id="f-protobufjs-firebase-critical"></a>
#### 🔵 LOW — Critical protobufjs 7.5.3 (code execution / DoS) pulled by firebase, in production path

**ID:** `protobufjs-firebase-critical` &nbsp;·&nbsp; **Phase:** P1 &nbsp;·&nbsp; **Category:** Known-vulnerable transitive dependency &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package-lock.json (node_modules/protobufjs 7.5.3, reachable only via firebase -> @firebase/firestore -> @grpc/proto-loader); note: @firebase/firestore is never imported by this app`
- `Frontend/package.json:29 (firebase ^11.9.1)`
- `Frontend/package-lock.json (node_modules/protobufjs 7.5.3 via firebase -> @firebase/firestore -> @grpc/proto-loader)`

**Problem.** `npm ls protobufjs` shows firebase@11.9.1 -> @firebase/firestore@4.7.17 -> @grpc/proto-loader@0.7.15 -> protobufjs@7.5.3. npm audit rates protobufjs Critical with multiple advisories affecting <=7.5.7, including arbitrary code execution (GHSA-xq3m-2v4x-88gg, fixed <7.5.5), code injection via bytes-field defaults (GHSA-66ff-xgx4-vchm), prototype injection in generated constructors (GHSA-fx83-v9x8-x52w), and several DoS vectors (unbounded recursion, crafted field names).

**Impact.** protobufjs is the wire-format codec for Firestore traffic. Crafted protobuf data deserialized through generated code can lead to prototype pollution and, per the advisory, code execution / process-wide DoS in the running app. This sits in the live data path of a government application.

**Recommendation.** Upgrade firebase to the latest 11.x (or current major) so the transitive @grpc/proto-loader pulls protobufjs >=7.5.5 (ideally the newest). If firebase upgrade is constrained, add an `overrides` entry pinning protobufjs to a patched release and verify firebase still functions. Separately, evaluate whether firebase is even needed (see unused/heavy-deps finding) — removing it eliminates this entire subtree.

> 🔍 **Verifier note.** Supporting evidence: (1) Frontend/package.json:29 declares firebase ^11.9.1. (2) npm ls protobufjs and package-lock.json confirm protobufjs@7.5.3 via firebase->firestore->@grpc/proto-loader. (3) npm audit confirms Critical with all cited GHSA advisories. (4) Frontend/src/firebase/firebaseConfig.ts uses only firebase/app, /analytics, /messaging (NOT /firestore) and its exports are unreferenced. (5) Lockfile shows only @firebase/firestore and @grpc/grpc-js depend on @grpc/proto-loader. The 'unused/heavy-deps' finding the report references appears well-founded: firebase looks entirely unused here, which is the cleanest fix.


<a id="f-heavy-single-use-deps"></a>
#### 🔵 LOW — Heavy single-use deps (firebase, mqtt) inflate bundle and vulnerability surface

**ID:** `heavy-single-use-deps` &nbsp;·&nbsp; **Phase:** P3 &nbsp;·&nbsp; **Category:** Bundle weight / over-broad dependency &nbsp;·&nbsp; **Effort:** Medium &nbsp;·&nbsp; **Confidence:** High

**Location(s):**
- `Frontend/package.json:29 (firebase ^11.9.1)`
- `Frontend/package.json:33 (mqtt ^5.13.1)`
- `Frontend/src/firebase/firebaseConfig.ts (only firebase consumer)`
- `Frontend/src/components/Chat/ChatBox.tsx (only mqtt consumer)`

**Problem.** firebase (one of the largest npm packages; pulls grpc + protobufjs) is referenced from a single file, and mqtt (which pulls ws, flagged moderate by audit) is referenced from one ChatBox component. firebase alone drags in the Critical protobufjs subtree documented above. For a report-generation admin tool, both look incidental to the core function.

**Impact.** These two deps account for a large share of the install size and a meaningful slice of the 32 audit findings (protobufjs Critical, ws moderate, follow-redirects, etc.). Carrying them for one component each is a poor risk/value trade in a sensitive system.

**Recommendation.** Confirm whether the Firebase integration and MQTT chat are actually shipped features. If not, remove them (and their entire transitive subtrees disappear, including the protobufjs Critical). If they are needed, isolate them behind lazy/dynamic imports so they are not in the main bundle, and pin firebase/mqtt to patched releases.

> 🔍 **Verifier note.** Finding under-states the case in two helpful ways: (1) firebaseConfig.ts is unreferenced dead code (no importer), so firebase is not merely single-use but effectively unused — removal is risk-free for firebase. (2) mqtt's only consumer ChatBox is gated behind a /Test scratch route and points at a public EMQX broker, reinforcing that it is not a production feature. Removing both deps would also clear the prod-side ws path (storybook still pulls ws as a dev dep, so ws would remain in devDependencies). No corrections to location or severity needed.


---

## Implementation Task Checklist

Copy these into your tracker. Each task references its finding ID; check the detailed section for specifics.

### P0 — Emergency (hours → a few days)

- [ ] **(Critical)** Production SQL 'sa' credentials and weak literal JWT signing key checked into appsettings.json — _Small_ [`hardcoded-prod-credentials-jwt-key`](#f-hardcoded-prod-credentials-jwt-key)
- [ ] **(Critical)** Unauthenticated endpoint dumps entire Users table including plaintext passwords — _Small_ [`chatlist-unauth-user-password-dump`](#f-chatlist-unauth-user-password-dump)
- [ ] **(Critical)** Weak, human-readable JWT signing key hardcoded in source and committed — _Small_ [`weak-hardcoded-jwt-key`](#f-weak-hardcoded-jwt-key)
- [ ] **(Critical)** Admin user list renders a cleartext 'password' column from the API — _Small_ [`passwords-rendered-in-userlist`](#f-passwords-rendered-in-userlist)
- [ ] **(Critical)** Connection strings and JWT signing key read via raw IConfiguration from checked-in appsettings.json (no options/secret binding) — _Medium_ [`secrets-raw-iconfiguration-no-options`](#f-secrets-raw-iconfiguration-no-options)
- [ ] **(Critical)** Authentication compares plaintext passwords and misuses AsParallel() on an EF IQueryable — _Medium_ [`plaintext-password-and-asparallel-auth`](#f-plaintext-password-and-asparallel-auth)
- [ ] **(Critical)** Production SQL Server 'sa' credentials hardcoded in appsettings.json and committed to git — _Medium_ [`hardcoded-prod-sa-credentials-in-git`](#f-hardcoded-prod-sa-credentials-in-git)
- [ ] **(Critical)** Passwords stored and compared in plaintext — _Medium_ [`plaintext-password-storage-and-auth`](#f-plaintext-password-storage-and-auth)
- [ ] **(Critical)** Live production SQL 'sa' credentials and JWT key committed and still tracked in git — _Medium_ [`prod-sa-creds-committed-in-git`](#f-prod-sa-creds-committed-in-git)
- [ ] **(High)** User list renders a 'password' column and can export it client-side — _Trivial_ [`userlist-password-column`](#f-userlist-password-column)
- [ ] **(High)** Generic [AllowAnonymous] CRUD base controller exposes the User entity table over HTTP — _Small_ [`allowanonymous-generic-crud-base`](#f-allowanonymous-generic-crud-base)
- [ ] **(High)** UploadController is unauthenticated, vulnerable to path traversal, and leaks exception messages — _Medium_ [`upload-controller-path-traversal-and-leak`](#f-upload-controller-path-traversal-and-leak)
- [ ] **(High)** Inconsistent authorization: ChatController and UploadController fully anonymous; BaseAPIController marked [AllowAnonymous] exposing User table — _Medium_ [`inconsistent-authorization-across-controllers`](#f-inconsistent-authorization-across-controllers)
- [ ] **(Medium)** Swagger UI and OpenAPI spec served unconditionally in production — _Trivial_ [`swagger-exposed-in-production`](#f-swagger-exposed-in-production)
- [ ] **(Medium)** Login and upload endpoints return raw exception messages to clients — _Trivial_ [`verbose-exception-leak-on-login`](#f-verbose-exception-leak-on-login)
- [ ] **(Medium)** Swagger UI and OpenAPI spec served unconditionally in production — _Trivial_ [`swagger-served-in-production`](#f-swagger-served-in-production)
- [ ] **(Medium)** Login/SignUp/PasswordReset log submitted form values (including password) to console — _Trivial_ [`credentials-logged-to-console`](#f-credentials-logged-to-console)
- [ ] **(Medium)** JWT issuer and audience validation disabled — _Small_ [`jwt-issuer-audience-validation-disabled`](#f-jwt-issuer-audience-validation-disabled)
- [ ] **(Medium)** ChatController endpoints are unauthenticated — _Small_ [`chatcontroller-unauth-data`](#f-chatcontroller-unauth-data)
- [ ] **(Medium)** Hardcoded SQL SA password and personal host path baked into docker-compose — _Small_ [`compose-hardcoded-sa-password`](#f-compose-hardcoded-sa-password)
- [ ] **(Medium)** Unauthenticated file upload with path traversal and ineffective validation — _Medium_ [`upload-unauth-path-traversal`](#f-upload-unauth-path-traversal)
- [ ] **(Medium)** Generic CRUD PUT/POST allows mass-assignment of any entity field including UserController — _Medium_ [`mass-assignment-baseapicontroller-put`](#f-mass-assignment-baseapicontroller-put)

### P1 — High priority (this sprint)

- [ ] **(High)** No .NET CI pipeline — backend tests never run automatically — _Small_ [`no-dotnet-ci-tests-not-run`](#f-no-dotnet-ci-tests-not-run)
- [ ] **(High)** axios 1.9.0 (production HTTP client) carries 20+ advisories incl. SSRF, proxy-credential leak, prototype pollution — _Small_ [`axios-vulnerable-1-9-0`](#f-axios-vulnerable-1-9-0)
- [ ] **(High)** No dependency scanning (Dependabot/Renovate/audit) anywhere; only leftover template CI on EOL Node — _Small_ [`no-dependency-scanning`](#f-no-dependency-scanning)
- [ ] **(High)** Frontend production Dockerfile is broken: drops build tooling and copies wrong output dir — _Small_ [`frontend-dockerfile-broken-build`](#f-frontend-dockerfile-broken-build)
- [ ] **(High)** OFFSET/FETCH pagination without a stable, unique ORDER BY drops and duplicates rows — _Medium_ [`offset-without-stable-order-by`](#f-offset-without-stable-order-by)
- [ ] **(High)** Authentication / JWT issuance (JWTManagerService, AuthController) completely untested — _Medium_ [`auth-jwt-untested`](#f-auth-jwt-untested)
- [ ] **(High)** No route-level code splitting: 159 report pages + 336 KB config in one bundle — _Medium_ [`no-route-code-splitting`](#f-no-route-code-splitting)
- [ ] **(High)** JWT, user id and permission stored in localStorage and injected into every request — _Medium_ [`auth-token-in-localstorage`](#f-auth-token-in-localstorage)
- [ ] **(High)** Report grid advertises sorting/searching but wires neither — no way to sort or find rows in large reports — _Medium_ [`no-table-sort-or-search`](#f-no-table-sort-or-search)
- [ ] **(High)** JWT, user id and permission stored in localStorage (XSS token theft) — _Large_ [`jwt-and-identity-in-localstorage`](#f-jwt-and-identity-in-localstorage)
- [ ] **(Medium)** No global exception-handling middleware; in production unhandled errors leak raw stack traces or return opaque 500s — _Small_ [`no-global-exception-middleware`](#f-no-global-exception-middleware)
- [ ] **(Medium)** Excel export jobs have no per-user ownership scoping (authenticated IDOR) — _Small_ [`excel-export-idor`](#f-excel-export-idor)
- [ ] **(Medium)** react-router-dom 7.6.2 carries router advisories (DoS / pre-render data spoofing) — _Small_ [`react-router-7-advisory`](#f-react-router-7-advisory)
- [ ] **(Medium)** CORS allows credentials with localhost wildcards and a trailing-slash production origin — _Small_ [`cors-allowcredentials-localhost-wildcards`](#f-cors-allowcredentials-localhost-wildcards)
- [ ] **(Medium)** Structured logging and correlation IDs exist only in the Excel workers; the entire report/API surface logs nothing — _Medium_ [`no-structured-logging-correlation`](#f-no-structured-logging-correlation)
- [ ] **(Medium)** ExcelExportController download/list/delete authorization (IDOR surface) untested — _Medium_ [`excel-export-controller-authz-untested`](#f-excel-export-controller-authz-untested)
- [ ] **(Medium)** No CI security scanning or backend CI; main branch excluded from tests — _Medium_ [`no-ci-security-scanning`](#f-no-ci-security-scanning)
- [ ] **(Medium)** No structured logging, monitoring, or alerting; default console logging only — _Medium_ [`no-monitoring-logging-alerting`](#f-no-monitoring-logging-alerting)
- [ ] **(Medium)** No Content-Security-Policy or hardening response headers — _Medium_ [`no-content-security-policy`](#f-no-content-security-policy)
- [ ] **(Medium)** Chat uses a hardcoded public MQTT broker with a static shared topic — _Medium_ [`public-mqtt-broker-chat`](#f-public-mqtt-broker-chat)
- [ ] **(Low)** Critical form-data 4.0.2 (unsafe random boundary) pulled transitively by axios — _Trivial_ [`form-data-critical-transitive`](#f-form-data-critical-transitive)
- [ ] **(Low)** Over-permissive CORS: AllowCredentials with localhost wildcard and AllowAnyHeader/Method — _Small_ [`cors-allowcredentials-localhost-wildcard`](#f-cors-allowcredentials-localhost-wildcard)
- [ ] **(Low)** lodash 4.17.21 (direct) vulnerable to prototype pollution and _.template code injection — _Small_ [`lodash-prototype-pollution`](#f-lodash-prototype-pollution)
- [ ] **(Low)** Backend NuGet packages have no automated vulnerability scanning — _Small_ [`backend-no-vuln-scan`](#f-backend-no-vuln-scan)
- [ ] **(Low)** xlsx (SheetJS) 0.18.5 from npm registry has unpatched known advisories — _Small_ [`xlsx-vulnerable-version`](#f-xlsx-vulnerable-version)
- [ ] **(Low)** xlsx (SheetJS) pinned to vulnerable 0.18.5 with prototype-pollution + ReDoS CVEs, used in live export path — _Medium_ [`xlsx-sheetjs-cve`](#f-xlsx-sheetjs-cve)
- [ ] **(Low)** Critical protobufjs 7.5.3 (code execution / DoS) pulled by firebase, in production path — _Medium_ [`protobufjs-firebase-critical`](#f-protobufjs-firebase-critical)

### P2 — Medium (this quarter)

- [ ] **(Medium)** ~34 unreferenced StoredProcedureToLinq classes (explicit _old/_V2/_Seperated/Test dead code) — _Small_ [`dead-code-sp-to-linq-and-controllers`](#f-dead-code-sp-to-linq-and-controllers)
- [ ] **(Medium)** Dynamic filter does an unguarded Convert.ToDateTime on user input (FormatException -> 500) — _Small_ [`apifilter-unguarded-datetime-convert`](#f-apifilter-unguarded-datetime-convert)
- [ ] **(Medium)** Full xlsx statically imported into shared BasicTable; Excel is server-generated — _Small_ [`xlsx-static-import-basictable`](#f-xlsx-static-import-basictable)
- [ ] **(Medium)** Vite config has no build/chunking/analysis settings — _Small_ [`bare-vite-build-config`](#f-bare-vite-build-config)
- [ ] **(Medium)** No rate limiting, health checks, or API versioning across 158 report endpoints — _Medium_ [`no-ratelimit-no-healthcheck-no-versioning`](#f-no-ratelimit-no-healthcheck-no-versioning)
- [ ] **(Medium)** Synchronous paged report endpoints do not accept or propagate CancellationToken — _Medium_ [`cancellationtoken-not-propagated-sync-endpoints`](#f-cancellationtoken-not-propagated-sync-endpoints)
- [ ] **(Medium)** Broad catch blocks swallow context, leak raw exception messages, and there is almost no logging on the request path — _Medium_ [`swallowed-exceptions-and-leaked-messages`](#f-swallowed-exceptions-and-leaked-messages)
- [ ] **(Medium)** Inconsistent API result shapes and return types across controllers — _Medium_ [`inconsistent-api-result-shapes`](#f-inconsistent-api-result-shapes)
- [ ] **(Medium)** ImportLicenceBySectionReportController materializes all rows then nested-loops sections x currencies in memory — _Medium_ [`bysection-cross-join-in-memory`](#f-bysection-cross-join-in-memory)
- [ ] **(Medium)** Border report pages order a UNION ALL of two 11-table joins by a non-unique key — _Medium_ [`border-union-unstable-and-heavy`](#f-border-union-unstable-and-heavy)
- [ ] **(Medium)** Most tests hard-require a live SQL Server with no skip guards; suite is not CI-runnable as-is — _Medium_ [`db-required-tests-no-skip-guards`](#f-db-required-tests-no-skip-guards)
- [ ] **(Medium)** ReportAggregationService (the report-correctness engine) has zero tests — _Medium_ [`report-aggregation-service-untested`](#f-report-aggregation-service-untested)
- [ ] **(Medium)** ReportUsdConversionService FX logic is untested despite intricate currency rules — _Medium_ [`usd-fx-conversion-untested`](#f-usd-fx-conversion-untested)
- [ ] **(Medium)** Endpoint smoke tests assert empty/zero results, not report correctness — _Medium_ [`smoke-tests-assert-plumbing-not-correctness`](#f-smoke-tests-assert-plumbing-not-correctness)
- [ ] **(Medium)** All containers run as root; backend uses dev SDK image with no multi-stage or .dockerignore — _Medium_ [`containers-run-as-root-no-multistage`](#f-containers-run-as-root-no-multistage)
- [ ] **(Medium)** ESLint 9 paired with legacy .eslintrc + unsupported --ext flag: lint gate runs nothing — _Medium_ [`broken-eslint-gate`](#f-broken-eslint-gate)
- [ ] **(Medium)** Half the codebase is unused antd-multi-dashboard template demo code shipped to production — _Medium_ [`dead-template-code`](#f-dead-template-code)
- [ ] **(Medium)** Unvirtualized HTML table exposes a 1000-row page size — _Medium_ [`unvirtualized-large-table`](#f-unvirtualized-large-table)
- [ ] **(Medium)** Report grid fetch has no cancellation; rapid filter/page changes race and can show stale/wrong data — _Medium_ [`no-request-cancellation-race`](#f-no-request-cancellation-race)
- [ ] **(Medium)** Backend errors are swallowed into generic strings; details discarded with empty catch blocks — _Medium_ [`swallowed-errors`](#f-swallowed-errors)
- [ ] **(Medium)** 134-report sidebar has no search/filter, and clicks log to console — _Medium_ [`huge-unsearchable-report-nav`](#f-huge-unsearchable-report-nav)
- [ ] **(Medium)** ~145 report controllers are near-identical copy-paste (157 duplicated TryCreateReportRequest blocks) — _Large_ [`report-controller-copypaste-sprawl`](#f-report-controller-copypaste-sprawl)
- [ ] **(Medium)** Full COUNT and full GROUP BY (plus FX query) re-executed on every page request — _Large_ [`count-and-groupby-rerun-per-page`](#f-count-and-groupby-rerun-per-page)
- [ ] **(Medium)** Legacy non-Fast LINQ loads full entities with per-row country subqueries and an in-memory UNION buffer — _Large_ [`legacy-notracking-and-client-eval`](#f-legacy-notracking-and-client-eval)
- [ ] **(Medium)** Pervasive `any` (66 sites) and an AnyObject catch-all type defeat strict mode — _Large_ [`any-and-anyobject-escape-hatch`](#f-any-and-anyobject-escape-hatch)
- [ ] **(Medium)** All server data fetched manually with useState; no caching, dedup, or retry (Redux used only for theme) — _Large_ [`no-rtk-query-no-caching`](#f-no-rtk-query-no-caching)

### P3 — Low / hardening (backlog)

- [ ] **(Low)** moment 2.30.1 is a direct dependency with zero source imports (app uses dayjs) — _Trivial_ [`moment-unused-direct-dep`](#f-moment-unused-direct-dep)
- [ ] **(Low)** moment is a dependency but unused; dayjs date-format helpers duplicated across report pages — _Trivial_ [`duplicate-date-and-dead-moment`](#f-duplicate-date-and-dead-moment)
- [ ] **(Low)** moment (~70 KB gzip) declared but never imported — _Trivial_ [`moment-dead-dependency`](#f-moment-dead-dependency)
- [ ] **(Low)** PersistGate wraps Provider (inverted), bypassing persisted-store gating — _Trivial_ [`persistgate-provider-order`](#f-persistgate-provider-order)
- [ ] **(Low)** Hardcoded UAT/localhost fallbacks in runtime config — _Trivial_ [`hardcoded-uat-qr-fallback`](#f-hardcoded-uat-qr-fallback)
- [ ] **(Low)** Every API response is round-tripped through JSON.parse(JSON.stringify()) before use — _Trivial_ [`redundant-json-roundtrip`](#f-redundant-json-roundtrip)
- [ ] **(Low)** Filter Reset wipes applied filters and Logout signs out with no confirmation — _Trivial_ [`reset-and-logout-no-confirmation`](#f-reset-and-logout-no-confirmation)
- [ ] **(Low)** Report request DTOs rely on hand-rolled per-controller checks instead of validation attributes — _Small_ [`no-dto-validation-attributes`](#f-no-dto-validation-attributes)
- [ ] **(Low)** DbContext pool / SQL connection pool sizing unconfigured with MARS disabled — _Small_ [`connection-pool-unconfigured`](#f-connection-pool-unconfigured)
- [ ] **(Low)** Test harness silently falls back to a default constructor, masking dependency gaps — _Small_ [`createcontroller-silent-fallback-masks-coverage`](#f-createcontroller-silent-fallback-masks-coverage)
- [ ] **(Low)** ExcelExportHasher dedup logic and file-store path handling untested — _Small_ [`excel-hasher-and-filestore-untested`](#f-excel-hasher-and-filestore-untested)
- [ ] **(Low)** EF query-translation tests cover a small slice of 93 SP-to-LINQ queries — _Small_ [`query-translation-coverage-narrow`](#f-query-translation-coverage-narrow)
- [ ] **(Low)** Storybook devDependency version skew (mixed 8.6.x and 9.0.x packages) yielding High/Moderate audit hits — _Small_ [`storybook-version-skew-dev`](#f-storybook-version-skew-dev)
- [ ] **(Low)** No container healthchecks or resource limits; restart policies inconsistent — _Small_ [`no-healthchecks-no-resource-limits`](#f-no-healthchecks-no-resource-limits)
- [ ] **(Low)** HTTPS/HSTS configuration gaps: AllowedHosts wildcard, duplicate redirect, dev SSL trust — _Small_ [`https-hsts-gaps`](#f-https-hsts-gaps)
- [ ] **(Low)** useFetchData has a stale-closure dependency bug, no auth, and no cleanup — _Small_ [`usefetchdata-stale-closure-bug`](#f-usefetchdata-stale-closure-bug)
- [ ] **(Low)** BasicHttpServices casts every response to PaginationType and deep-clones via JSON round-trip — _Small_ [`http-service-type-lying`](#f-http-service-type-lying)
- [ ] **(Low)** 51 console.log/error statements left in production source, including auth and 401 flows — _Small_ [`console-logs-in-prod`](#f-console-logs-in-prod)
- [ ] **(Low)** Core shared components live under a folder with a literal space ("My Components") — _Small_ [`folder-literal-space-my-components`](#f-folder-literal-space-my-components)
- [ ] **(Low)** Multiple useEffect/useCallback dependency-array bugs flagged by react-hooks — _Small_ [`missing-hook-deps`](#f-missing-hook-deps)
- [ ] **(Low)** ~7 MB unoptimized demo images + 188 KB favicon in public/ — _Small_ [`oversized-public-assets`](#f-oversized-public-assets)
- [ ] **(Low)** lodash imported as full namespace in 6 starter-template files — _Small_ [`lodash-full-namespace-import`](#f-lodash-full-namespace-import)
- [ ] **(Low)** Timeline, Test and Certificate routes sit outside ProtectedRoute — _Small_ [`unprotected-admin-routes`](#f-unprotected-admin-routes)
- [ ] **(Low)** No React error boundary anywhere; a single render throw blanks the entire admin app — _Small_ [`no-error-boundary`](#f-no-error-boundary)
- [ ] **(Low)** useFetchData has a stale-closure bug, no cancellation, and bypasses auth/baseUrl — _Small_ [`usefetchdata-stale-closure`](#f-usefetchdata-stale-closure)
- [ ] **(Low)** 1000-row page size plus DOM-scraping Excel fallback can load/serialize very large datasets on the main thread — _Small_ [`unbounded-pagesize-client-export`](#f-unbounded-pagesize-client-export)
- [ ] **(Low)** Page breadcrumb shows '...' instead of the report name, using the raw URL segment — _Small_ [`breadcrumb-collapses-current-page`](#f-breadcrumb-collapses-current-page)
- [ ] **(Low)** Sign-in form prefills hardcoded demo credentials and has weak validation/feedback — _Small_ [`signin-hardcoded-demo-creds`](#f-signin-hardcoded-demo-creds)
- [ ] **(Low)** Client-side Excel export silently exports only the current page, not the full report — _Small_ [`client-excel-exports-current-page-only`](#f-client-excel-exports-current-page-only)
- [ ] **(Low)** Report table lacks header scope/caption for screen readers and has no sticky header on tall/wide grids — _Small_ [`table-a11y-and-sticky-header`](#f-table-a11y-and-sticky-header)
- [ ] **(Low)** TradeNetDbContext (228 entities, the real data) is a DB-first scaffold with no migrations or schema-version control — _Medium_ [`second-dbcontext-not-under-migrations`](#f-second-dbcontext-not-under-migrations)
- [ ] **(Low)** Excel writers call PropertyInfo.GetValue via reflection per cell on every export row — _Medium_ [`reflection-getvalue-per-cell-excel`](#f-reflection-getvalue-per-cell-excel)
- [ ] **(Low)** Heavy single-use deps (firebase, mqtt) inflate bundle and vulnerability surface — _Medium_ [`heavy-single-use-deps`](#f-heavy-single-use-deps)
- [ ] **(Low)** Production deploy is a manual PowerShell robocopy to a Windows share with no env isolation or rollback — _Medium_ [`deploy-script-manual-robocopy-no-env-isolation`](#f-deploy-script-manual-robocopy-no-env-isolation)
- [ ] **(Low)** reportConfigs.ts is a single 13,793-line file holding 134 report definitions — _Medium_ [`oversized-report-configs`](#f-oversized-report-configs)
- [ ] **(Low)** BasicTable rows/cells not memoized; full re-render on every state change — _Medium_ [`basictable-no-row-cell-memo`](#f-basictable-no-row-cell-memo)
- [ ] **(Low)** isAuthenticated derived from client-controlled localStorage with no token validation — _Medium_ [`client-side-auth-state-spoofable`](#f-client-side-auth-state-spoofable)
- [ ] **(Low)** Dark theme is supported in code but has no UI toggle, and report tables hardcode light colors — _Medium_ [`dark-theme-dead-and-tables-hardcode-light`](#f-dark-theme-dead-and-tables-hardcode-light)
- [ ] **(Low)** Data access is split across 83 static sp_* helpers and per-controller boilerplate with no injectable abstraction, hurting testability and consistency — _Large_ [`data-access-static-helpers-tight-coupling`](#f-data-access-static-helpers-tight-coupling)
- [ ] **(Low)** No internationalization and no AntD locale despite Myanmar/English requirement — all UI chrome is English-only — _Large_ [`no-i18n-and-no-antd-locale`](#f-no-i18n-and-no-antd-locale)
- [ ] **(Info)** ScrollToTop runs smooth-scroll on every route change — _Trivial_ [`scrolltotop-smooth-on-every-nav`](#f-scrolltotop-smooth-on-every-nav)
- [ ] **(Info)** Login form pre-filled with demo@email.com / demo123 — _Trivial_ [`demo-creds-prefilled-login`](#f-demo-creds-prefilled-login)
- [ ] **(Info)** Dashboard cards crash on fetch error (null-deref) instead of showing the error — _Trivial_ [`dashboard-card-null-deref`](#f-dashboard-card-null-deref)
- [ ] **(Info)** redux-persist configured with no whitelist/blacklist (persists entire root reducer) — _Trivial_ [`persist-no-whitelist`](#f-persist-no-whitelist)
- [ ] **(Info)** Two overlapping country caches with different TTLs and a redundant per-request freshness check — _Small_ [`countrycache-vs-memorycache-duplication`](#f-countrycache-vs-memorycache-duplication)
- [ ] **(Info)** useFileUpload opens a new window and writes image markup built from user input — _Small_ [`fileupload-window-write-sink`](#f-fileupload-window-write-sink)
- [ ] **(Info)** xlsx (SheetJS 0.18.5) is used only for export, not for parsing untrusted files — _Small_ [`sheetjs-cve-not-exploitable-note`](#f-sheetjs-cve-not-exploitable-note)

---

## Definition of Done / Verification

How to confirm each phase is genuinely complete:

### P0 (security emergency)
- [ ] `git log -p` / secret-scanner (e.g. `gitleaks`) on full history shows **no** DB password or JWT key.
- [ ] Old `sa` password and old JWT key **no longer authenticate** anywhere (rotated). App runs under a least-privilege SQL login.
- [ ] `GET /api/ChatList`, `/api/User`, `/api/Chat`, `/api/Upload` all return **401** without a valid token; user-shaped responses contain **no** `password` field (verified by request capture).
- [ ] Passwords in the DB are hashes, not plaintext; login still works after migration.
- [ ] Swagger returns **404** in the production environment; error responses contain no stack traces or raw exception text.
- [ ] JWTs signed with the *old* key, or with altered issuer/audience, are **rejected**.

### P1 (high priority)
- [ ] Paged reports return each row exactly once across pages on a dataset with duplicate timestamps (regression test for [`offset-without-stable-order-by`](#f-offset-without-stable-order-by)).
- [ ] `npm audit --audit-level=high` and `dotnet list package --vulnerable --include-transitive` are **green** (or risk-accepted in writing) and run in CI.
- [ ] A root-level GitHub Actions workflow runs `dotnet test` and `npm ci && npm run build` on every PR and **blocks merge** on failure.
- [ ] Frontend production image builds and serves the SPA (correct `dist/` copy, full install).
- [ ] A strict CSP header is present; session token is no longer the only thing standing between an XSS and account takeover.
- [ ] CORS no longer combines `AllowCredentials` with wildcard/localhost origins in production.

### P2 / P3
- [ ] Lighthouse/bundle analysis shows route-level code-splitting and a materially smaller initial bundle.
- [ ] Operators can sort and search inside report grids; sidebar is searchable.
- [ ] Global exception middleware + structured logging with correlation IDs cover the whole API; health checks respond.
- [ ] ESLint actually runs and the `any`/dead-code debt is trending down; tests cover auth, aggregation and FX logic.

---

## Appendix — Methodology

This report was produced by a **multi-agent audit** of the full codebase, run as a deterministic workflow:

- **12 specialist finder agents**, one per dimension: Backend Security, Architecture, Code Quality, Performance, Testing; Frontend Security, State & Data, Performance, UI/UX, Code Quality; DevOps & Deployment; Dependencies.
- Each finding was then handed to an independent **adversarial verifier agent** instructed to *refute* it by reading the actual source. Findings that did not hold up were dropped; severities and locations were corrected (those corrections appear as 🔍 *Verifier note* entries). Of the raw findings, **122 survived verification**.
- Severities reflect impact **in this specific application and deployment context**, not generic CVSS. Where a generic CVE was *not* actually exploitable here (e.g. the SheetJS `xlsx` parse CVEs — `xlsx` is used only to *write* exports, never to parse untrusted uploads; and the Node-only axios proxy/SSRF advisories, since this is a browser SPA), that nuance is recorded rather than inflated.

**Caveats / things to verify on the live system** (the audit read code, it did not run the deployed app):

- Whether `[AllowAnonymous]` on `BaseAPIController` currently exposes `/api/User` anonymously (the ASP.NET Core precedence rule says it likely does — verify and treat as Critical until disproven).
- Whether the database server is in fact reachable from the public internet on `203.81.66.111,14330` (inferred from config).
- Exact production CORS/Swagger behaviour, which depends on the deployed `ASPNETCORE_ENVIRONMENT`.

_Generated 2026-06-06. Finding IDs are stable; cross-reference them when filing remediation tickets._
