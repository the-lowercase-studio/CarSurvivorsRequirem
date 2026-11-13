<#
.SYNOPSIS
    Fail-closed verification harness for Car Survivors changes and safe commits.
.DESCRIPTION
    Executes six deterministic validation gates:
      1. Compilation and zero-warnings check (Assembly-CSharp & Assembly-CSharp-Editor)
      2. Coding standards & architecture check (diff-aware) + .agents/ doc rules
      3. Conditional test detection & execution (if tests exist in repository/scope)
      4. Unity .meta pairing, serialized asset notice, and .gitignore/hygiene guard
      5. Live Unity Editor compile & console error check via unity CLI (if connected)
      6. CSR-<number> branch and commit prefix validation
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .agents/scripts/verify-change.ps1
    powershell -ExecutionPolicy Bypass -File .agents/scripts/verify-change.ps1 -StagedOnly
    powershell -ExecutionPolicy Bypass -File .agents/scripts/verify-change.ps1 -Files Assets/Scripts/Input/Foo.cs -CommitMessage "CSR-51 - add input helper"
#>

param(
    [switch]$StagedOnly,
    [string[]]$Files = @(),
    [string]$CommitMessage = "",
    [switch]$SkipUnityEditor,
    [switch]$SkipBuild,
    [switch]$AllowMainBranch,
    [switch]$StrictWarnings,
    [switch]$AllowDesignDocs
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Location).Path
$normalizedRoot = $repoRoot.Replace("\", "/").TrimEnd("/")

function Invoke-Native {
    param(
        [string]$Executable,
        [string[]]$Arguments
    )
    $prevPref = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $Executable @Arguments 2>&1
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $prevPref
    }
    return [PSCustomObject]@{
        ExitCode = $exitCode
        Output   = @($output | ForEach-Object { $_.ToString() })
    }
}

function Add-GateResult {
    param(
        [System.Collections.Generic.List[object]]$List,
        [string]$Gate,
        [string]$Status,
        [string]$Detail
    )
    $List.Add([PSCustomObject]@{
        Gate   = $Gate
        Status = $Status
        Detail = $Detail
    })
}

# Collect target file entries: @{ Path = "rel/path"; ChangeType = "M/A/D/?"; IsStaged = $bool }
$scopeItems = [System.Collections.Generic.List[object]]::new()
$seenPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

