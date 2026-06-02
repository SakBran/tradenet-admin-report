# New Reports Build Task (Module 12+)

Create 33 **net-new** report APIs + frontend pages for 12 new modules. These are
**not** in `docs/ReportAndLinqMappingList.md` yet (that file tracks the 125
already-done reports). The LINQ converters and stored procedures **already exist**
— only the controllers, frontend config/pages/routes/nav, and tracker rows are
missing.

This follows the existing **ReportApiBuildGuide** flow (controller →
`ReportQueryService.CreatePagedResultAsync(query, request)` on the converter's
`IQueryable`), **not** the `_pagination` stored-procedure conversion flow.

Reference example end-to-end:
- Backend: `Backend/Controllers/Report/MemberRegistrationReportController.cs`
- Frontend page wrapper: `Frontend/src/Report/Page/RegistrationByVoucher.tsx`
- Frontend config entry: `RegistrationByVoucher` in `Frontend/src/Report/config/reportConfigs.ts`

---

## What each report needs (per-report checklist)

1. **Backend controller** — `Backend/Controllers/Report/{ControllerName}Controller.cs`
   - `[Authorize] [ApiController] [Route("api/[controller]")]`, inject `TradeNetDbContext`.
   - `[HttpPost]` → build the converter request, call `{Sp}.Query(_context, req)`,
     return `ReportQueryService.CreatePagedResultAsync(query, request)`.
   - `[HttpPost("Excel")]` → same query → `ExcelGenerator.CreateWorkbookAsync(query, request, "{Title}")`.
   - Controller request DTO inherits `ReportQueryRequest`; only fields the converter request needs.
2. **Frontend config** — add a keyed entry to `reportConfigs` in
   `Frontend/src/Report/config/reportConfigs.ts` (columns from the converter
   Result class, filters from the request DTO + old MVC view).
3. **Frontend page wrapper** — `Frontend/src/Report/Page/{ControllerName}.tsx`
   (6-line wrapper over `GenericReportPage` with `config={reportConfigs.{ControllerName}}`).
4. **Route** — import + `{ path, element }` line in `Frontend/src/Report/reportRoutes.tsx`.
5. **Nav category** — add a `ReportCategory` (or extend one) in
   `Frontend/src/Report/reportNavItems.tsx`.
6. **Trackers** — add row to `docs/ReportAndLinqMappingList.md` (new module section)
   and `docs/FrontEndImplementationGuide.md` tracker.
7. **Build** — `dotnet build Backend/API.csproj` and `npm run build` (Frontend) green.

---

## Stored-procedure parameter contract (from old MVC `Business/Reports.cs`)

The "Report" SPs are multi-form (Summary / Detail share one SP via `@Type`).
`@FormType`/`RegistrationType` values come from `AppConfig`:
`"Whole Sale"`, `"Retail"`, `"Whole Sale and Retail"`, `"Sale Center"`,
`"Show Room"`, etc. (`AppConfig.cs` ~line 1226-1259).

| Report kind | Converter request fields to set |
| --- | --- |
| **Summary** | `Type = "Summary"`, `FormType = <AppConfig value>` (only if request has FormType), `FromDate`, `ToDate`, `Date = ToDate.Date`, `ApplyType = ""` |
| **Detail** | `Type = "Detail"` (i.e. not "Summary"), `FormType = <value>`, `ApplyType` (Valid/Invalid/New/...), `FromDate`, `ToDate`, `Date = ToDate.Date` |
| **Registration By Voucher** | `RegistrationType = <value>` (only if request has it), `ApplyType`, `PaymentType`, `FromDate`, `ToDate` |

> Summary's converter (`SummaryQuery`) ignores `ApplyType` and returns 5 rows
> (New/Cancel/Extension/Valid/Invalid counts). The new grid shows those rows
> (`ApplicationCount`, `ApplyType`) — the old RDLC pivoted them into one row;
> this is an acceptable grid-vs-RDLC difference.
> Detail's `ApplyType == "Valid"/"Invalid"` filters by `EndDate` vs `Date`.

---

## Modules, reports, SPs, controllers

Naming: controller = report name with spaces/punctuation removed, business
spelling kept, `+ Controller`. FormType column = value passed to the converter.

### 12. WholeSale  — SP `sp_WholeSaleRetailReport` / `sp_WholeSaleRetailRegistrationReport` (FormType `"Whole Sale"`)
| Report | Controller | Converter | Type/Reg |
| --- | --- | --- | --- |
| Whole Sale Summary Report | `WholeSaleSummaryReport` | `sp_WholeSaleRetailReport` | Summary |
| Whole Sale Detail Report | `WholeSaleDetailReport` | `sp_WholeSaleRetailReport` | Detail |
| WholeSale Registration By Voucher | `WholeSaleRegistrationByVoucher` | `sp_WholeSaleRetailRegistrationReport` | Reg |

