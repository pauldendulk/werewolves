output "api_url" {
  description = "URL of the deployed Cloud Run API"
  value       = google_cloud_run_v2_service.api.uri
}

output "docker_image" {
  description = "Docker image path to push to"
  value       = "${var.region}-docker.pkg.dev/${var.project_id}/werewolves/werewolves-api:latest"
}
