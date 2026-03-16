# Deployment

## Overview

| Component | Platform | Region |
|---|---|---|
| Frontend | Firebase Hosting | Global CDN |
| Backend API | Google Cloud Run | europe-west4 |
| Container images | Google Artifact Registry | europe-west4 |
| Infrastructure | Terraform | — |

---

## Full Deploy

```powershell
# From the project root
.\deploy.ps1
```

This script:

1. Builds the Docker image for the backend
2. Pushes it to Artifact Registry
3. Deploys the image to Cloud Run
4. Builds the MkDocs documentation into `frontend/werewolves-app/public/docs/`
5. Runs `ng build --configuration production`
6. Deploys the Angular output to Firebase Hosting

---

## Backend — Cloud Run

The backend is containerised and deployed to Cloud Run.

```powershell
$PROJECT_ID = "brutiledemo"
$REGION     = "europe-west4"
$IMAGE      = "$REGION-docker.pkg.dev/$PROJECT_ID/werewolves/werewolves-api:latest"

docker build -t $IMAGE .\backend\WerewolvesAPI\
docker push $IMAGE
gcloud run deploy werewolves-api --image $IMAGE --region $REGION --project $PROJECT_ID
```

The `Dockerfile` is at `backend/WerewolvesAPI/Dockerfile`. The Cloud Run service is configured with:

- **Min instances**: 0 (scales to zero when idle)
- **Max instances**: 1 (demo workload)
- **Port**: 8080 (internal container port)
- **Public access**: unauthenticated (all users)

---

## Frontend — Firebase Hosting

The Angular production build is deployed to Firebase Hosting.

```powershell
cd frontend/werewolves-app
npm run build -- --configuration production
firebase deploy --only hosting --project brutiledemo
```

### Firebase Configuration

The Firebase site is configured in `frontend/werewolves-app/firebase.json`:

- **Site**: `werewolves-app-brutiledemo`
- **Public directory**: `dist/werewolves-app/browser`
- **Rewrite**: all unmatched routes → `/index.html` (SPA routing)

The developer documentation at `/docs/` is served as static files from `public/docs/` inside the Angular build output. Static files take precedence over the catch-all rewrite, so MkDocs's directory URLs (e.g. `/docs/architecture/`) resolve correctly.

---

## Developer Docs — MkDocs

The documentation site is built separately and placed inside the Angular public folder before the Angular build runs.

```powershell
cd docs-src
mkdocs build
```

Output goes to `frontend/werewolves-app/public/docs/` as configured in `docs-src/mkdocs.yml`. Run this before `npm run build` to include the latest docs in the deployment. The `deploy.ps1` script does this automatically.

To preview docs locally:

```powershell
cd docs-src
mkdocs serve
```

This starts a live-reload server at http://127.0.0.1:8000 (independent of the Angular app).

---

## Infrastructure — Terraform

Google Cloud infrastructure is defined in `infra/`:

```powershell
cd infra
terraform init
terraform plan
terraform apply
```

### Resources Managed

| Resource | Description |
|---|---|
| `google_artifact_registry_repository` | Docker image registry |
| `google_cloud_run_v2_service` | Backend API service |
| `google_cloud_run_v2_service_iam_member` | Public (unauthenticated) access policy |

### Variables

Defined in `infra/variables.tf`:

- `project_id` — GCP project ID
- `region` — deployment region (default: `europe-west4`)

State is stored locally in `infra/terraform.tfstate`.
