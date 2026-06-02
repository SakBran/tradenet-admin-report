# Report Mapping List

This document maps each report name to its related SQL stored procedure and tracks API build status.

- Total modules: 23
- Total mappings: 158
- API completed: 158
- API remaining: 0

Status values:

- `To Do`: API controller has not been created yet.
- `Completed`: API controller exists, builds successfully, returns `ApiResult<T>`, and has an Excel export endpoint.
- `Blocked`: API could not be completed; add the reason beside the row before stopping.

## Summary

| No. | Module                | Mapping Count |
| --: | --------------------- | ------------: |
|   1 | Member                |             1 |
|   2 | Pathaka               |            11 |
|   3 | Import Licence        |            16 |
|   4 | Import Permit         |            12 |
|   5 | Border Import Licence |            16 |
|   6 | Border Import Permit  |            12 |
|   7 | Border Export Permit  |            12 |
|   8 | Export Licence        |            14 |
|   9 | Export Permit         |            12 |
|  10 | Border Export Licence |            14 |
|  11 | Payment               |             5 |
|  12 | WholeSale             |             3 |
|  13 | Retail                |             3 |
|  14 | Whole Sale and Retail |             3 |
|  15 | Alcoholic Beverages Importation |      3 |
|  16 | Duty Free Shop        |             3 |
|  17 | Re-Export             |             2 |
|  18 | Business Service Agency |           3 |
|  19 | Sale Center           |             3 |
|  20 | Show Room             |             3 |
|  21 | EVCycle Show Room     |             3 |
|  22 | EV Show Room          |             3 |
|  23 | OGA Recommendation    |             1 |

## 1. Member

| No. | Report Name                | Stored Procedure                  | API Status |
| --: | -------------------------- | --------------------------------- | ---------- |
|   1 | Member Registration Report | `dbo.sp_MemberRegistrationReport` | Completed  |

## 2. Pathaka

| No. | Report Name                                  | Stored Procedure                     | API Status |
| --: | -------------------------------------------- | ------------------------------------ | ---------- |
|   1 | PaThaKaRegisteredBusinessOrganizationReport  | `dbo.sp_PaThaKaReport`               | Completed  |
|   2 | List of Valid and Invalid Company            | `dbo.sp_PaThaKaValidInvalidReport`   | Completed  |
|   3 | List of Directors By Company Registration No | `dbo.sp_DirectorListReport`          | Completed  |
|   4 | List of Top Capital Company                  | `dbo.sp_PaThaKaReport`               | Completed  |
|   5 | List of Company                              | `dbo.sp_PaThaKaAllReport`            | Completed  |
|   6 | List of Directors                            | `dbo.sp_DirectorListReport`          | Completed  |
|   7 | Registration By Voucher                      | `dbo.sp_PaThaKaRegistrationReport`   | Completed  |
|   8 | Registration By Business Type                | `dbo.sp_PaThaKaByBusinessTypeReport` | Completed  |
|   9 | Company Profile                              | `dbo.sp_CompanyProfileReport`        | Completed  |
|  10 | EIR Card bind Report                         | `dbo.sp_PathakaBindReport`           | Completed  |
|  11 | CardListsByCompanyRegistrationNumber         | `dbo.sp_CardListsByPaThaKaReport`    | Completed  |

## 3. Import Licence

| No. | Report Name                                      | Stored Procedure                          | API Status |
| --: | ------------------------------------------------ | ----------------------------------------- | ---------- |
|   1 | Import Licence Daily Report (New Licence Report) | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|   2 | Import Licence Amendment Report                  | `dbo.sp_AmendReport`                      | Completed  |
|   3 | Import Licence Extension Report                  | `dbo.sp_ExtensionReport`                  | Completed  |
|   4 | Import Licence Cancellation Report               | `dbo.sp_CancelReport`                     | Completed  |
|   5 | Import Licence Detail Report                     | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|   6 | Import Licence By Section Report                 | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|   7 | Import Licence By Method Report                  | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|   8 | Import Licence By Seller Country Report          | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|   9 | Import Licence Company List Report               | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|  10 | Import Licence By HS Code Report                 | `dbo.sp_HSCodeReport`                     | Completed  |
|  11 | Import Licence Total Value & Licences Report     | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|  12 | Import Licence Voucher Report                    | `dbo.sp_VoucherReport`                    | Completed  |
|  13 | Import Licence Actual Amendment Report           | `dbo.sp_ActualAmendReport`                | Completed  |
|  14 | Import Licence New Report (New Report )          | `dbo.sp_NewReport`                        | Completed  |
|  15 | Import Licence Pending Report                    | `dbo.sp_PendingReport`                    | Completed  |
|  16 | Import Licence Detail Report (Pending)           | `dbo.sp_ImportLicencePendingDetailReport` | Completed  |

