<#
.SYNOPSIS
    PreToolUse hook (matcher: Bash) — blocks destructive database commands.

.DESCRIPTION
    Reads the Claude Code hook payload from stdin, inspects the Bash command,
    and blocks it if it matches a destructive-SQL / destructive-EF pattern.

    Blocking uses the canonical PreToolUse mechanism: exit code 2 with an
    explanation written to stderr (Claude Code feeds stderr back to the model
    and cancels the tool call). Any other command is allowed to fall through
    to the normal permission flow (exit 0, no output).

    Hook contract: https://docs.claude.com/en/docs/claude-code/hooks
#>

$ErrorActionPreference = 'Stop'

# --- Read the hook payload from stdin -------------------------------------
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try {
    $payload = $raw | ConvertFrom-Json
} catch {
    # Malformed payload: don't get in the way of the tool call.
    exit 0
}

if ($payload.tool_name -ne 'Bash') { exit 0 }

$command = [string]$payload.tool_input.command
if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

# --- Destructive patterns --------------------------------------------------
# Each entry: a case-insensitive regex + a human-readable reason.
$rules = @(
    @{ Pattern = 'drop\s+database';                      Reason = 'DROP DATABASE' }
    @{ Pattern = 'drop\s+table';                         Reason = 'DROP TABLE' }
    @{ Pattern = 'truncate\s+table';                     Reason = 'TRUNCATE TABLE' }
    @{ Pattern = 'dotnet\s+ef\s+database\s+drop';        Reason = 'dotnet ef database drop' }
    # DELETE / UPDATE without a WHERE clause (whole-table mutation).
    @{ Pattern = 'delete\s+from\s+\w+(?!.*\bwhere\b)';   Reason = 'DELETE without a WHERE clause' }
    @{ Pattern = 'update\s+\w+\s+set\b(?!.*\bwhere\b)';  Reason = 'UPDATE without a WHERE clause' }
)

foreach ($rule in $rules) {
    if ($command -imatch $rule.Pattern) {
        [Console]::Error.WriteLine(
            "Blocked by block-destructive-sql hook: command matches '$($rule.Reason)'. " +
            'If this is intentional, run it manually outside Claude Code.')
        exit 2
    }
}

# No match — allow.
exit 0
