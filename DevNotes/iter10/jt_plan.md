# Iteration 10 - The Rise of Jumbotron: fix plan (planner)

Status: IN PROGRESS (skeleton written first; sections filled as they are decided)

Inputs: DN/iter10/jt_audit.md, DN/iter10/jt_materials.md, DN/iter10/jt_lighting.md,
DN/research/rr_jumbotron_look.md, and the code they reference (read only).

## 0. TL;DR

## 1. Root causes (merged from the four investigations)

## 2. Work split (A hull / B light), file ownership

## 3. A "hull" changes (RrJumbotronHull.cs + new files in Games/Quests/Jumbotron/)

### 3.1 Material mapping table

### 3.2 Not drawn / drawn as haze / special

### 3.3 Code changes by function

## 4. B "light" changes (RrEnvironmentTable Jumbotron_* rows, RrJumbotronArea.cs, haze, bloom)

### 4.1 Environment bounds / SetArea

### 4.2 Per-scene env numbers

### 4.3 Kit options / lights

### 4.4 Emission / bloom / post

### 4.5 Haze (area mist, portals, light shafts)

## 5. Invariants (must stay true)

## 6. Acceptance checks and harness lock order

## 7. Risks, uncertainties, fallbacks

## 8. Log