## 4. Import Permit

| No. | Report Name                                    | Stored Procedure                  | API Status |
| --: | ---------------------------------------------- | --------------------------------- | ---------- |
|   1 | Import Permit Daily Report (New Permit Report) | `dbo.sp_ImportPermitDetailReport` | Completed  |
|   2 | Import Permit Amendment Report                 | `dbo.sp_AmendReport`              | Completed  |
|   3 | Import Permit Extension Report                 | `dbo.sp_ExtensionReport`          | Completed  |
|   4 | Import Permit Cancellation Report              | `dbo.sp_CancelReport`             | Completed  |
|   5 | Import Permit Detail Report                    | `dbo.sp_ImportPermitDetailReport` | Completed  |
|   6 | Import Permit By Section Report                | `dbo.sp_ImportPermitDetailReport` | Completed  |
|   7 | Import Permit By Seller Country Report         | `dbo.sp_ImportPermitDetailReport` | Completed  |
|   8 | Import Permit Company List Report              | `dbo.sp_ImportPermitDetailReport` | Completed  |
|   9 | Import Permit By HS Code Report                | `dbo.sp_HSCodeReport`             | Completed  |
|  10 | Import Permit Voucher Report                   | `dbo.sp_VoucherReport`            | Completed  |
|  11 | Import Permit Actual Amendment Report          | `dbo.sp_ActualAmendReport`        | Completed  |
|  12 | Import Permit New Report (New Report )         | `dbo.sp_NewReport`                | Completed  |

## 5. Border Import Licence

| No. | Report Name                                             | Stored Procedure                          | API Status |
| --: | ------------------------------------------------------- | ----------------------------------------- | ---------- |
|   1 | Border Import Licence Daily Report (New Licence Report) | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|   2 | Border Import Licence Amendment Report                  | `dbo.sp_AmendReport`                      | Completed  |
|   3 | Border Import Licence Extension Report                  | `dbo.sp_ExtensionReport`                  | Completed  |
|   4 | Border Import Licence Cancellation Report               | `dbo.sp_CancelReport`                     | Completed  |
|   5 | Border Import Licence Detail Report                     | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|   6 | Border Import Licence By Section Report                 | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|   7 | Border Import Licence By Method Report                  | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|   8 | Border Import Licence By Seller Country Report          | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|   9 | Border Import Licence Company List Report               | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|  10 | Border Import Licence By HS Code Report                 | `dbo.sp_HSCodeReport`                     | Completed  |
|  11 | Border Import Licence Total Value & Licences Report     | `dbo.sp_ImportLicenceDetailReport`        | Completed  |
|  12 | Border Import Licence Voucher Report                    | `dbo.sp_VoucherReport`                    | Completed  |
|  13 | Border Import Licence Actual Amendment Report           | `dbo.sp_ActualAmendReport`                | Completed  |
|  14 | Border Import Licence New Report (New Report )          | `dbo.sp_NewReport`                        | Completed  |
|  15 | Border Import Licence Pending Report                    | `dbo.sp_PendingReport`                    | Completed  |
|  16 | Border Import Licence Detail Report (Pending)           | `dbo.sp_ImportLicencePendingDetailReport` | Completed  |

## 6. Border Import Permit

