#!/usr/bin/env pwsh
#
# bootstrap.ps1 — One-time setup for a new environment.
#
# Run this ONCE before using Terraform or the GitHub Actions pipeline.
# After this script completes, add the printed service account key as a
# GitHub Actions secret named GCP_CREDENTIALS.
#
# Usage:
#   cd infra
#   .\bootstrap.ps1 -ProjectId brutiledemo -Region europe-west4
#
# Requirements:
#   - gcloud CLI installed and authenticated as a project Owner
#   - Terraform installed

param(
    [Parameter(Mandatory)]
    [string]$ProjectId,

    [string]$Region = "europe-west4",

    [string]$StateBucket = "$ProjectId-terraform-state",

    [string]$ServiceAccountName = "github-actions"
)

$ErrorActionPreference = "Stop"
$SA = "$ServiceAccountName@$ProjectId.iam.gserviceaccount.com"

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Bootstrap: $ProjectId" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# ── 1. GCS bucket for Terraform state ────────────────────────────────────────

Write-Host "[1/3] Creating Terraform state bucket: gs://$StateBucket" -ForegroundColor Yellow

$existing = gcloud storage buckets list --project=$ProjectId --filter="name=$StateBucket" --format="value(name)" 2>$null
if ($existing) {
    Write-Host "      Bucket already exists, skipping." -ForegroundColor DarkGray
} else {
    gcloud storage buckets create "gs://$StateBucket" `
        --project=$ProjectId `
        --location=$Region `
        --uniform-bucket-level-access
    Write-Host "      Bucket created." -ForegroundColor Green
}

# ── 2. GitHub Actions service account ────────────────────────────────────────

Write-Host ""
Write-Host "[2/3] Creating service account: $SA" -ForegroundColor Yellow

$existingSA = gcloud iam service-accounts list --project=$ProjectId --filter="email=$SA" --format="value(email)" 2>$null
if ($existingSA) {
    Write-Host "      Service account already exists, skipping creation." -ForegroundColor DarkGray
} else {
    gcloud iam service-accounts create $ServiceAccountName `
        --project=$ProjectId `
        --display-name="GitHub Actions"
    Write-Host "      Service account created." -ForegroundColor Green
}

Write-Host "      Granting IAM roles..." -ForegroundColor Yellow

$roles = @(
    "roles/run.admin",                       # Deploy to Cloud Run
    "roles/artifactregistry.writer",         # Push Docker images
    "roles/cloudsql.admin",                  # Manage Cloud SQL instances
    "roles/secretmanager.admin",             # Manage secrets
    "roles/resourcemanager.projectIamAdmin", # Grant project-level IAM bindings
    "roles/iam.serviceAccountAdmin",         # Create/manage service accounts
    "roles/iam.serviceAccountUser",          # Attach service accounts to Cloud Run
    "roles/serviceusage.serviceUsageAdmin",  # Enable GCP APIs
    "roles/storage.admin",                   # Read/write Terraform state bucket
    "roles/firebasehosting.admin"            # Deploy to Firebase Hosting
)

foreach ($role in $roles) {
    gcloud projects add-iam-policy-binding $ProjectId `
        --member="serviceAccount:$SA" `
        --role="$role" `
        --quiet | Out-Null
    Write-Host "      + $role" -ForegroundColor DarkGray
}

Write-Host "      Roles granted." -ForegroundColor Green

# ── 3. Download service account key ─────────────────────────────────────────

Write-Host ""
Write-Host "[3/3] Downloading service account key..." -ForegroundColor Yellow

$keyFile = "github-actions-key.json"
gcloud iam service-accounts keys create $keyFile --iam-account=$SA
Write-Host "      Key saved to: $keyFile" -ForegroundColor Green

# ── Done ─────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host "  Bootstrap complete!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Yellow
Write-Host "  1. Copy the contents of '$keyFile' into a GitHub Actions secret" -ForegroundColor White
Write-Host "     named 'GCP_CREDENTIALS'." -ForegroundColor White
Write-Host "  2. DELETE the key file from your machine:" -ForegroundColor White
Write-Host "       Remove-Item $keyFile" -ForegroundColor Cyan
Write-Host "  3. Create infra/terraform.tfvars with your DB password:" -ForegroundColor White
Write-Host "       db_password = `"your-password`"" -ForegroundColor Cyan
Write-Host "  4. Run: terraform init -migrate-state" -ForegroundColor White
Write-Host "     (only needed if switching from local to GCS state)" -ForegroundColor DarkGray
Write-Host ""
