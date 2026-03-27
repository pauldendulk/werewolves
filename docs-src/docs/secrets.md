# Secret Management

### `GCP_CREDENTIALS`

- **What:** GCP service account key (JSON)
- **Where:** GitHub → Settings → Secrets and variables → Actions
- **Used by:** Every job in the deploy workflow to authenticate to GCP
- **Allows:** Pushing Docker images to Artifact Registry, deploying to Cloud Run, deploying to Firebase Hosting, running Terraform

### `DB_PASSWORD`

- **What:** Password for the `werewolves` PostgreSQL database user
- **Where:** GitHub → Settings → Secrets and variables → Actions
- **Used by:** The `infrastructure` job — passed to Terraform as `TF_VAR_db_password`
- **Allows:** Terraform to create the Cloud SQL user and write the connection string to GCP Secret Manager

### `werewolves-db-connection-string`

- **What:** Full PostgreSQL connection string (host, database, username, password)
- **Where:** GCP Secret Manager, project `brutiledemo`
- **Written by:** Terraform at deploy time, using `DB_PASSWORD`
- **Used by:** Cloud Run — mounted as the `ConnectionStrings__DefaultConnection` environment variable at container startup
- **Source:** [`infra/main.tf`](../../infra/main.tf) — `google_secret_manager_secret.db_connection_string`

### `AZURE_TTS_KEY` / `AZURE_TTS_REGION`

- **What:** Azure Cognitive Services credentials for text-to-speech
- **Where:** `audio-scripts/GenerateAudio/.env` on the developer's machine (git-ignored)
- **Used by:** The one-off audio generation script — not part of CI/CD or the running app
- **Template:** [`audio-scripts/GenerateAudio/.env.example`](../../audio-scripts/GenerateAudio/.env.example)

### Local database password

- **What:** Hardcoded password (`werewolves`) in `docker-compose.yml`
- **Not a real secret** — local-only development database, no external access

---

## Adding a New Secret

- **Local-only** (developer tool): add to a `.env` file, add `.env` to `.gitignore`, document here.
- **CI/CD only** (GitHub Actions): add as a GitHub repository secret, reference as `${{ secrets.MY_SECRET }}`.
- **Runtime** (needed by the backend): provision in GCP Secret Manager via `infra/main.tf`, grant the Cloud Run service account `secretAccessor`, and mount as an environment variable.