| No. | Report Name                                           | Stored Procedure                  | API Status |
| --: | ----------------------------------------------------- | --------------------------------- | ---------- |
|   1 | Border Import Permit Daily Report (New Permit Report) | `dbo.sp_ImportPermitDetailReport` | Completed  |
|   2 | Border Import Permit Amendment Report                 | `dbo.sp_AmendReport`              | Completed  |
|   3 | Border Import Permit Extension Report                 | `dbo.sp_ExtensionReport`          | Completed  |
|   4 | Border Import Permit Cancellation Report              | `dbo.sp_CancelReport`             | Completed  |
|   5 | Border Import Permit Detail Report                    | `dbo.sp_ImportPermitDetailReport` | Completed  |
|   6 | Border Import Permit By Section Report                | `dbo.sp_ImportPermitDetailReport` | Completed  |
|   7 | Border Import Permit By Seller Country Report         | `dbo.sp_ImportPermitDetailReport` | Completed  |
|   8 | Border Import Permit Company List Report              | `dbo.sp_ImportPermitDetailReport` | Completed  |
|   9 | Border Import Permit By HS Code Report                | `dbo.sp_HSCodeReport`             | Completed  |
|  10 | Border Import Permit Voucher Report                   | `dbo.sp_VoucherReport`            | Completed  |
|  11 | Border Import Permit Actual Amendment Report          | `dbo.sp_ActualAmendReport`        | Completed  |
|  12 | Border Import Permit New Report (New Report )         | `dbo.sp_NewReport`                | Completed  |

## 7. Border Export Permit

| No. | Report Name                                           | Stored Procedure                  | API Status |
| --: | ----------------------------------------------------- | --------------------------------- | ---------- |
|   1 | Border Export Permit Daily Report (New Permit Report) | `dbo.sp_ExportPermitDetailReport` | Completed  |
|   2 | Border Export Permit Amendment Report                 | `dbo.sp_AmendReport`              | Completed  |
|   3 | Border Export Permit Extension Report                 | `dbo.sp_ExtensionReport`          | Completed  |
|   4 | Border Export Permit Cancellation Report              | `dbo.sp_CancelReport`             | Completed  |
|   5 | Border Export Permit Detail Report                    | `dbo.sp_ExportPermitDetailReport` | Completed  |
|   6 | Border Export Permit By Section Report                | `dbo.sp_ExportPermitDetailReport` | Completed  |
|   7 | Border Export Permit By Seller Country Report         | `dbo.sp_ExportPermitDetailReport` | Completed  |
|   8 | Border Export Permit Company List Report              | `dbo.sp_ExportPermitDetailReport` | Completed  |
|   9 | Border Export Permit By HS Code Report                | `dbo.sp_HSCodeReport`             | Completed  |
|  10 | Border Export Permit Voucher Report                   | `dbo.sp_VoucherReport`            | Completed  |
|  11 | Border Export Permit Actual Amendment Report          | `dbo.sp_ActualAmendReport`        | Completed  |
|  12 | Border Export Permit New Report (New Report )         | `dbo.sp_NewReport`                | Completed  |

## 8. Export Licence

| No. | Report Name                                      | Stored Procedure                   | API Status |
| --: | ------------------------------------------------ | ---------------------------------- | ---------- |
|   1 | Export Licence Daily Report (New Licence Report) | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|   2 | Export Licence Amendment Report                  | `dbo.sp_AmendReport`               | Completed  |
|   3 | Export Licence Extension Report                  | `dbo.sp_ExtensionReport`           | Completed  |
|   4 | Export Licence Cancellation Report               | `dbo.sp_CancelReport`              | Completed  |
|   5 | Export Licence Detail Report                     | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|   6 | Export Licence By Section Report                 | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|   7 | Export Licence By Method Report                  | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|   8 | Export Licence By Seller Country Report          | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|   9 | Export Licence Company List Report               | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|  10 | Export Licence By HS Code Report                 | `dbo.sp_HSCodeReport`              | Completed  |
|  11 | Export Licence Total Value & Licences Report     | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|  12 | Export Licence Voucher Report                    | `dbo.sp_VoucherReport`             | Completed  |
|  13 | Export Licence Actual Amendment Report           | `dbo.sp_ActualAmendReport`         | Completed  |
|  14 | Export Licence New Report (New Report )          | `dbo.sp_NewReport`                 | Completed  |

## 9. Export Permit

| No. | Report Name                                    | Stored Procedure                  | API Status |
| --: | ---------------------------------------------- | --------------------------------- | ---------- |
|   1 | Export Permit Daily Report (New Permit Report) | `dbo.sp_ExportPermitDetailReport` | Completed  |
|   2 | Export Permit Amendment Report                 | `dbo.sp_AmendReport`              | Completed  |
|   3 | Export Permit Extension Report                 | `dbo.sp_ExtensionReport`          | Completed  |
|   4 | Export Permit Cancellation Report              | `dbo.sp_CancelReport`             | Completed  |
|   5 | Export Permit Detail Report                    | `dbo.sp_ExportPermitDetailReport` | Completed  |
|   6 | Export Permit By Section Report                | `dbo.sp_ExportPermitDetailReport` | Completed  |
|   7 | Export Permit By Seller Country Report         | `dbo.sp_ExportPermitDetailReport` | Completed  |
|   8 | Export Permit Company List Report              | `dbo.sp_ExportPermitDetailReport` | Completed  |
|   9 | Export Permit By HS Code Report                | `dbo.sp_HSCodeReport`             | Completed  |
|  10 | Export Permit Voucher Report                   | `dbo.sp_VoucherReport`            | Completed  |
|  11 | Export Permit Actual Amendment Report          | `dbo.sp_ActualAmendReport`        | Completed  |
|  12 | Export Permit New Report (New Report )         | `dbo.sp_NewReport`                | Completed  |

