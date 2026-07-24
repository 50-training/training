<#
.SYNOPSIS
    PostToolUse hook (matcher: Edit|Write) — appends an audit line per file edit.

.DESCRIPTION
    Reads the Claude Code hook payload from stdin and appends a timestamped
    entry to `.claude/logs/edits.log` recording which file was written or
    edited. Never blocks: always exits 0, even on error.

    Hook contract: https://docs.claude.com/en/docs/claude-code/hooks
#>

$ErrorActionPreference = 'SilentlyContinue'

try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

    $payload = $raw | ConvertFrom-Json

    $tool     = [string]$payload.tool_name
    $filePath = [string]$payload.tool_input.file_path
    if ([string]::IsNullOrWhiteSpace($filePath)) { exit 0 }

    # Resolve log location relative to the repo root (two levels up from this script).
    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $logDir   = Join-Path $repoRoot '.claude/logs'
    $logFile  = Join-Path $logDir 'edits.log'

    if (-not (Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }

    $timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    $line      = "$timestamp`t$tool`t$filePath"

    Add-Content -Path $logFile -Value $line -Encoding utf8
} catch {
    # Logging must never break the workflow.
}

exit 0
