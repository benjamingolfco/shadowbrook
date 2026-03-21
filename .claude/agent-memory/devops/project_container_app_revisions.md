---
name: Container App revision mode and deploy pattern
description: activeRevisionsMode Single prevents old revisions reactivating on deploy; revision-suffix added to update command for diagnosability
type: project
---

`shadowbrook-app-dev` runs with `activeRevisionsMode: Single`. The root cause of old revisions reactivating during deploys was that the app was previously in Multiple mode with manually deactivated revisions — `az containerapp update` does not reactivate dormant revisions in Single mode.

**Why:** Competing PR deploys to the shared Container App (last-deploy-wins setup) were triggering SQL Error 2714 crash loops when old revisions with stale migration state came back online and tried to re-create tables.

**How to apply:**
- `activeRevisionsMode: 'Single'` is now in `infra/bicep/modules/container-app.bicep` — must stay there so it can never be lost via `az` CLI drift.
- `deploy-api.yml` adds `--revision-suffix` to `az containerapp update` — the suffix is derived from `image-tag` (lowercased, non-alphanumeric chars replaced with `-`, truncated to 20 chars). This gives each revision a readable name tied to the PR/commit for easier diagnosis.
- Azure Container Apps revision suffix must match `[a-z0-9-]{1,20}` — the `sed`/`tr` pipeline in the workflow enforces this.