### 13. Retail — same SPs (FormType `"Retail"`)
| Retail Summary Report | `RetailSummaryReport` | `sp_WholeSaleRetailReport` | Summary |
| Retail Detail Report | `RetailDetailReport` | `sp_WholeSaleRetailReport` | Detail |
| Retail Registration By Voucher | `RetailRegistrationByVoucher` | `sp_WholeSaleRetailRegistrationReport` | Reg |

### 14. Whole Sale and Retail — same SPs (FormType `"Whole Sale and Retail"`)
| Whole Sale and Retail Summary Report | `WholeSaleAndRetailSummaryReport` | `sp_WholeSaleRetailReport` | Summary |
| Whole Sale and Retail Detail Report | `WholeSaleAndRetailDetailReport` | `sp_WholeSaleRetailReport` | Detail |
| WS and R Registration By Voucher | `WholeSaleAndRetailRegistrationByVoucher` | `sp_WholeSaleRetailRegistrationReport` | Reg |

### 15. Alcoholic Beverages Importation — `sp_WineImportationReport` / `sp_WineImportationRegistrationReport` (no FormType/RegistrationType)
| Alcoholic Beverages Importation Summary Report | `AlcoholicBeveragesImportationSummaryReport` | `sp_WineImportationReport` | Summary |
| Alcoholic Beverages Importation Detail Report | `AlcoholicBeveragesImportationDetailReport` | `sp_WineImportationReport` | Detail |
| AB Registration By Voucher | `AlcoholicBeveragesImportationRegistrationByVoucher` | `sp_WineImportationRegistrationReport` | Reg |

### 16. Duty Free Shop — `sp_DutyFreeShopReport` / `sp_DutyFreeShopRegistrationReport` (no FormType/RegistrationType)
| Duty Free Shop Summary Report | `DutyFreeShopSummaryReport` | `sp_DutyFreeShopReport` | Summary |
| Duty Free Shop Detail Report | `DutyFreeShopDetailReport` | `sp_DutyFreeShopReport` | Detail |
| Duty Free Shop Registration By Voucher | `DutyFreeShopRegistrationByVoucher` | `sp_DutyFreeShopRegistrationReport` | Reg |

### 17. Re-Export — `sp_ReExportReport` (no registration report; no FormType)
| Re-Export Summary Report | `ReExportSummaryReport` | `sp_ReExportReport` | Summary |
| Re-Export Detail Report | `ReExportDetailReport` | `sp_ReExportReport` | Detail |

### 18. Business Service Agency — `sp_BusinessServiceAgencyReport` / `sp_BusinessServiceAgencyRegistrationReport` (no FormType/RegistrationType)
| Business Service Agency Summary Report | `BusinessServiceAgencySummaryReport` | `sp_BusinessServiceAgencyReport` | Summary |
| Business Service Agency Detail Report | `BusinessServiceAgencyDetailReport` | `sp_BusinessServiceAgencyReport` | Detail |
| BSA Registration By Voucher | `BusinessServiceAgencyRegistrationByVoucher` | `sp_BusinessServiceAgencyRegistrationReport` | Reg |

### 19. Sale Center — `sp_SaleCenterReport` / `sp_SaleCenterRegistrationReport` (FormType `"Sale Center"`)
| Sale Center Summary Report | `SaleCenterSummaryReport` | `sp_SaleCenterReport` | Summary |
| Sale Center Detail Report | `SaleCenterDetailReport` | `sp_SaleCenterReport` | Detail |
| Sale Center Registration By Voucher | `SaleCenterRegistrationByVoucher` | `sp_SaleCenterRegistrationReport` | Reg |

### 20. Show Room — `sp_ShowRoomReport` / `sp_ShowRoomRegistrationReport` (FormType `"Show Room"`)
| Show Room Summary Report | `ShowRoomSummaryReport` | `sp_ShowRoomReport` | Summary |
| Show Room Detail Report | `ShowRoomDetailReport` | `sp_ShowRoomReport` | Detail |
| Show Room Registration By Voucher | `ShowRoomRegistrationByVoucher` | `sp_ShowRoomRegistrationReport` | Reg |

### 21. EVCycle Show Room — `sp_EVCycleShowRoomReport` / `sp_EVCycleShowRoomRegistrationReport` (FormType TBD from old MVC)
| EVCycle Show Room Summary Report | `EVCycleShowRoomSummaryReport` | `sp_EVCycleShowRoomReport` | Summary |
| EVCycle Show Room Detail Report | `EVCycleShowRoomDetailReport` | `sp_EVCycleShowRoomReport` | Detail |
| EVCycle Show Room Registration By Voucher | `EVCycleShowRoomRegistrationByVoucher` | `sp_EVCycleShowRoomRegistrationReport` | Reg |

