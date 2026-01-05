# Project Evaluation Report (HCI / CCP)

**Project Name:** MessHub – Mess Management System  
**Course:** Introduction to Human Computer Interaction  
**Date:** 2026-01-04  
**Platform:** ASP.NET Core MVC (Razor Views), EF Core, SQL Server, Tailwind/CSS  

---

## 0) Project Overview (Context)
MessHub is a role-based mess management web application designed to manage daily attendance (Breakfast/Lunch/Dinner), tea consumption, billing periods, payments, and period reports.

**Primary modules observed in the codebase:**
- Authentication & account recovery (login, forgot/reset password)
- Admin management: Members, Menu/Dish Plans, Mess Periods
- Daily operations: Attendance marking, Tea marking
- Member self-service: “My Attendance”, “My Tea”, “My Payments”
- Finance & compliance: Payment tracking (including approval workflow), Report generation and export (CSV/PDF)

---

## 1) Requirements

### 1a) Identified Primary Users
**User Group 1: Mess Admin / Manager (role: Admin)**
- Marks daily attendance for all members.
- Marks daily tea consumption entries for members.
- Manages members, dish plans (menu), and mess billing periods.
- Reviews/approves submitted payments.
- Generates reports for a billing period (CSV/PDF export).

**User Group 2: Mess Member / Resident (role: User/Member)**
- Views their attendance records.
- Verifies/declines each meal attendance entry to prevent disputes.
- Views tea records and verifies/declines entries.
- Views payments and remaining balance.

**Secondary/Optional stakeholders (not separate roles in current app)**
- Hostel warden/accountant (typically uses Admin access to export reports).

---

### 1b) Functional & Non-Functional Requirements

#### Functional Requirements (extracted from controllers/views)
**Authentication & Security**
- User can log in using username/password.
- System blocks deactivated accounts and provides clear messages.
- User can request password reset via email link and reset password using a token.

**Admin – Core Operations**
- Admin can manage members (create/edit/deactivate patterns visible; create/edit implemented).
- Admin can manage dish plans/menu (CRUD).
- Admin can manage mess periods (CRUD, with “only one active” behavior).
- Admin can mark attendance for all members for a selected date.
- Admin can assign meal-specific dish plans for the selected day-of-week.
- Admin can view attendance analytics via a period calendar heatmap.
- Admin can mark tea entries for all members and view period calendar.
- Admin can view payment summary for a period (charges vs paid vs remaining), and see breakdown by method.
- Admin can approve/reject member-submitted payments (pending approvals).
- Admin can generate reports per period and export CSV/PDF.

**Member – Self Service**
- Member can view “My Attendance” and verify each meal (Breakfast/Lunch/Dinner) or verify all.
- Member can decline incorrect attendance entries (per meal).
- Member can view “My Tea” and verify/decline entries or verify all.
- Member can view “My Payments” and see balance/charges (period-based).

#### Non-Functional Requirements
**Usability**
- Consistent navigation across all screens (shared header + breadcrumbs).
- Clear system feedback for actions (success alerts, pending counts, badges).

**Performance**
- Pages should render quickly for typical mess sizes (e.g., 30–200 members). Tables and calendars must remain usable.

**Security & Privacy**
- Role-based authorization for Admin features.
- Password hashing (ASP.NET `PasswordHasher`).
- Reset tokens with expiry.

**Reliability**
- Data integrity for period boundaries (attendance/tea/payments summarized by active/selected period).

**Accessibility (baseline)**
- Keyboard accessible navigation, visible focus, readable typography, and color contrast that meets WCAG expectations.

---

### 1c) User Needs, Pain Points, and Usage Scenarios

#### Key user needs
**Admin needs**
- Fast daily marking: reduce time and errors when marking multiple members.
- Period overview: quickly see which days have data and where gaps exist.
- Financial clarity: see dues and collection rate; export for record-keeping.

**Member needs**
- Trust & transparency: ability to verify/decline records to avoid disputes.
- Simple status visibility: “pending verifications” should be obvious.
- Understand charges: meal, tea, water charges per period and payments made.