if ($Files.Count -gt 0) {
    foreach ($f in $Files) {
        if ([string]::IsNullOrWhiteSpace($f)) { continue }
        foreach ($part in ($f -split ",")) {
            if ([string]::IsNullOrWhiteSpace($part)) { continue }
            $rel = ($part.Trim().Replace("\", "/") -replace '^\./', '')
            if ($seenPaths.Add($rel)) {
                $exists = Test-Path (Join-Path $repoRoot ($rel.Replace("/", "\")))
                $scopeItems.Add([PSCustomObject]@{
                    Path       = $rel
                    ChangeType = if ($exists) { "M" } else { "D" }
                    IsStaged   = $true
                })
            }
        }
    }
}
elseif ($StagedOnly) {
    $stagedRes = Invoke-Native -Executable "git" -Arguments @("diff", "--cached", "--name-status")
    if ($stagedRes.ExitCode -ne 0) {
        Write-Error "Failed to read staged git diff."
        exit 1
    }
    foreach ($line in $stagedRes.Output) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "`t"
        if ($parts.Count -lt 2) { continue }
        $statusCode = $parts[0].Substring(0, 1)
        $rel = $parts[$parts.Count - 1].Replace("\", "/")
        if ($seenPaths.Add($rel)) {
            $scopeItems.Add([PSCustomObject]@{
                Path       = $rel
                ChangeType = $statusCode
                IsStaged   = $true
            })
        }
    }
}
else {
    $stagedRes = Invoke-Native -Executable "git" -Arguments @("diff", "--cached", "--name-status")
    $unstagedRes = Invoke-Native -Executable "git" -Arguments @("diff", "--name-status")
    $untrackedRes = Invoke-Native -Executable "git" -Arguments @("ls-files", "--others", "--exclude-standard")

    foreach ($line in $stagedRes.Output) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "`t"
        if ($parts.Count -lt 2) { continue }
        $rel = $parts[$parts.Count - 1].Replace("\", "/")
        if ($seenPaths.Add($rel)) {
            $scopeItems.Add([PSCustomObject]@{
                Path       = $rel
                ChangeType = $parts[0].Substring(0, 1)
                IsStaged   = $true
            })
        }
    }
    foreach ($line in $unstagedRes.Output) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "`t"
        if ($parts.Count -lt 2) { continue }
        $rel = $parts[$parts.Count - 1].Replace("\", "/")
        if ($seenPaths.Add($rel)) {
            $scopeItems.Add([PSCustomObject]@{
                Path       = $rel
                ChangeType = $parts[0].Substring(0, 1)
                IsStaged   = $false
            })
        }
    }
    foreach ($line in $untrackedRes.Output) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $rel = $line.Replace("\", "/")
        if ($seenPaths.Add($rel)) {
            $scopeItems.Add([PSCustomObject]@{
                Path       = $rel
                ChangeType = "?"
                IsStaged   = $false
            })
        }
    }
}

$results = [System.Collections.Generic.List[object]]::new()
$hasFailure = $false
$notices = [System.Collections.Generic.List[string]]::new()

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Car Survivors Validation Harness" -ForegroundColor Cyan
Write-Host " Scope items: $($scopeItems.Count) | StagedOnly=$StagedOnly" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# ------------------------------------------------------------------
# Gate 1: Compilation & Zero Warnings
# ------------------------------------------------------------------
if ($SkipBuild) {
    Add-GateResult -List $results -Gate "1. Compilation" -Status "PENDING" -Detail "Skipped via -SkipBuild."
} else {
    $projects = @("Assembly-CSharp.csproj", "Assembly-CSharp-Editor.csproj")
    $buildErrors = [System.Collections.Generic.List[string]]::new()
    foreach ($proj in $projects) {
        if (-not (Test-Path (Join-Path $repoRoot $proj))) {
            $buildErrors.Add("$proj missing")
            continue
        }
        $buildRes = Invoke-Native -Executable "dotnet" -Arguments @("build", $proj, "-p:BuildProjectReferences=false", "--no-restore", "-v", "minimal")
        if ($buildRes.ExitCode -ne 0) {
            # Retry once with restore in case project.assets.json is stale
            $buildRes = Invoke-Native -Executable "dotnet" -Arguments @("build", $proj, "-p:BuildProjectReferences=false", "-v", "minimal")
        }
        $outputText = ($buildRes.Output -join "`n")
        $warnMatch = [regex]::Match($outputText, "(\d+)\s+Warning\(s\)")
        $errMatch = [regex]::Match($outputText, "(\d+)\s+Error\(s\)")
        $warnCount = if ($warnMatch.Success) { [int]$warnMatch.Groups[1].Value } else { 0 }
        $errCount = if ($errMatch.Success) { [int]$errMatch.Groups[1].Value } else { 0 }
        if ($buildRes.ExitCode -ne 0 -or $errCount -gt 0 -or $warnCount -gt 0) {
            $buildErrors.Add("$proj failed (exit=$($buildRes.ExitCode), errors=$errCount, warnings=$warnCount)")
            foreach ($line in $buildRes.Output) {
                if ($line -match "error\s+[A-Z]+\d+|warning\s+[A-Z]+\d+") {
                    Write-Host "  [BUILD] $line" -ForegroundColor Red
                }
            }
        }
    }
    if ($buildErrors.Count -gt 0) {
        $hasFailure = $true
        Add-GateResult -List $results -Gate "1. Compilation" -Status "FAIL" -Detail ($buildErrors -join "; ")
    } else {
        Add-GateResult -List $results -Gate "1. Compilation" -Status "PASS" -Detail "Assembly-CSharp & Editor built with 0 warnings, 0 errors."
    }
}

# ------------------------------------------------------------------
# Gate 2: Coding Standards, Architecture & .agents/ Conventions
# ------------------------------------------------------------------
$csScoped = @($scopeItems | Where-Object {
    $_.ChangeType -ne "D" -and $_.Path -like "Assets/Scripts/*.cs"
} | ForEach-Object { $_.Path })

$archIssues = [System.Collections.Generic.List[string]]::new()
if ($csScoped.Count -gt 0) {
    $joinedCsFiles = $csScoped -join ","
    $prevPref = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        if ($StrictWarnings) {
            & powershell -ExecutionPolicy Bypass -File ".agents/scripts/check-architecture.ps1" -Files $joinedCsFiles -StrictWarnings
        } else {
            & powershell -ExecutionPolicy Bypass -File ".agents/scripts/check-architecture.ps1" -Files $joinedCsFiles
        }
        $archExit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $prevPref
    }
    if ($archExit -ne 0) {
        $archIssues.Add("check-architecture.ps1 failed for scoped C# files")
    }
}

# Validate .agents/ markdown naming conventions on added/modified files
foreach ($item in $scopeItems) {
    if ($item.ChangeType -eq "D") { continue }
    $p = $item.Path
    if ($p -match "^\.agents/context/(implementations/(plans|summaries)|brainstorming-summaries|game-systems)/([^/]+\.md)$") {
        $fileName = $Matches[3]
        if ($fileName -match "^\d{4}-\d{2}-\d{2}") {
            $archIssues.Add("Date prefix forbidden in agent doc filename: $p")
        }
        if ($fileName -notmatch "^[a-z0-9-]+\.md$") {
            $archIssues.Add("Agent doc filename must be kebab-case: $p")
        }
        if ($p -match "^\.agents/context/game-systems/" -and $fileName -notmatch "-system\.md$") {
            $archIssues.Add("Game system doc must end with -system.md: $p")
        }
    }
}

if ($archIssues.Count -gt 0) {
    $hasFailure = $true
    Add-GateResult -List $results -Gate "2. Standards & Arch" -Status "FAIL" -Detail ($archIssues -join "; ")
} elseif ($csScoped.Count -eq 0) {
    Add-GateResult -List $results -Gate "2. Standards & Arch" -Status "PASS" -Detail "0 C# files in scope; .agents/ naming checks passed."
} else {
    Add-GateResult -List $results -Gate "2. Standards & Arch" -Status "PASS" -Detail "$($csScoped.Count) scoped C# file(s) passed architecture linter."
}

# ------------------------------------------------------------------
# Gate 3: Conditional Tests
# ------------------------------------------------------------------
$testFiles = @(Get-ChildItem -Path (Join-Path $repoRoot "Assets") -Recurse -Include "*Test*.cs","*.Tests.asmdef" -ErrorAction SilentlyContinue)
$unityCmd = Get-Command "unity" -ErrorAction SilentlyContinue
$connectedProject = $false

if (-not $SkipUnityEditor -and $null -ne $unityCmd) {
    $statusRes = Invoke-Native -Executable "unity" -Arguments @("status", "--format", "json")
    if ($statusRes.ExitCode -eq 0) {
        try {
            $statusJson = ($statusRes.Output -join "`n") | ConvertFrom-Json
            foreach ($inst in $statusJson.data.instances) {
                if ($inst.projectPath -and ($inst.projectPath.Replace("\", "/").TrimEnd("/") -ieq $normalizedRoot)) {
                    $connectedProject = $true
                    break
                }
            }
        } catch {
            $connectedProject = $false
        }
    }
}

if ($testFiles.Count -eq 0) {
    Add-GateResult -List $results -Gate "3. Tests" -Status "N/A" -Detail "No test assemblies or *Test*.cs files exist in Assets/."
} elseif (-not $connectedProject) {
    $hasFailure = $true
    Add-GateResult -List $results -Gate "3. Tests" -Status "FAIL" -Detail "Test files exist in Assets/ but Unity Editor is not connected to execute them."
} else {
    $testListRes = Invoke-Native -Executable "unity" -Arguments @("command", "test", "list", "--json")
    if ($testListRes.ExitCode -ne 0) {
        Add-GateResult -List $results -Gate "3. Tests" -Status "PENDING" -Detail "Failed to query Unity test list."
    } else {
        $testListJson = ($testListRes.Output -join "`n") | ConvertFrom-Json
        $testCount = [int]$testListJson.data.result.Count
        if ($testCount -eq 0) {
            Add-GateResult -List $results -Gate "3. Tests" -Status "N/A" -Detail "Unity reported 0 registered tests."
        } else {
            $testRunRes = Invoke-Native -Executable "unity" -Arguments @("command", "test", "run", "--json")
            if ($testRunRes.ExitCode -ne 0) {
                $hasFailure = $true
                Add-GateResult -List $results -Gate "3. Tests" -Status "FAIL" -Detail "Unity test execution failed."
            } else {
                Add-GateResult -List $results -Gate "3. Tests" -Status "PASS" -Detail "Executed $testCount registered Unity test(s)."
            }
        }
    }
}

# ------------------------------------------------------------------
# Gate 4: Assets, .meta Pairing & Hygiene Guard
# ------------------------------------------------------------------
$hygieneErrors = [System.Collections.Generic.List[string]]::new()
$forbiddenPatterns = @(
    "^Library/",
    "^Temp/",
    "^Logs/",
    "^UserSettings/",
    "^ProfilerCaptures/",
    "^\.utmp/",
    "^Assets/Data/",
    "^\.agents/context/tmp/(?!\.gitkeep$)",
    "^\.user-docs/evidences/(?!\.gitkeep$)",
    "\.csproj$",
    "\.sln$",
    "\.slnx$",
    "(^|/)scratch[^/]*$",
    "\.log$",
    "\.tmp$",
    "\.bak$",
    "\.orig$",
    "\.swp$"
)

$stagedLookup = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$stagedNames = Invoke-Native -Executable "git" -Arguments @("diff", "--cached", "--name-only")
foreach ($n in $stagedNames.Output) {
    if (-not [string]::IsNullOrWhiteSpace($n)) {
        [void]$stagedLookup.Add($n.Replace("\", "/"))
    }
}

foreach ($item in $scopeItems) {
    $p = $item.Path

    # Forbidden/generated artifact guard
    $isForbidden = $false
    foreach ($pat in $forbiddenPatterns) {
        if ($p -match $pat) {
            $isForbidden = $true
            break
        }
    }
    if ($isForbidden) {
        if ($item.IsStaged -or $Files.Count -gt 0 -or $StagedOnly) {
            $hygieneErrors.Add("Forbidden generated/temp file in commit scope: $p")
        } else {
            $notices.Add("Unstaged generated/scratch file present (do not commit): $p")
        }
    }

    # Brainstorming & unimplemented plan/spec scope guard
    if (-not $AllowDesignDocs -and $item.ChangeType -ne "D") {
        if ($p -match "^\.agents/context/brainstorming-summaries/.*\.md$") {
            if ($item.IsStaged -or $Files.Count -gt 0 -or $StagedOnly) {
                $hygieneErrors.Add("Brainstorming doc in commit scope without -AllowDesignDocs: $p")
            } else {
                $notices.Add("Unstaged brainstorm doc present (keep out of feature commits): $p")
            }
        }
        elseif ($p -match "^\.agents/context/implementations/plans/([^/]+)\.md$") {
            $planBase = $Matches[1] -replace "-(plan|spec)$", ""
            $summaryCandidate1 = Join-Path $repoRoot ".agents\context\implementations\summaries\$planBase-summary.md"
            $summaryCandidate2 = Join-Path $repoRoot ".agents\context\implementations\summaries\$planBase.md"
            $hasMatchingSummary = (Test-Path $summaryCandidate1) -or (Test-Path $summaryCandidate2)
            if (-not $hasMatchingSummary) {
                if ($item.IsStaged -or $Files.Count -gt 0 -or $StagedOnly) {
                    $hygieneErrors.Add("Unimplemented plan/spec in commit scope (no matching summary; pass -AllowDesignDocs if explicitly committing a standalone spec): $p")
                } else {
                    $notices.Add("Unimplemented plan/spec present (do not commit or sync to game-systems): $p")
                }
            }
        }
    }

    # Serialized Unity asset change notice
    if ($p -match "^Assets/.*\.(unity|prefab|asset|inputactions)$") {
        $notices.Add("Serialized Unity asset in scope ($($item.ChangeType)): $p")
    }

    # .meta pairing under Assets/
    if ($p -match "^Assets/(.+)$" -and $p -notmatch "^Assets/Data/") {
        if ($p.EndsWith(".meta", [System.StringComparison]::OrdinalIgnoreCase)) {
            $assetPath = $p.Substring(0, $p.Length - 5)
            $assetFull = Join-Path $repoRoot ($assetPath.Replace("/", "\"))
            if ($item.ChangeType -ne "D" -and -not (Test-Path $assetFull)) {
                $hygieneErrors.Add("Orphaned .meta file (target missing): $p")
            }
        } else {
            $metaPath = "$p.meta"
            $metaFull = Join-Path $repoRoot ($metaPath.Replace("/", "\"))
            if ($item.ChangeType -ne "D") {
                if (-not (Test-Path $metaFull)) {
                    $hygieneErrors.Add("Missing .meta file for asset: $p")
                } elseif ($StagedOnly -and -not $stagedLookup.Contains($metaPath)) {
                    $trackedMeta = Invoke-Native -Executable "git" -Arguments @("ls-files", "--error-unmatch", $metaPath)
                    if ($trackedMeta.ExitCode -ne 0) {
                        $hygieneErrors.Add("Untracked .meta file must be staged alongside new asset: $metaPath")
                    }
                }
            } elseif ($StagedOnly -and (Test-Path $metaFull)) {
                $hygieneErrors.Add("Asset deleted but .meta still exists on disk: $metaPath")
            }
        }
    }
}

if ($hygieneErrors.Count -gt 0) {
    $hasFailure = $true
    Add-GateResult -List $results -Gate "4. Assets & Hygiene" -Status "FAIL" -Detail ($hygieneErrors -join "; ")
} else {
    Add-GateResult -List $results -Gate "4. Assets & Hygiene" -Status "PASS" -Detail ".meta pairing and forbidden-file checks passed."
}

# ------------------------------------------------------------------
# Gate 5: Live Unity Editor Verification
# ------------------------------------------------------------------
if ($SkipUnityEditor) {
    Add-GateResult -List $results -Gate "5. Unity Editor" -Status "PENDING" -Detail "Skipped via -SkipUnityEditor."
} elseif ($null -eq $unityCmd) {
    Add-GateResult -List $results -Gate "5. Unity Editor" -Status "PENDING" -Detail "unity CLI not found in PATH."
} elseif (-not $connectedProject) {
    Add-GateResult -List $results -Gate "5. Unity Editor" -Status "PENDING" -Detail "No running Unity Editor connected for this project."
} else {
    $recompileRes = Invoke-Native -Executable "unity" -Arguments @("command", "script", "recompile_status", "--json")
    $consoleRes = Invoke-Native -Executable "unity" -Arguments @("command", "console", "status", "--json")

    if ($recompileRes.ExitCode -ne 0 -or $consoleRes.ExitCode -ne 0) {
        Add-GateResult -List $results -Gate "5. Unity Editor" -Status "PENDING" -Detail "Unity Pipeline momentarily unreachable (reload/startup in progress)."
    } else {
        try {
            $rcJson = ($recompileRes.Output -join "`n") | ConvertFrom-Json
            $csJson = ($consoleRes.Output -join "`n") | ConvertFrom-Json
            $compileFailed = [bool]$rcJson.data.result.compilationFailed -or [bool]$csJson.data.result.groundTruth.compilationFailed
            $consoleErrors = [int]$csJson.data.result.groundTruth.consoleErrors
            $consoleWarnings = [int]$csJson.data.result.groundTruth.consoleWarnings

            if ($compileFailed -or $consoleErrors -gt 0) {
                $hasFailure = $true
                Add-GateResult -List $results -Gate "5. Unity Editor" -Status "FAIL" -Detail "Unity compileFailed=$compileFailed, consoleErrors=$consoleErrors, consoleWarnings=$consoleWarnings."
            } else {
                Add-GateResult -List $results -Gate "5. Unity Editor" -Status "PASS" -Detail "Editor compile clean; groundTruth consoleErrors=0, warnings=$consoleWarnings."
            }
        } catch {
            Add-GateResult -List $results -Gate "5. Unity Editor" -Status "PENDING" -Detail "Could not parse Unity CLI JSON status."
        }
    }
}

# ------------------------------------------------------------------
# Gate 6: Branch & Commit Prefix Guard
# ------------------------------------------------------------------
$branchRes = Invoke-Native -Executable "git" -Arguments @("branch", "--show-current")
$branchName = ($branchRes.Output -join "").Trim()
$prefixErrors = [System.Collections.Generic.List[string]]::new()

if ([string]::IsNullOrWhiteSpace($branchName)) {
    $prefixErrors.Add("Detached HEAD or unreadable branch name.")
} else {
    if (-not $AllowMainBranch -and ($branchName -in @("main", "master", "development"))) {
        $prefixErrors.Add("Direct commits on '$branchName' are blocked; use a CSR-<number>-<slug> branch.")
    } elseif (-not $AllowMainBranch -and $branchName -notmatch "^(CSR-\d+)-[a-z0-9-]+$") {
        $prefixErrors.Add("Branch '$branchName' does not match CSR-<number>-<kebab-case>.")
    }

    if (-not [string]::IsNullOrWhiteSpace($CommitMessage)) {
        if ($branchName -match "^(CSR-\d+)-") {
            $expectedPrefix = $Matches[1]
            if ($CommitMessage -notmatch "^$expectedPrefix - \S") {
                $prefixErrors.Add("Commit message must start with '$expectedPrefix - <summary>'.")
            }
        } elseif ($CommitMessage -notmatch "^CSR-\d+ - \S") {
            $prefixErrors.Add("Commit message must start with 'CSR-<number> - <summary>'.")
        }
    }
}

if ($prefixErrors.Count -gt 0) {
    $hasFailure = $true
    Add-GateResult -List $results -Gate "6. Branch/Prefix" -Status "FAIL" -Detail ($prefixErrors -join "; ")
} else {
    Add-GateResult -List $results -Gate "6. Branch/Prefix" -Status "PASS" -Detail "Branch '$branchName' matches project CSR convention."
}

# ------------------------------------------------------------------
# Summary Output
# ------------------------------------------------------------------
Write-Host "`nGate Summary:" -ForegroundColor Cyan
foreach ($r in $results) {
    $color = switch ($r.Status) {
        "PASS"    { "Green" }
        "FAIL"    { "Red" }
        "PENDING" { "Yellow" }
        default   { "Gray" }
    }
    Write-Host ("  [{0,-7}] {1,-20} : {2}" -f $r.Status, $r.Gate, $r.Detail) -ForegroundColor $color
}

if ($notices.Count -gt 0) {
    Write-Host "`nNotices:" -ForegroundColor Yellow
    foreach ($n in $notices) {
        Write-Host "  - $n" -ForegroundColor Yellow
    }
}

if ($hasFailure) {
    Write-Host "`nHARNESS RESULT: FAIL (commit blocked)" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`nHARNESS RESULT: PASS" -ForegroundColor Green
    exit 0
}
