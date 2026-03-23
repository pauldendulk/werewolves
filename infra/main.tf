terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 6.0"
    }
  }

  # Remote state stored in GCS so GitHub Actions and local runs share the same state.
  # One-time setup: create the bucket first (see README), then run:
  #   terraform init -migrate-state
  backend "gcs" {
    bucket = "brutiledemo-terraform-state"
    prefix = "werewolves"
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

# Enable required APIs
resource "google_project_service" "run" {
  service            = "run.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "artifact_registry" {
  service            = "artifactregistry.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "sqladmin" {
  service            = "sqladmin.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "secretmanager" {
  service            = "secretmanager.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "iam" {
  service            = "iam.googleapis.com"
  disable_on_destroy = false
}

# Artifact Registry repository to store Docker images
resource "google_artifact_registry_repository" "werewolves" {
  repository_id = "werewolves"
  location      = var.region
  format        = "DOCKER"
  description   = "Werewolves API Docker images"

  depends_on = [google_project_service.artifact_registry]
}

# Cloud Run service for the .NET backend
resource "google_cloud_run_v2_service" "api" {
  name     = "werewolves-api"
  location = var.region

  deletion_protection = false

  template {
    # Run as a dedicated service account with minimal permissions
    service_account = google_service_account.cloud_run.email

    # Connect to Cloud SQL via Unix socket (no public IP needed)
    volumes {
      name = "cloudsql"
      cloud_sql_instance {
        instances = [google_sql_database_instance.main.connection_name]
      }
    }

    containers {
      image = "${var.region}-docker.pkg.dev/${var.project_id}/werewolves/werewolves-api:latest"

      ports {
        container_port = 8080
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      # Inject connection string from Secret Manager
      env {
        name = "ConnectionStrings__DefaultConnection"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.db_connection_string.secret_id
            version = "latest"
          }
        }
      }

      volume_mounts {
        name       = "cloudsql"
        mount_path = "/cloudsql"
      }
    }

    scaling {
      min_instance_count = 0
      max_instance_count = 1
    }
  }

  depends_on = [
    google_project_service.run,
    google_sql_database_instance.main,
    google_secret_manager_secret_version.db_connection_string,
    google_service_account.cloud_run,
    google_secret_manager_secret_iam_member.cloud_run_secret_access,
    google_project_iam_member.cloud_run_sql_client,
  ]
}

# Allow unauthenticated (public) access to the Cloud Run service
resource "google_cloud_run_v2_service_iam_member" "public" {
  project  = var.project_id
  location = var.region
  name     = google_cloud_run_v2_service.api.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}

# ── Cloud SQL ─────────────────────────────────────────────────────────────────

resource "google_sql_database_instance" "main" {
  name             = "werewolves-db"
  database_version = "POSTGRES_16"
  region           = var.region

  deletion_protection = false

  settings {
    tier      = "db-f1-micro"
    edition   = "ENTERPRISE"
    disk_size = 10

    backup_configuration {
      enabled = true
    }
  }

  depends_on = [google_project_service.sqladmin]
}

resource "google_sql_database" "werewolves" {
  name     = "werewolves"
  instance = google_sql_database_instance.main.name
}

resource "google_sql_user" "werewolves" {
  name     = "werewolves"
  instance = google_sql_database_instance.main.name
  password = var.db_password
}

# ── Secret Manager ────────────────────────────────────────────────────────────

resource "google_secret_manager_secret" "db_connection_string" {
  secret_id = "werewolves-db-connection-string"

  replication {
    auto {}
  }

  depends_on = [google_project_service.secretmanager]
}

resource "google_secret_manager_secret_version" "db_connection_string" {
  secret      = google_secret_manager_secret.db_connection_string.id
  secret_data = "Host=/cloudsql/${google_sql_database_instance.main.connection_name};Database=werewolves;Username=werewolves;Password=${var.db_password}"
}

# ── IAM — let Cloud Run read the secret and connect to Cloud SQL ──────────────

# Dedicated service account for the Cloud Run service
resource "google_service_account" "cloud_run" {
  account_id   = "werewolves-api"
  display_name = "Werewolves API (Cloud Run)"

  depends_on = [google_project_service.iam]
}

resource "google_secret_manager_secret_iam_member" "cloud_run_secret_access" {
  secret_id = google_secret_manager_secret.db_connection_string.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.cloud_run.email}"
}

resource "google_project_iam_member" "cloud_run_sql_client" {
  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${google_service_account.cloud_run.email}"
}
