## What does this PR do?
<!-- One or two lines. Link the ticket/task. -->

## Module(s) touched
- [ ] API (ClassicERPCoreAPI)
- [ ] FN (Finance)  - [ ] IN (Inventory)  - [ ] BL (Bought Leaf)  - [ ] Other: ____

## Checklist
- [ ] Branch name follows `<type>/<me>/<module>-<ticket>-<description>` and targets the right base
      (`feature/bugfix/chore` → `Release_Next`, `uatfix` → `Release`, `hotfix` → `Main`)
- [ ] I merged the latest base branch into my branch today
- [ ] Builds locally with no new warnings
- [ ] Tested the screen/endpoint I changed
- [ ] No `bin/`, `obj/`, `.vs/`, DLLs or `appsettings.Production.json` in this PR
- [ ] DB change? -> new file under `Database/` (I did **not** edit an already-merged run-once script)
- [ ] New config key? -> added to `appsettings.json` with a safe default **and** listed below so it can be added on the server
- [ ] Depends on an API change? -> API PR link: ____

## New config keys / server steps (if any)
<!-- e.g. "Add Smtp:Host to appsettings.Production.json on UAT and PROD" -->

## Screenshots (UI changes)
