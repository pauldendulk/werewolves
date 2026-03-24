---
name: gcp-cloud-sql
description: GCP Cloud Run + Cloud SQL (managed PostgreSQL) setup — Terraform patterns, IAM, Secret Manager, and GitHub Actions CI/CD. Use when setting up or modifying GCP infrastructure with a managed database.
---

# GCP Cloud Run + Cloud SQL — Infrastructure Patterns

## Stack
- **Backend**: Google Cloud Run (containerised)
- **Database**: Cloud SQL for PostgreSQL
- **Secrets**: Secret Manager
- **IaC**: Terraform in `infra/`
- **CI/CD**: GitHub Actions

---

## How the backend connects to Cloud SQL

Use the **Cloud SQL Auth Proxy via Unix socket** — no public IP or TCP connection needed.

1. Mount a `cloudsql` volume in the Cloud Run template pointing at the instance connection name.
2. Set the connection string host to `/cloudsql/PROJECT:REGION:INSTANCE`.
3. Disable the public IP entirely: `ip_configuration { ipv4_enabled = false }`.

---

## Terraform resource pattern

```hcl
# ── Enable required APIs ──────────────────────────────────────────────────────
resource "google_project_service" "sqladmin"        { service = "sqladmin.googleapis.com" }
resource "google_project_service" "secretmanager"   { service = "secretmanager.googleapis.com" }
resource "google_project_service" "run"             { service = "run.googleapis.com" }
resource "google_project_service" "artifactregistry" { service = "artifactregistry.googleapis.com" }
resource "google_project_service" "iam"             { service = "iam.googleapis.com" }

# ── Cloud SQL instance ────────────────────────────────────────────────────────
resource "google_sql_database_instance" "main" {
  name             = "myapp-db"
  database_version = "POSTGRES_16"
  region           = var.region
  deletion_protection = false   # flip to true in production

  settings {
    tier      = "db-f1-micro"
    edition   = "ENTERPRISE"
    disk_size = 10
    backup_configuration { enabled = true }
    ip_configuration { ipv4_enabled = false }   # no public IP needed
  }
  depends_on = [google_project_service.sqladmin]
}

resource "google_sql_database" "app" {
  name     = "myapp"
  instance = google_sql_database_instance.main.name
}

resource "google_sql_user" "app" {
  name     = "myapp"
  instance = google_sql_database_instance.main.name
  password = var.db_password   # passed as TF_VAR_db_password in CI
}

# ── Secret Manager — store connection string ──────────────────────────────────
resource "google_secret_manager_secret" "db_connection_string" {
  secret_id = "myapp-db-connection-string"
  replication { auto {} }
  depends_on = [google_project_service.secretmanager]
}

resource "google_secret_manager_secret_version" "db_connection_string" {
  secret      = google_secret_manager_secret.db_connection_string.id
  secret_data = "Host=/cloudsql/${google_sql_database_instance.main.connection_name};Database=myapp;Username=myapp;Password=${var.db_password}"
}

# ── Dedicated service account for Cloud Run ───────────────────────────────────
resource "google_service_account" "cloud_run" {
  account_id   = "myapp-api"
  display_name = "MyApp API (Cloud Run)"
  depends_on   = [google_project_service.iam]
}

resource "google_project_iam_member" "cloud_run_sql_client" {
  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${google_service_account.cloud_run.email}"
}

resource "google_secret_manager_secret_iam_member" "cloud_run_secret_access" {
  secret_id = google_secret_manager_secret.db_connection_string.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.cloud_run.email}"
}

# ── Cloud Run service ─────────────────────────────────────────────────────────
resource "google_cloud_run_v2_service" "api" {
  name     = "myapp-api"
  location = var.region
  deletion_protection = false   # flip to true in production

  template {
    service_account = google_service_account.cloud_run.email

    volumes {
      name = "cloudsql"
      cloud_sql_instance {
        instances = [google_sql_database_instance.main.connection_name]
      }
    }

    containers {
      image = "${var.region}-docker.pkg.dev/${var.project_id}/myapp/myapp-api:latest"

      volume_mounts {
        name       = "cloudsql"
        mount_path = "/cloudsql"
      }

      env {
        name = "ConnectionStrings__DefaultConnection"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.db_connection_string.secret_id
            version = "latest"
          }
        }
      }
    }
  }

  # Cloud Run must wait for all dependencies to be fully provisioned
  depends_on = [
    google_project_service.run,
    google_sql_database_instance.main,
    google_secret_manager_secret_version.db_connection_string,
    google_service_account.cloud_run,
    google_secret_manager_secret_iam_member.cloud_run_secret_access,
    google_project_iam_member.cloud_run_sql_client,
  ]
}
```

---

## IAM — minimum required roles for the Cloud Run service account

| Role | Scope | Why |
|---|---|---|
| `roles/cloudsql.client` | Project | Connect via Cloud SQL Auth Proxy |
| `roles/secretmanager.secretAccessor` | Secret resource | Read the connection string |

---

## GitHub Actions CI/CD

### Required repository secrets

| Secret | Value |
|---|---|
| `GCP_CREDENTIALS` | JSON key of a GCP service account with Terraform + Artifact Registry + Cloud Run permissions |
| `DB_PASSWORD` | Database user password |

### Terraform apply step

```yaml
- name: Terraform Apply
  working-directory: infra
  run: terraform apply -auto-approve
  env:
    TF_VAR_db_password: ${{ secrets.DB_PASSWORD }}
```

### Job ordering

```
test → infrastructure (terraform apply) → deploy-backend
test → deploy-frontend   (can run in parallel with infrastructure)
```

---

## Terraform remote state (GCS)

```hcl
backend "gcs" {
  bucket = "myproject-terraform-state"
  prefix = "myapp"
}
```

- Create the bucket **manually once** before running `terraform init`.
- The `GCP_CREDENTIALS` service account needs `roles/storage.objectAdmin` on that bucket.
- After creating the bucket: `terraform init -migrate-state` to move existing local state.

---

## Common pitfalls

- **`depends_on` ordering** — the first `terraform apply` can fail if Cloud Run tries to reference the secret or SQL instance before they're fully provisioned. Always include all dependencies in `depends_on` on the Cloud Run resource.
- **`deletion_protection = false`** — set on both `google_sql_database_instance` and `google_cloud_run_v2_service` during development so Terraform can cleanly destroy/recreate.
- **Database migrations** — run at app startup so they happen automatically on each deployment. Use `Host=/cloudsql/PROJECT:REGION:INSTANCE` as the connection host (same path as the volume mount).
