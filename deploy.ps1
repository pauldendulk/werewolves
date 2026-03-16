#!/usr/bin/env pwsh

$ErrorActionPreference = 'Stop'

$PROJECT_ID = "brutiledemo"
$REGION     = "europe-west4"
$IMAGE      = "$REGION-docker.pkg.dev/$PROJECT_ID/werewolves/werewolves-api:latest"
$SERVICE    = "werewolves-api"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Werewolves - Deploy Script" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# ── Backend ───────────────────────────────────────────────────────────────────

Write-Host "[Backend] Building Docker image..." -ForegroundColor Yellow
docker build -t $IMAGE .\backend\WerewolvesAPI\
if ($LASTEXITCODE -ne 0) { Write-Host "[Backend] Build failed." -ForegroundColor Red; exit 1 }

Write-Host "[Backend] Pushing image to Artifact Registry..." -ForegroundColor Yellow
docker push $IMAGE
if ($LASTEXITCODE -ne 0) { Write-Host "[Backend] Push failed." -ForegroundColor Red; exit 1 }

Write-Host "[Backend] Deploying to Cloud Run..." -ForegroundColor Yellow
gcloud run deploy $SERVICE --image $IMAGE --region $REGION --project $PROJECT_ID
if ($LASTEXITCODE -ne 0) { Write-Host "[Backend] Cloud Run deploy failed." -ForegroundColor Red; exit 1 }

Write-Host "[Backend] Deployed successfully." -ForegroundColor Green
Write-Host ""

# ── Docs ──────────────────────────────────────────────────────────────────────

Write-Host "[Docs] Building MkDocs documentation..." -ForegroundColor Yellow
Push-Location docs-src
mkdocs build
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "[Docs] MkDocs build failed." -ForegroundColor Red; exit 1 }
Pop-Location

Write-Host "[Docs] Documentation built successfully." -ForegroundColor Green
Write-Host ""

# ── Frontend ──────────────────────────────────────────────────────────────────

Write-Host "[Frontend] Building Angular app..." -ForegroundColor Yellow
Push-Location frontend\werewolves-app
npm run build -- --configuration production
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "[Frontend] Build failed." -ForegroundColor Red; exit 1 }

Write-Host "[Frontend] Deploying to Firebase Hosting..." -ForegroundColor Yellow
firebase deploy --only hosting --project $PROJECT_ID
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "[Frontend] Firebase deploy failed." -ForegroundColor Red; exit 1 }
Pop-Location

Write-Host "[Frontend] Deployed successfully." -ForegroundColor Green
Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host "  Deploy complete!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
