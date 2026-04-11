<#
.SYNOPSIS
  Push to remote and watch the CI run. Shows a Windows toast notification on completion.
.DESCRIPTION
  Wraps `git push` and then uses `gh run watch` to monitor the triggered workflow.
  When the run finishes, a Windows 10/11 toast notification pops up with the result.
#>
param(
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$GitPushArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Push ─────────────────────────────────────────────────────────────────────
Write-Host "Pushing..." -ForegroundColor Cyan
git push @GitPushArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "git push failed" -ForegroundColor Red
    exit 1
}

# ── Find the run that was just triggered ─────────────────────────────────────
Write-Host "Waiting for CI run to appear..." -ForegroundColor Cyan
Start-Sleep -Seconds 3

$run = gh run list --limit 1 --json databaseId,status,name,headBranch --jq '.[0]' | ConvertFrom-Json
if (-not $run) {
    Write-Host "Could not find a workflow run. Check GitHub manually." -ForegroundColor Yellow
    exit 0
}

$runId = $run.databaseId
$runName = $run.name
Write-Host "Watching run #$runId ($runName) on branch '$($run.headBranch)'..." -ForegroundColor Cyan

# ── Watch ────────────────────────────────────────────────────────────────────
gh run watch $runId --exit-status 2>&1 | ForEach-Object { Write-Host $_ }
$success = $LASTEXITCODE -eq 0

# ── Toast notification ───────────────────────────────────────────────────────
function Show-Toast([string]$Title, [string]$Message) {
    $xml = @"
<toast>
  <visual>
    <binding template="ToastGeneric">
      <text>$Title</text>
      <text>$Message</text>
    </binding>
  </visual>
</toast>
"@
    [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
    [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType = WindowsRuntime] | Out-Null
    $doc = [Windows.Data.Xml.Dom.XmlDocument]::new()
    $doc.LoadXml($xml)
    $notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("VS Code – Werewolves CI")
    $toast = [Windows.UI.Notifications.ToastNotification]::new($doc)
    $notifier.Show($toast)
}

if ($success) {
    Write-Host "`nCI passed!" -ForegroundColor Green
    Show-Toast "CI Passed ✅" "$runName completed successfully."
} else {
    Write-Host "`nCI FAILED!" -ForegroundColor Red
    Show-Toast "CI FAILED ❌" "$runName failed! Check the GitHub Actions tab."
    exit 1
}
