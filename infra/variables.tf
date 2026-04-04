variable "project_id" {
  description = "Google Cloud project ID"
  type        = string
  default     = "brutiledemo"
}

variable "region" {
  description = "Google Cloud region"
  type        = string
  default     = "europe-west4"
}

variable "db_password" {
  description = "Password for the werewolves database user"
  type        = string
  sensitive   = true
}

variable "stripe_secret_key" {
  description = "Stripe secret API key (sk_live_... or sk_test_...)"
  type        = string
  sensitive   = true
}

variable "stripe_webhook_secret" {
  description = "Stripe webhook signing secret (whsec_...)"
  type        = string
  sensitive   = true
}

variable "stripe_price_id" {
  description = "Stripe Price ID for the tournament pass product"
  type        = string
}

variable "tournament_bypass_code" {
  description = "Cheat code to unlock tournament premium mode without payment (SKIPPY)"
  type        = string
  sensitive   = true
}
