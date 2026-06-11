# User roles, hierarchy & system flow

LGB Services has **4 active login roles**. Internal users can also have **permission flags** and **`IsInternalSignatory`** — overlays on `Admin` / `User`, not separate roles.

**Full document (PDF):** [USER_ROLES.pdf](./USER_ROLES.pdf)  
**Enlarged flowcharts only (PDF):** [USER_ROLES_FLOWCHARTS.pdf](./USER_ROLES_FLOWCHARTS.pdf)  
**Regenerate PDFs:** `python3 docs/build-user-roles-pdf.py` (from repo root)

---

## 1. Active roles (4)

| # | Role | Side | Scoped to | Typical UI |
|---|------|------|-----------|------------|
| 1 | **Admin** | LGB internal | All customers | Dashboard, customers, packages, admin, workflow config |
| 2 | **User** | LGB internal | Jobs assigned to them | My work tracker, assigned package lines, forms |
| 3 | **ClientAdmin** | Customer external | One `CustomerId` | Client portal, packages, team & signatories |
| 4 | **ClientSignatory** | Customer external | One company + their name on forms | **My documents** only |

**Legacy:** `Client` → migrated to `ClientAdmin` on startup (not used for new accounts).

---

## 2. Role hierarchy

```mermaid
flowchart TB
  subgraph LGB["LGB INTERNAL"]
    direction TB
    A["Admin<br/>Sharon — all customers, packages, users, workflow config"]
    U["User — secretarial / resolution<br/>Ng Poh Li, Nita, Siti, Nadia"]
    A --> U
    subgraph FLAGS["Permission overlays on Admin / User"]
      direction LR
      F1["CanApproveMoiIntake"]
      F2["CanRecommendMoi"]
      F3["CanApproveMoi"]
      F4["CanApproveMoa"]
      F5["IsInternalSignatory"]
    end
    A --- FLAGS
    U --- FLAGS
  end

  subgraph EXT["CUSTOMER EXTERNAL — scoped by CustomerId"]
    direction TB
    CA["ClientAdmin<br/>Auto per company — portal, issue MOI, team"]
    CS["ClientSignatory<br/>Per account holder — My documents only"]
    CA --> CS
  end

  LGB -.->|"creates & manages"| EXT
```

### Access ladder (high → low)

```
Admin (internal)
  └── User (internal secretary)        ← assigned per job; prep MOI/MOA
        └── [+ permission flags]       ← intake, recommend, MOI/MOA sign-off
        └── [+ IsInternalSignatory]    ← named MOA workflow step approver

──────────── customer boundary ────────────

ClientAdmin (external)                 ← one company; full client portal
  └── ClientSignatory (external)       ← one company; only their forms
```

---

## 3. Internal permission overlays

| Flag | Who typically | What it does |
|------|----------------|--------------|
| `CanApproveMoiIntake` | Sharon, named intake approvers | Approve client-submitted MOI → resolution can start |
| `CanRecommendMoi` | Division recommenders (matrix) | Recommend MOI after secretary prep |
| `CanApproveMoi` | Sharon | Final internal MOI sign-off |
| `CanApproveMoa` | Sharon | Oversight on MOA workflow (sees MOA before wide assignment) |
| `IsInternalSignatory` | CFO, Ms Teh, etc. | Named approver on **internal** MOA workflow steps |

Configured in **Admin → Users** and **Admin → Workflow config** (link user to MOA template step).

---

## 4. Customer onboarding (who gets an account)

```mermaid
flowchart TD
  START(["Sharon — Admin"]) --> CREATE["Create / edit customer<br/>contacts + MOI / MOI Approval / MOA flags"]
  CREATE --> SYNC["Sync package jobs<br/>Service lines + form jobs"]
  CREATE --> AUTO_CA["Ensure ClientAdmin account<br/>one per company"]
  CREATE --> AUTO_CS["Ensure ClientSignatory accounts<br/>per flagged contact with email"]
  AUTO_CA --> CA_LOGIN["ClientAdmin logs in<br/>full client portal"]
  AUTO_CS --> CS_LOGIN["ClientSignatory logs in<br/>My documents only"]
  SYNC --> PKG["Package workboard rows<br/>Annual Return, MOI, MOA, etc."]
```

| Trigger | Account created | Role |
|---------|-----------------|------|
| Customer saved | 1× company admin | `ClientAdmin` |
| Contact flagged MOI / MOI Approval / MOA + email | 1× per contact | `ClientSignatory` |
| ClientAdmin adds signatory (Team tab) | Holder + login | `ClientSignatory` (marked **Client-added**) |
| Admin creates user | Manual | Any of 4 roles |