#### Common pain points (domain)
- Manual marking errors cause disputes (“I wasn’t present” / “I didn’t take tea”).
- High cognitive load when scanning long lists/tables.
- Confusion between current period vs past periods.

#### Usage scenarios (realistic)
1) **Daily attendance (admin):** At breakfast time, admin selects today’s date, reviews meal checkboxes/dishes, presses Save, sees confirmation.
2) **Dispute prevention (member):** Member opens “My Attendance”, sees pending verification cards, verifies meals they took and declines incorrect ones.
3) **Tea tracking (admin→member):** Admin marks cups per member; member later verifies/declines entries.
4) **End-of-month settlement (admin):** Admin opens Payments/Reports for period, sees outstanding balances and exports PDF report.

---

### 1d) Personas and User Stories

#### Persona 1: “Ayesha” – Mess Admin (Operations-Focused)
- **Age:** 24–35  
- **Context:** Runs daily mess operations for a hostel mess; uses a laptop at a desk.  
- **Goals:** Mark attendance and tea quickly; minimize mistakes; generate reports; monitor dues.  
- **Frustrations:** Repetitive marking; small UI controls; unclear confirmation; hard to spot anomalies in long tables.

**User stories (Admin)**
- As an admin, I want to mark attendance for all members for a selected date so that meal charges are accurate.
- As an admin, I want a period calendar view so I can instantly see which days have attendance data.
- As an admin, I want to approve or reject pending payments so that billing remains consistent.
- As an admin, I want to export a period report as PDF/CSV so I can share and archive it.

#### Persona 2: “Hamza” – Hostel Member (Self-Service, Trust)
- **Age:** 18–24  
- **Context:** Student using mobile phone; checks status late evening.  
- **Goals:** Verify daily meals/tea; understand dues; avoid being charged incorrectly.  
- **Frustrations:** Hard-to-find pending items; unclear what a button does; fear of “accepting” wrong record.

**User stories (Member)**
- As a member, I want to see all pending meal verifications so I can confirm or dispute them.
- As a member, I want to verify all pending items quickly when they’re correct.
- As a member, I want to see tea cups and charges so I can track my spending.

---

## 2) Interface Design & Usability Principles

### 2a) Interface Layout, Navigation Clarity, Screen Flow
**Global layout (observed):**
- Sticky top header with branding and role-aware navigation.
- Breadcrumbs on major screens (Dashboard → Module).
- Consistent “page header” pattern: title + icon + short description.
- Cards for summary stats; tables for detailed lists; calendars for period overview.

**Navigation clarity features (observed):**
- Admin sees full navigation: Members, Menu, Periods, Attendance, Tea, Payments, Reports.
- Admin has a clear **Admin/User Mode toggle** (stored in localStorage) to switch between admin tools vs member-like views.
- Mobile navigation exists (hamburger menu with links).

**Core screen flow (high-level):**
- Login → Dashboard
- Admin:
  - Dashboard → Attendance Management → Save → success alert
  - Dashboard → Tea Management → Save → success alert
  - Dashboard → Payments → Pending Approvals → Approve/Reject → success alert
  - Dashboard → Reports → Select Period → View summary + export
- Member:
  - Dashboard → My Attendance → Verify/Decline or Verify All
  - Dashboard → My Tea → Verify/Decline or Verify All
  - Dashboard → My Payments

---

### 2b) Application of Usability Heuristics
(Using Nielsen’s 10 heuristics; tied to actual UI patterns seen.)

1) **Visibility of system status**
- Success messages via alerts after Save/Verify actions.
- Stats cards show totals (e.g., today’s meal counts, pending verification counts).
- Report screen shows collection progress bar with percentage.

2) **Match between system and real world**
- Uses familiar domain language: Breakfast/Lunch/Dinner, cups, “Rs.” currency, billing periods.

3) **User control and freedom**
- Member can verify or decline entries (undo-like capability at the record level).
- “Verify all” provides efficiency while keeping user in control.

4) **Consistency and standards**
- Shared layout, repeated patterns (cards, badges, buttons) across modules.
- Icons + labels improve recognition.

5) **Error prevention**
- Forms include server-side validation and anti-forgery tokens.
- Period selector prevents accidental mixing of periods.

