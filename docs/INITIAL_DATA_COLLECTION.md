# Initial data collection (Sharon)

Workbook: **[templates/LGB_Customer_Initialization_Template.xlsx](templates/LGB_Customer_Initialization_Template.xlsx)**  
Open in **Excel 365 / 2021+** (MOA setup uses FILTER formulas).

```bash
python3 scripts/build_sharon_init_template.py
python3 scripts/import-sharon-workbook.py --email sharon@lgb.test
```

---

## Fill order

1. **Who we invoice**
2. **Divisions** — MOI managers + emails
3. **Companies** — division, LOA, billing
4. **Client contacts** — MOI / MOA flags, who starts requests
5. **Company MOA setup** — **one row per company**, pick workflow, fill approver blanks
6. **Contracts** — packages + extra LGB staff only

Reference: **MOA workflow steps** (standardised paths), **Package list**

---

## Company MOA setup (main MOA tab)

Each company can use a **different MOA workflow** (No LOA / With LOA / SWM).

| Colour | Meaning |
|--------|---------|
| **Row 2** | Orange hints — what each column needs for the workflow you picked |
| **Blue** | Auto — pulls from Client contacts or Companies |
| **Yellow** | Sharon fills — names/emails that differ per company (CEO, CFO, board, etc.) |

**Auto columns**
- Project initiator ← contact with “Usually starts the request?” = Y
- MOA signers ← contacts with “Signs MOA?” = Y
- LOA holder 1 name ← Companies “LOA holder names”

**Fill columns** (only when row 2 hint ≠ `—`)
- CFO / Finance, CEO / GM (No LOA + finance matters)
- Board members (No LOA)
- Extra LOA holder 2 (SWM)
- LGB prepared-by / vetted-by emails

**Companies** tab shows MOA workflow via formula from this sheet.

---

## Import

Script reads **Company MOA setup** for MOA workflow + board/LOA names. Client contacts still drive signatory logins.
