# MPU Voucher No parity — 2026-09-17

**Deploy the procedures BEFORE the application.** Against the stale procedures the reports
keep printing the wrong voucher numbers, and the application build carries no fix of its
own for this half.

| Run order | Procedure | Report |
|---|---|---|
| `01` | `sp_MPUReport_V3_pagination` | MPU Report V3 |
| `02` | `sp_MPUReport_pagination` | MPU Report (V1) |

Target database: **TradeNetDB** (not `ReportTemplateDB` — that one only holds the Excel
export job queue; deploying report procedures into it is a known trap).

---

## Why

ငွေစာရင်း department, 2026-09-17:

> MPU Report V3 မှာ ပြနေတဲ့ voucher no တွေက မှားနေပါတယ်။ account summary မှာ ပြနေတဲ့ voucher no
> နဲ့မတူဘူး။ account summary မှာ ပြနေတဲ့ voucher no က မှန်ပါတယ်။

Both reports print the **same column**, `AccountTransaction.VoucherNo`. They differ in
**which row** they read it from.

`MPUPaymentTransaction` has no foreign key to `AccountTransaction`. The only link is the
shared application id `TransactionId`, which is one-to-many on both sides, so the voucher
has to be found by pairing the Nth card charge with the Nth account transaction. Two
things were breaking that pairing.

### 1. The ordinals were counted over the report's own filtered rows

`ROW_NUMBER()` on the MPU side was computed **after** the date window, `ResponseCode`,
`FormType` and `PaymentType` filters (`sp_MPUReport_V3_pagination.sql:37-52` before this
release). The legacy Tradenet 2.0 `sp_MPUReport_V3` numbered the whole table and filtered
afterwards (`docs/StoredProcedureDefinitions.sql:6191-6197` vs `:6239-6246`), so this was
introduced by the pagination migration.

The effect is that **a row's Voucher No depends on the date range the user picks**.
Measured on the live PROD API, same rows, two nested windows:

| Window | Rows sampled | Rows also in the other window | Voucher No differs |
|---|---|---|---|
| 2025-02-10 .. 2025-02-16 | 1,000 | 112 | **8** |
| 2025-01-01 .. 2025-02-28 | 1,000 | — | — |

```
id=1750683 reg=133823778  one week: U01202524646   two months: U02202515580
id=1749813 reg=111839476  one week: U01202507303   two months: U02202514741
id=1753574 reg=103421780  one week: U01202509293   two months: U02202520521
```

### 2. Any `AccountTransaction` row was a candidate

The `#acc` set had no `IsPayment` predicate, so a provisional or non-payment record could
win the pairing and hand the report a voucher Account Summary will never display —
`sp_AccountSummaryReport_pagination.sql:58` shows only `IsPayment = 1`.

### 3. Payments that were never made by card still took an ordinal

An application's fees are not all paid by card. A payment taken at the counter also
creates an `AccountTransaction` with a voucher, and while it sits in the sequence it
consumes an ordinal and pushes every later card charge onto the wrong voucher. Nothing in
the table marks which is which — but **time does**, and cleanly. Over the same 1,000 PROD
rows:

| | rows | paid within 1 day of the charge | median gap |
|---|---|---|---|
| voucher month **==** charge month | 950 | **100.0%** | 0.00 days |
| voucher month **!=** charge month | 50 | **0.0%** | 42.27 days |

The two populations do not overlap, so a payment more than two days from every one of the
application's charges is not that charge's payment and is not a candidate.

Together these put a voucher from a **different month** on 5–7% of rows. The voucher
number encodes its own month (`U02202515580` = February 2025), so this is visible without
consulting Account Summary at all:

| Window | Rows | Voucher month == charge month | Differs |
|---|---|---|---|
| 2025-02-10 .. 2025-02-16 | 1,000 | 932 | **68 (6.8%)** |
| 2025-02-01 .. 2025-02-28 | 1,000 | 950 | **50 (5.0%)** |

```
id=1745984 charged 2025-02-10 -> voucher U11202421628   (November 2024)
id=1750334 charged 2025-02-13 -> voucher U12202416517   (December 2024)
id=1747114 charged 2025-02-10 -> voucher U04202443562   (April 2024)
id=1761267 charged 2025-02-18 -> voucher U08202404604   (August 2024)
```

---

## What changes

### `01_sp_MPUReport_V3_pagination.sql`

The pairing is rebuilt in three steps instead of one:

1. `#ids` — the applications the user's filters select.
2. `#mpuAll` — **every** successful (`ResponseCode = '00'`) charge of those applications,
   whenever it happened, numbered by `(TransactionDateTime, Id)`. A failed attempt creates
   no `AccountTransaction`, so it must not consume an ordinal.
3. `#mpu` — only now the date window, `FormType` and `PaymentType`.

`#acc` is narrowed twice — to `IsPayment = 1 AND VoucherNo IS NOT NULL` (exactly the rows
Account Summary can display), and then to payments within `@PairingToleranceDays` (2) of
one of the application's charges — and numbered among themselves. The final
`WHERE a.VoucherNo IS NOT NULL` becomes redundant and is gone.

Nothing else moves: the same columns, the same sort whitelist, the same paging.

Verified on a scratch database against the three shapes that were failing:

| Case | Before | After |
|---|---|---|
| Feb 2025 charge, application also holds an Apr 2024 counter payment | `U04202443562` | `U02202515580` |
| Failed card attempt before the successful one | shifted ordinal | correct voucher |
| Same row read over one week vs two months | two different vouchers | identical |

### `02_sp_MPUReport_pagination.sql`

MPU Report V1 finds its voucher a third way again — `TOP 1 … ORDER BY CreatedDate DESC`
with the `@OnlineFeeTotalAmount` split. That rule is kept; all four subqueries (voucher and
amount, in both the fee and online-fee branches) gain the same
`IsPayment = 1 AND VoucherNo IS NOT NULL` restriction, so V1 cannot show a voucher Account
Summary lacks either. **Both** subqueries in a pair carry the identical predicate, or the
voucher and the amount would come from two different rows.

Nobody complained about V1; it is included because it had the same defect and the three
ငွေစာရင်း screens should agree.

---

## Sequence

1. `CaptureRollback.sql` — save the result grid. This is the rollback artifact.
2. `00_RunAll.sql` (or `01…` then `02…`).
3. `VerifyDeployment.sql`.
4. Deploy the application.

## Rollback

Take the definition text saved in step 1, replace the leading `CREATE` with
`CREATE OR ALTER`, and execute it.

## Acceptance

Re-run the wrong-month check against the live API afterwards. The share of rows whose
voucher month differs from the charge month should fall from 5–7% to ~0, and a row's
voucher must no longer change when the date range widens.