---

## 5. End-to-end MOI/MOA pipeline (roles at each step)

This is the **implemented** flow today. Steps marked *planned* are not built yet.

```mermaid
flowchart TD
  subgraph CLIENT["CLIENT SIDE"]
    CA["ClientAdmin<br/>issue MOI per package line<br/>set dates, manage team"]
    CS_MOI["ClientSignatory — MOI holder<br/>fill & submit MOI form"]
    CS_MOA["ClientSignatory — MOA holder<br/>approve MOA when visible"]
    CA -->|"Start MOI"| CS_MOI
  end

  CS_MOI --> S1["Status: Awaiting intake<br/>Handoff: ClientSubmitted"]

  subgraph INTAKE["INTAKE — CanApproveMoiIntake"]
    SH1["Admin / Sharon<br/>Approve MOI intake"]
  end

  S1 --> SH1
  SH1 --> S2["Status: Resolution prep<br/>Handoff: PendingPrep"]

  subgraph RESO["RESOLUTION — User role assigned"]
    SEC["Secretarial team<br/>Ng Poh Li, Nita, Siti, Nadia"]
    SH2["Admin: Assign secretarial team<br/>shortcut after MOI sign-off"]
    SEC --> PREP["Complete MOI prep<br/>recommendation fields"]
  end

  S2 --> SH2
  SH2 --> SEC

  PREP --> S3["CanRecommendMoi<br/>division recommender"]
  S3 --> S4["CanApproveMoi<br/>MOI sign-off — Sharon"]
  S4 --> S5["Status: MOI approved<br/>Handoff: AdminReview"]

  S5 --> MOA_PREP["Secretaries prepare MOA<br/>pack checklist — internal only"]
  MOA_PREP --> SH3["Admin: Send MOA to client<br/>Approve for MOA"]
  SH3 --> S6["Status: Ready for MOA<br/>Client can see MOA"]

  S6 --> CS_MOA
  SH3 --> MOA_INT["Start MOA workflow<br/>MoaCirculation"]

  subgraph MOA_WF["INTERNAL MOA WORKFLOW"]
    direction TB
    IS["IsInternalSignatory users<br/>CFO, Ms Teh, etc."]
    SH4["CanApproveMoa — Sharon oversight"]
    IS --> STEPS["Sequential approval steps<br/>template A / B / C per division"]
    SH4 --> STEPS
  end

  MOA_INT --> MOA_WF
  MOA_WF --> DONE["Status: Completed<br/>execution phase — planned"]
```

### Step-by-step (role column)

| Step | Display status (examples) | Handoff | Primary role(s) |
|------|---------------------------|---------|-----------------|
| Package synced | MOI not received | — | **Admin** (Sharon) creates customer |
| Client starts MOI | Awaiting intake | `ClientSubmitted` | **ClientAdmin** issues; **ClientSignatory** fills |
| Intake approved | Resolution prep | `PendingPrep` | **CanApproveMoiIntake** |
| Secretary works MOI | Resolution prep → Pending recommendation | `ResoInProgress` | **User** (assigned) |
| Recommended | MOI sign-off | — | **CanRecommendMoi** |
| MOI signed off | MOI approved | `AdminReview` | **CanApproveMoi** |
| MOA pack prep | MOI approved / internal | `AdminReview` | **User** via **Assign secretarial team** |
| Sent to client | Ready for MOA | `ReadyForMoa` | **Admin** — Approve for MOA |
| Client MOA | MOA circulation | `MoaCirculation` | **ClientSignatory** (MOA holder) |
| Internal MOA chain | MOA circulation | `MoaCirculation` | **IsInternalSignatory** + **CanApproveMoa** |
| Done | Completed | `Completed` | — |

---

## 6. Job types & who sees them

```mermaid
flowchart LR
  subgraph JOBS["Job rows per customer / package"]
    SVC["Service<br/>e.g. Annual Return<br/>one MOI per line"]
    MOI["MOI job<br/>per MOI holder"]
    MOIA["MOI Approval job<br/>per approval holder"]
    MOA["MOA job<br/>per MOA holder"]
  end

  subgraph WHO_SEES["Who sees what"]
    ADM["Admin — all"]
    USR["User — assigned units only"]
    CA["ClientAdmin — company jobs"]
    CS["ClientSignatory — name match only"]
  end

  SVC --> MOI_FORM["Draft MOI linked"]
  MOI --> MOI_FORM
  MOIA --> MOI_FORM2["Paired MOI status"]
  MOA --> MOA_FORM["MOA form + workflow"]

  ADM --> JOBS
  USR --> SVC
  USR --> MOA
  CA --> SVC
  CA --> MOI
  CS --> SVC
  CS --> MOI
  CS -.->|"hidden until ReadyForMoa"| MOA
```