## 10. Border Export Licence

| No. | Report Name                                             | Stored Procedure                   | API Status |
| --: | ------------------------------------------------------- | ---------------------------------- | ---------- |
|   1 | Border Export Licence Daily Report (New Licence Report) | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|   2 | Border Export Licence Amendment Report                  | `dbo.sp_AmendReport`               | Completed  |
|   3 | Border Export Licence Extension Report                  | `dbo.sp_ExtensionReport`           | Completed  |
|   4 | Border Export Licence Cancellation Report               | `dbo.sp_CancelReport`              | Completed  |
|   5 | Border Export Licence Detail Report                     | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|   6 | Border Export Licence By Section Report                 | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|   7 | Border Export Licence By Method Report                  | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|   8 | Border Export Licence By Seller Country Report          | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|   9 | Border Export Licence Company List Report               | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|  10 | Border Export Licence By HS Code Report                 | `dbo.sp_HSCodeReport`              | Completed  |
|  11 | Border Export Licence Total Value & Licences Report     | `dbo.sp_ExportLicenceDetailReport` | Completed  |
|  12 | Border Export Licence Voucher Report                    | `dbo.sp_VoucherReport`             | Completed  |
|  13 | Border Export Licence Actual Amendment Report           | `dbo.sp_ActualAmendReport`         | Completed  |
|  14 | Border Export Licence New Report (New Report )          | `dbo.sp_NewReport`                 | Completed  |

## 11. Payment

| No. | Report Name            | Stored Procedure              | API Status |
| --: | ---------------------- | ----------------------------- | ---------- |
|   1 | MPU Report             | `dbo.sp_MPUReport`            | Completed  |
|   2 | Cheque No Report       | `dbo.sp_ChequeNoReport`       | Completed  |
|   3 | Online Fees Report     | `dbo.sp_OnlineFeesReport`     | Completed  |
|   4 | Account Summary Report | `dbo.sp_AccountSummaryReport` | Completed  |
|   5 | MPU Report V3          | `dbo.sp_MPUReport_V3`         | Completed  |

## 12. WholeSale

| No. | Report Name                        | Stored Procedure                            | API Status |
| --: | ---------------------------------- | ------------------------------------------- | ---------- |
|   1 | Whole Sale Summary Report          | `dbo.sp_WholeSaleRetailReport`              | Completed  |
|   2 | Whole Sale Detail Report           | `dbo.sp_WholeSaleRetailReport`              | Completed  |
|   3 | WholeSale Registration By Voucher  | `dbo.sp_WholeSaleRetailRegistrationReport`  | Completed  |

## 13. Retail

| No. | Report Name                    | Stored Procedure                            | API Status |
| --: | ------------------------------ | ------------------------------------------- | ---------- |
|   1 | Retail Summary Report          | `dbo.sp_WholeSaleRetailReport`              | Completed  |
|   2 | Retail Detail Report           | `dbo.sp_WholeSaleRetailReport`              | Completed  |
|   3 | Retail Registration By Voucher | `dbo.sp_WholeSaleRetailRegistrationReport`  | Completed  |

## 14. Whole Sale and Retail

| No. | Report Name                             | Stored Procedure                            | API Status |
| --: | --------------------------------------- | ------------------------------------------- | ---------- |
|   1 | Whole Sale and Retail Summary Report    | `dbo.sp_WholeSaleRetailReport`              | Completed  |
|   2 | Whole Sale and Retail Detail Report     | `dbo.sp_WholeSaleRetailReport`              | Completed  |
|   3 | WS and R Registration By Voucher        | `dbo.sp_WholeSaleRetailRegistrationReport`  | Completed  |