6) **Recognition rather than recall**
- Calendars show which days have data.
- Pending panels list “what needs action” rather than requiring user to remember.

7) **Flexibility and efficiency of use**
- Admin quick actions on dashboard.
- Member “Verify All”.

8) **Aesthetic and minimalist design**
- Visual hierarchy: title → description → cards → details.
- Uses whitespace and consistent components.

9) **Help users recognize, diagnose, recover from errors**
- Login clearly states: user not found vs deactivated vs incorrect password.

10) **Help and documentation**
- No dedicated help page found; guidance is embedded in page descriptions and labels.

---

### 2c) Cognitive & Physical Load Considerations
**Cognitive load reduction (observed):**
- Dashboard cards summarize key stats so users don’t interpret raw tables first.
- Color-coded badges and status indicators (pending vs verified vs paid).
- Calendars aggregate daily data with intensity mapping.

**Potential high-load areas (risk):**
- Large member tables (Payments/Reports) can become dense for bigger mess sizes.

**Physical load (observed):**
- Large clickable buttons; checkboxes and select inputs styled for usability.
- Mobile-friendly navigation and responsive layout.

---

### 2d) Appropriateness of Color, Typography, Iconography
**Typography**
- Uses Inter (web font) for legibility and modern UI.

**Iconography**
- Uses Lucide icons consistently with labels, supporting recognition.

**Color usage**
- Semantic color mapping is consistent:
  - Green/emerald: success/verified/paid
  - Orange/amber: warning/pending
  - Red: dues/errors
  - Cyan: primary actions and navigation

---

### 2e) Accessibility Design (contrast, readability, inclusive options)
**Accessibility features present (observed in layout/CSS):**
- Skip link (“Skip to main content”).
- Visible focus styling (`:focus-visible`).
- Responsive design (mobile nav).
- `prefers-reduced-motion` support.
- `prefers-contrast: high` adjustments.

**Likely manual checks still required:**
- Verify WCAG contrast ratios for text on gradients/colored badges.
- Keyboard-only navigation testing for all interactive controls.
- Screen reader labeling checks for custom controls (e.g., toggle switch, icon-only buttons).

---

## 3) Prototype Development (Low-/High-Fidelity)

### 3a) Wireframes / Mockups
**High-fidelity prototype:** The current implementation (Razor Views + Tailwind styling) is a high-fidelity prototype close to production UI.

**Low-fidelity wireframes (text-based; you should recreate in Figma):**

1) **Login**
- Logo + Title
- Username input
- Password input
- Login button
- “Forgot password?” link

2) **Admin Dashboard**
- Header nav + mode toggle
- 4 stat cards (members, attendance, payments, tea)
- Quick Actions grid (attendance, members, dish plans, payments, tea records, reports)

3) **Attendance Management (Admin)**
- Period selector
- Calendar heatmap (days)
- Pending verification panel
- Table/list for members with meal toggles + dish selection
- Save button + success confirmation

4) **My Attendance (Member)**
- Pending verification cards with per-meal verify/decline
- “Verify All” button
- History grouped by period

5) **Tea Management / My Tea**
- Similar structure to attendance: calendar overview + pending verifications

6) **Reports (Admin)**
- Period selector
- Summary cards + collection progress
- Export buttons (CSV/PDF)
- Member report table with filters/search

---

### 3b) Core Features Coverage
Core features required by the prompt are covered by implemented screens:
- Marking attendance (Admin)
- Viewing status and verifying (Member)
- Reports (Admin) including export

---

### 3c) Mapping Between Actions and System Responses
Examples grounded in the UI:
- Save attendance/tea → `TempData["SuccessMessage"]` alert appears.
- Verify/Decline → status badges change (pending → verified) and count decreases.
- Period selection → calendar + summaries update for that period.
- Report export buttons → download CSV/PDF.

---

### 3d) Complete Task Flow Demonstration (example)
**Task:** Mark attendance → save → confirmation
1) Admin opens Attendance Management.
2) Admin chooses date from calendar (and period if needed).
3) Admin reviews members and meal presence/dish selection.
4) Admin clicks Save.
5) System shows success alert confirming save.

---