### 22. EV Show Room — `sp_EVShowRoomReport` / `sp_EVShowRoomRegistrationReport` (FormType TBD from old MVC)
| EV Show Room Summary Report | `EVShowRoomSummaryReport` | `sp_EVShowRoomReport` | Summary |
| EV Show Room Detail Report | `EVShowRoomDetailReport` | `sp_EVShowRoomReport` | Detail |
| EV Show Room Registration By Voucher | `EVShowRoomRegistrationByVoucher` | `sp_EVShowRoomRegistrationReport` | Reg |

### 23. OGA Recommendation — `sp_OGARecommendationListReport` (distinct shape: department/section ids, ref no)
| OGA Recommendation Report | `OGARecommendationReport` | `sp_OGARecommendationListReport` | — |

---

## Tracker

Status: ⬜ To Do · 🟡 Backend done (frontend pending) · ✅ Backend + Frontend done · 🚫 Blocked.

| # | Controller | Backend | Frontend |
| --- | --- | --- | --- |
| 12.1 | WholeSaleSummaryReport | ✅ | ✅ |
| 12.2 | WholeSaleDetailReport | ✅ | ✅ |
| 12.3 | WholeSaleRegistrationByVoucher | ✅ | ✅ |
| 13.1 | RetailSummaryReport | ✅ | ✅ |
| 13.2 | RetailDetailReport | ✅ | ✅ |
| 13.3 | RetailRegistrationByVoucher | ✅ | ✅ |
| 14.1 | WholeSaleAndRetailSummaryReport | ✅ | ✅ |
| 14.2 | WholeSaleAndRetailDetailReport | ✅ | ✅ |
| 14.3 | WholeSaleAndRetailRegistrationByVoucher | ✅ | ✅ |
| 15.1 | AlcoholicBeveragesImportationSummaryReport | ✅ | ✅ |
| 15.2 | AlcoholicBeveragesImportationDetailReport | ✅ | ✅ |
| 15.3 | AlcoholicBeveragesImportationRegistrationByVoucher | ✅ | ✅ |
| 16.1 | DutyFreeShopSummaryReport | ✅ | ✅ |
| 16.2 | DutyFreeShopDetailReport | ✅ | ✅ |
| 16.3 | DutyFreeShopRegistrationByVoucher | ✅ | ✅ |
| 17.1 | ReExportSummaryReport | ✅ | ✅ |
| 17.2 | ReExportDetailReport | ✅ | ✅ |
| 18.1 | BusinessServiceAgencySummaryReport | ✅ | ✅ |
| 18.2 | BusinessServiceAgencyDetailReport | ✅ | ✅ |
| 18.3 | BusinessServiceAgencyRegistrationByVoucher | ✅ | ✅ |
| 19.1 | SaleCenterSummaryReport | ✅ | ✅ |
| 19.2 | SaleCenterDetailReport | ✅ | ✅ |
| 19.3 | SaleCenterRegistrationByVoucher | ✅ | ✅ |
| 20.1 | ShowRoomSummaryReport | ✅ | ✅ |
| 20.2 | ShowRoomDetailReport | ✅ | ✅ |
| 20.3 | ShowRoomRegistrationByVoucher | ✅ | ✅ |
| 21.1 | EVCycleShowRoomSummaryReport | ✅ | ✅ |
| 21.2 | EVCycleShowRoomDetailReport | ✅ | ✅ |
| 21.3 | EVCycleShowRoomRegistrationByVoucher | ✅ | ✅ |
| 22.1 | EVShowRoomSummaryReport | ✅ | ✅ |
| 22.2 | EVShowRoomDetailReport | ✅ | ✅ |
| 22.3 | EVShowRoomRegistrationByVoucher | ✅ | ✅ |
| 23.1 | OGARecommendationReport | ✅ | ✅ |

### Resolved — Alcoholic Beverages Importation (module 15)

Wine uses the existing `sp_WineImportationReport_Fast` and
`sp_WineImportationRegistrationReport_Fast` converters. They page scalar rows
first and resolve WineType CSV ids through `ReportLookupCache` after
materialization, avoiding an EF correlated-subquery translation problem.

### Progress (this session)

- ✅ **Done end-to-end (33):** all module 12-23 reports have backend APIs,
  frontend config/pages/routes/nav coverage, tracker rows, and green
  backend/frontend builds.

**Total: 33 reports.**

### Resolved FormType / RegistrationType values (from dev DB `RegistrationType` columns)
| Module | FormType / RegistrationType value |
| --- | --- |
| WholeSale | `Whole Sale` |
| Retail | `Retail` |
| Whole Sale and Retail | `Whole Sale and Retail` |
| Sale Center | `Sale Center for Motor Vehicles` |
| Show Room | `Show Room for Brand New Motor Vehicles` |
| EVCycle Show Room | `Show Room for Electric Cycles` |
| EV Show Room | `Show Room for Electric Vehicles` |
| Wine / Duty Free / Re-Export / BSA | _(no FormType — SP filters by `@Type` only)_ |