## 15. Alcoholic Beverages Importation

| No. | Report Name                                             | Stored Procedure                                  | API Status |
| --: | ------------------------------------------------------- | ------------------------------------------------- | ---------- |
|   1 | Alcoholic Beverages Importation Summary Report          | `dbo.sp_WineImportationReport`                    | Completed  |
|   2 | Alcoholic Beverages Importation Detail Report           | `dbo.sp_WineImportationReport`                    | Completed  |
|   3 | AB Registration By Voucher                              | `dbo.sp_WineImportationRegistrationReport`        | Completed  |

## 16. Duty Free Shop

| No. | Report Name                              | Stored Procedure                                | API Status |
| --: | ---------------------------------------- | ----------------------------------------------- | ---------- |
|   1 | Duty Free Shop Summary Report            | `dbo.sp_DutyFreeShopReport`                     | Completed  |
|   2 | Duty Free Shop Detail Report             | `dbo.sp_DutyFreeShopReport`                     | Completed  |
|   3 | Duty Free Shop Registration By Voucher   | `dbo.sp_DutyFreeShopRegistrationReport`         | Completed  |

## 17. Re-Export

| No. | Report Name              | Stored Procedure          | API Status |
| --: | ------------------------ | ------------------------- | ---------- |
|   1 | Re-Export Summary Report | `dbo.sp_ReExportReport`   | Completed  |
|   2 | Re-Export Detail Report  | `dbo.sp_ReExportReport`   | Completed  |

## 18. Business Service Agency

| No. | Report Name                                    | Stored Procedure                                      | API Status |
| --: | ---------------------------------------------- | ----------------------------------------------------- | ---------- |
|   1 | Business Service Agency Summary Report         | `dbo.sp_BusinessServiceAgencyReport`                  | Completed  |
|   2 | Business Service Agency Detail Report          | `dbo.sp_BusinessServiceAgencyReport`                  | Completed  |
|   3 | BSA Registration By Voucher                    | `dbo.sp_BusinessServiceAgencyRegistrationReport`      | Completed  |

## 19. Sale Center

| No. | Report Name                         | Stored Procedure                          | API Status |
| --: | ----------------------------------- | ----------------------------------------- | ---------- |
|   1 | Sale Center Summary Report          | `dbo.sp_SaleCenterReport`                 | Completed  |
|   2 | Sale Center Detail Report           | `dbo.sp_SaleCenterReport`                 | Completed  |
|   3 | Sale Center Registration By Voucher | `dbo.sp_SaleCenterRegistrationReport`     | Completed  |

## 20. Show Room

| No. | Report Name                       | Stored Procedure                        | API Status |
| --: | --------------------------------- | --------------------------------------- | ---------- |
|   1 | Show Room Summary Report          | `dbo.sp_ShowRoomReport`                 | Completed  |
|   2 | Show Room Detail Report           | `dbo.sp_ShowRoomReport`                 | Completed  |
|   3 | Show Room Registration By Voucher | `dbo.sp_ShowRoomRegistrationReport`     | Completed  |

## 21. EVCycle Show Room

| No. | Report Name                               | Stored Procedure                               | API Status |
| --: | ----------------------------------------- | ---------------------------------------------- | ---------- |
|   1 | EVCycle Show Room Summary Report          | `dbo.sp_EVCycleShowRoomReport`                 | Completed  |
|   2 | EVCycle Show Room Detail Report           | `dbo.sp_EVCycleShowRoomReport`                 | Completed  |
|   3 | EVCycle Show Room Registration By Voucher | `dbo.sp_EVCycleShowRoomRegistrationReport`     | Completed  |

## 22. EV Show Room

| No. | Report Name                          | Stored Procedure                          | API Status |
| --: | ------------------------------------ | ----------------------------------------- | ---------- |
|   1 | EV Show Room Summary Report          | `dbo.sp_EVShowRoomReport`                 | Completed  |
|   2 | EV Show Room Detail Report           | `dbo.sp_EVShowRoomReport`                 | Completed  |
|   3 | EV Show Room Registration By Voucher | `dbo.sp_EVShowRoomRegistrationReport`     | Completed  |

## 23. OGA Recommendation

| No. | Report Name               | Stored Procedure                         | API Status |
| --: | ------------------------- | ---------------------------------------- | ---------- |
|   1 | OGA Recommendation Report | `dbo.sp_OGARecommendationListReport`     | Completed  |