| Job `TaskType` | Purpose | ClientAdmin | ClientSignatory | User (secretary) |
|----------------|---------|-------------|-----------------|------------------|
| `Service` | Package deliverable (e.g. Annual Return) | Issue/open MOI | Start/open own MOI | If assigned |
| `MOI` | Form job per MOI holder | Company view | Own forms | If assigned |
| `MOI Approval` | Parallel approval track | Hidden on portal | *thin UX* | Internal board |
| `MOA` | MOA per MOA holder | After Ready for MOA | After Ready for MOA | Prep before client send |

---

## 7. Display status lifecycle

```mermaid
stateDiagram-v2
  direction LR

  [*] --> MoiNotReceived: Service line synced
  MoiNotReceived --> AwaitingIntake: Client issues MOI
  AwaitingIntake --> ResolutionPrep: Intake approved
  ResolutionPrep --> PendingRecommendation: Secretary prep
  PendingRecommendation --> MoiSignOff: Recommended
  MoiSignOff --> MoiApproved: MOI sign-off
  MoiApproved --> MoaPrepInternal: Assign secretarial team
  MoaPrepInternal --> ReadyForMoa: Approve for MOA
  ReadyForMoa --> MoaCirculation: Start MOA workflow
  MoaCirculation --> Completed: Workflow complete
  Completed --> [*]
```

---

## 8. Client vs internal signatory

```mermaid
flowchart TB
  subgraph CLIENT_SIG["CLIENT SIGNATORY — external"]
    CS_ROLE["Role: ClientSignatory"]
    CS_WHO["Account holders flagged MOI / MOI Approval / MOA"]
    CS_SCOPE["One CustomerId + AccountHolder name match"]
    CS_UI["UI: My documents"]
    CS_ROLE --> CS_WHO --> CS_SCOPE --> CS_UI
  end

  subgraph INT_SIG["INTERNAL SIGNATORY — LGB staff"]
    IS_FLAG["User + IsInternalSignatory flag"]
    IS_WHO["CFO, Ms Teh, division approvers"]
    IS_SCOPE["MOA workflow step assignee"]
    IS_UI["Admin workboard + MOA modal steps"]
    IS_FLAG --> IS_WHO --> IS_SCOPE --> IS_UI
  end
```

| | Client signatory | Internal signatory |
|--|------------------|-------------------|
| **Role** | `ClientSignatory` | `User` (+ optional flags) |
| **Flag** | — | `IsInternalSignatory` |
| **MOI** | Fill/submit as holder | Prep when assigned |
| **MOA** | Approve when sent to client | Approve workflow steps before/during circulation |
| **Portal** | My documents | Internal workboard |

---

## 9. Seed / example accounts

| Person | Role | Flags |
|--------|------|-------|
| Sharon | Admin | Intake, Recommend, MOI sign-off, MOA sign-off |
| Ng Poh Li, Nita, Siti, Nadia | User | — (assigned only) |
| `{company} Admin` | ClientAdmin | Auto per customer |
| MOI/MOA contacts | ClientSignatory | Auto from account holders |
| CFO, Ms Teh, … | User | `IsInternalSignatory` + workflow step link |

---

## 10. Summary

- **4 roles:** `Admin`, `User`, `ClientAdmin`, `ClientSignatory`
- **2 sides:** LGB internal vs customer external (`CustomerId` boundary)
- **5 internal overlays:** intake, recommend, MOI sign-off, MOA oversight, internal MOA signatory
- **Client never sees MOA** until **Ready for MOA** (after Sharon approves for MOA)
- **Secretarial shortcut:** after MOI sign-off, **Assign secretarial team** adds all resolution `User` accounts to the job + MOA row

---

## Code references

| Area | Path |
|------|------|
| Roles | `LGBApp.Backend/Models/UserRoles.cs` |
| User flags | `LGBApp.Backend/Models/User.cs` |
| Handoff states | `LGBApp.Backend/Services/JobHandoffService.cs` |
| Status labels | `LGBApp.Backend/Services/PackageItemStatusResolver.cs` |
| Secretarial assign | `LGBApp.Backend/Services/JobRequestAssignmentService.cs` |
| Client signatory provision | `LGBApp.Backend/Services/CustomerSignatoryProvisioner.cs` |
| Frontend roles | `LGBApp.Frontend/src/lib/roles.ts` |