### 3e) Realism and closeness to final standards
- UI uses a consistent component system (cards, tables, badges, buttons).
- Pages include descriptions and status visibility.
- Exports and approvals represent real administrative tasks.

---

## 4) Evaluation & Testing

### 4a) Evaluation Methods (recommended for HCI report)
Use a mix of methods to triangulate usability:

1) **Heuristic Evaluation**
- 2–3 evaluators review key screens using Nielsen’s heuristics.
- Output: list of issues with severity (0–4) and suggested fixes.

2) **Task-based Usability Testing (moderated or unmoderated)**
- Participants: 3–5 members + 1–2 admins.
- Tasks:
  - Admin: Mark today’s attendance for 10 members and save.
  - Admin: Approve a pending payment.
  - Admin: Generate report and export PDF.
  - Member: Verify 3 pending meals and decline one incorrect meal.
  - Member: Verify 2 tea entries.
- Metrics:
  - Task completion rate (%).
  - Time on task.
  - Error rate (misclicks, wrong action).
  - Post-task difficulty rating (1–5).

3) **Questionnaire (SUS + short custom questions)**
- SUS score out of 100.
- Add 3 custom Likert questions:
  - “I can easily find pending items.”
  - “I trust the system’s charges and records.”
  - “The terms match real mess terminology.”

---

### 4b) Reporting Findings (template)
Create a table like this after testing:

| Issue | Where | Severity | Evidence | Recommendation |
|------|-------|----------|----------|----------------|
| Users missed pending verifications | My Attendance | 3 | 2/5 users didn’t notice pending block | Make pending section first + stronger badge |

---

### 4c) Comparison of Methods (lab vs real environment)
- **Lab testing:** controlled, faster to measure time/errors, but less realistic stress.
- **Real environment:** mess office + actual daily flow (noise, time pressure), more realistic but harder to control variables.

Recommendation: do **1 lab session** to identify obvious issues, then **1 real-environment session** to validate under real constraints.

---

### 4d) Evidence of Improvement (Iteration)
To satisfy “iteration” in your CCP report, document at least one improvement cycle:

1) **Baseline**
- Run usability test + heuristic review.
- Capture screenshots of problem areas.

2) **Change implemented**
- Record the UI change (before/after screenshots).
- Note what changed and why.

3) **Re-test**
- Repeat 1–2 tasks to confirm improvement.
- Compare metrics (time, errors, satisfaction).

> Important: This document proposes the iteration process. If you need *evidence*, you must perform testing and capture screenshots/notes yourself (see “Manual work” below).

---

## 5) Manual Work You Must Do (I can’t do these automatically)

1) **Create actual wireframes and mockups (Figma/Adobe XD)**
- Make frames for: Login, Dashboard (Admin + Member), Attendance (Admin), My Attendance, Tea (Admin), My Tea, Payments (Admin), Reports.
- Export as PNG/PDF and insert into this report.

2) **Collect screenshots from the running application**
- Run the app and take screenshots for each key screen.
- Use them as “high-fidelity prototype evidence”.

3) **Run real usability testing & collect evidence**
- Recruit at least 5 participants.
- Record task time and issues; collect SUS responses.
- Summarize results in Section 4.

4) **Accessibility audit evidence**
- Use a contrast checker (e.g., WebAIM Contrast Checker) on:
  - Buttons, badges, gradient header text, table text.
- Keyboard-only pass: Tab through navigation, forms, dialogs.

---

## 6) Appendix (Optional)

### A) Suggested SUS Form (copy into Google Forms)
- I think that I would like to use this system frequently.
- I found the system unnecessarily complex.
- I thought the system was easy to use.
- I think that I would need the support of a technical person to be able to use this system.
- I found the various functions in this system were well integrated.
- I thought there was too much inconsistency in this system.
- I would imagine that most people would learn to use this system very quickly.
- I found the system very cumbersome to use.
- I felt very confident using the system.
- I needed to learn a lot of things before I could get going with this system.

---

### B) Heuristic Severity Scale
- 0 = Not a usability problem
- 1 = Cosmetic
- 2 = Minor
- 3 = Major
- 4 = Usability catastrophe
