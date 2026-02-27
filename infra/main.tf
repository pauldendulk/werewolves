terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 6.0"
    }
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
    containers {
      image = "${var.region}-docker.pkg.dev/${var.project_id}/werewolves/werewolves-api:latest"

      ports {
        container_port = 8080
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
    }

    scaling {
      min_instance_count = 0
      max_instance_count = 1
    }
  }

  depends_on = [google_project_service.run]
}

# Allow unauthenticated (public) access to the Cloud Run service
resource "google_cloud_run_v2_service_iam_member" "public" {
  project  = var.project_id
  location = var.region
  name     = google_cloud_run_v2_service.api.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}
