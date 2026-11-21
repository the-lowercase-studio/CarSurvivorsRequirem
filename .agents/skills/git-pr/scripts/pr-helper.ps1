<#
.SYNOPSIS
    Deterministic helper for the user-invoked git-pr skill.
.DESCRIPTION
    Provides read-only preflight checks, CSR branch number computation, safe
    carryover stashing across main fast-forward pull, and next CSR branch
    creation with unstaged carryover reporting. Never performs pre-PR code
    validation and never deletes source branches.
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Preflight", "NextBranchNumber", "PullMain", "CreateNextBranch")]
    [string]$Action,

    [string]$BranchName = "",
    [string]$StashRef = "",
    [switch]$NoCarryover,
    [switch]$SkipFetch
)

$ErrorActionPreference = "Stop"

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

function Resolve-GhExecutable {
    $cmd = Get-Command "gh" -ErrorAction SilentlyContinue
    if ($null -ne $cmd) {
        return $cmd.Source
    }

    $candidates = @(
        (Join-Path $env:ProgramFiles "GitHub CLI\gh.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "GitHub CLI\gh.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\GitHub CLI\gh.exe")
    )

    foreach ($path in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-Path $path)) {
            return $path
        }
    }

    return $null
}

function Get-OriginRepoSlug {
    $remoteRes = Invoke-Native -Executable "git" -Arguments @("remote", "get-url", "origin")
    if ($remoteRes.ExitCode -ne 0) {
        return $null
    }
    $url = ($remoteRes.Output -join "").Trim()
    if ($url -match "github\.com[:/]([^/]+/[^/\.]+?)(?:\.git)?$") {
        return $Matches[1]
    }
    return $null
}

function Get-PorcelainFiles {
    $res = Invoke-Native -Executable "git" -Arguments @("status", "--porcelain=v1", "-uall")
    if ($res.ExitCode -ne 0) {
        throw "Failed to inspect git status."
    }

    $items = New-Object System.Collections.Generic.List[object]
    foreach ($line in $res.Output) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.Length -lt 4) {
            continue
        }
        $xy = $line.Substring(0, 2)
        $pathPart = $line.Substring(3).Trim()
        if ($pathPart -match '^"?(.+?)"?\s+->\s+"?(.+?)"?$') {
            $pathPart = "$($Matches[1]) -> $($Matches[2])"
        } elseif ($pathPart -match '^"(.+)"$') {
            $pathPart = $Matches[1]
        }
        $items.Add([PSCustomObject]@{
            Status = $xy
            Path   = $pathPart
        })
    }
    return ,$items.ToArray()
}

function Get-NextCsrBranchInfo {
    param([switch]$NoFetch)

    if (-not $NoFetch) {
        $fetchRes = Invoke-Native -Executable "git" -Arguments @("fetch", "origin", "--prune")
        if ($fetchRes.ExitCode -ne 0) {
            throw ("Failed to fetch origin: " + ($fetchRes.Output -join " "))
        }
    }

    $localRes = Invoke-Native -Executable "git" -Arguments @("for-each-ref", "--format=%(refname:short)", "refs/heads/")
    $remoteRes = Invoke-Native -Executable "git" -Arguments @("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin/")

    if ($localRes.ExitCode -ne 0 -or $remoteRes.ExitCode -ne 0) {
        throw "Failed to enumerate git branches."
    }

    $localMax = 0
    foreach ($ref in $localRes.Output) {
        $name = $ref.Trim()
        if ($name -match "^CSR-(\d+)(?:-|$)") {
            $num = [int]$Matches[1]
            if ($num -gt $localMax) {
                $localMax = $num
            }
        }
    }

    $remoteMax = 0
    foreach ($ref in $remoteRes.Output) {
        $trimmed = $ref.Trim()
        if ($trimmed -eq "origin/HEAD" -or [string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }
        $name = $trimmed -replace "^origin/", ""
        if ($name -match "^CSR-(\d+)(?:-|$)") {
            $num = [int]$Matches[1]
            if ($num -gt $remoteMax) {
                $remoteMax = $num
            }
        }
    }

    $highest = [Math]::Max($localMax, $remoteMax)
    $nextNumber = $highest + 1

    return [PSCustomObject]@{
        LocalMaxNumber  = $localMax
        RemoteMaxNumber = $remoteMax
        HighestNumber   = $highest
        NextNumber      = $nextNumber
        NextPrefix      = "CSR-$nextNumber"
    }
}

function ConvertTo-KebabSlug {
    param([string]$RawName)

    if ([string]::IsNullOrWhiteSpace($RawName)) {
        return ""
    }

    $text = $RawName.Trim()

    # Strip accidental CSR-<number> prefix so the computed max+1 number always governs
    $text = $text -replace "(?i)^csr-\d+[-_\s]*", ""

    $replacements = @{
        [string][char]0x0105 = "a"; [string][char]0x0104 = "a"
        [string][char]0x0107 = "c"; [string][char]0x0106 = "c"
        [string][char]0x0119 = "e"; [string][char]0x0118 = "e"
        [string][char]0x0142 = "l"; [string][char]0x0141 = "l"
        [string][char]0x0144 = "n"; [string][char]0x0143 = "n"
        [string][char]0x00F3 = "o"; [string][char]0x00D3 = "o"
        [string][char]0x015B = "s"; [string][char]0x015A = "s"
        [string][char]0x017A = "z"; [string][char]0x0179 = "z"
        [string][char]0x017C = "z"; [string][char]0x017B = "z"
    }

    foreach ($key in $replacements.Keys) {
        $text = $text.Replace($key, $replacements[$key])
    }

    $text = $text.ToLowerInvariant()
    $text = [regex]::Replace($text, "[^a-z0-9]+", "-")
    $text = $text.Trim("-")
    $text = [regex]::Replace($text, "-{2,}", "-")

    return $text
}

function Find-CarryoverStashRef {
    param([string]$PreferredRef)

    $sep = [char]31
    $stashRes = Invoke-Native -Executable "git" -Arguments @("stash", "list", "--format=%gd%x1f%s")
    if ($stashRes.ExitCode -ne 0) {
        return $null
    }

    if (-not [string]::IsNullOrWhiteSpace($PreferredRef)) {
        $trimmedPref = $PreferredRef.Trim()
        foreach ($line in $stashRes.Output) {
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }
            $parts = $line -split [regex]::Escape([string]$sep), 2
            if ($parts.Count -eq 2) {
                $ref = $parts[0].Trim()
                $subj = $parts[1].Trim()
                if ($ref -eq $trimmedPref -or $subj -like "*$trimmedPref*") {
                    return $ref
                }
            }
        }
        return $null
    }

    foreach ($line in $stashRes.Output) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        $parts = $line -split [regex]::Escape([string]$sep), 2
        if ($parts.Count -eq 2 -and $parts[1] -like "*git-pr-carryover::*") {
            return $parts[0].Trim()
        }
    }

    return $null
}

switch ($Action) {
    "Preflight" {
        $errors = New-Object System.Collections.Generic.List[string]
        $ghPath = Resolve-GhExecutable
        $ghUser = $null

        if ($null -eq $ghPath) {
            $errors.Add("GitHub CLI (gh) not found in PATH or standard Windows install directories.")
        } else {
            $authRes = Invoke-Native -Executable $ghPath -Arguments @("auth", "status")
            if ($authRes.ExitCode -ne 0) {
                $errors.Add("GitHub CLI is not authenticated. Run 'gh auth login' first.")
            } else {
                $userRes = Invoke-Native -Executable $ghPath -Arguments @("api", "user", "--jq", ".login")
                if ($userRes.ExitCode -eq 0) {
                    $ghUser = ($userRes.Output -join "").Trim()
                }
            }
        }

        $repoSlug = Get-OriginRepoSlug
        if ($null -eq $repoSlug) {
            $errors.Add("Could not resolve GitHub owner/repo slug from 'origin' remote.")
        }

        $branchRes = Invoke-Native -Executable "git" -Arguments @("branch", "--show-current")
        $branch = ($branchRes.Output -join "").Trim()
        $csrPrefix = $null
        $csrNumber = $null

        if ([string]::IsNullOrWhiteSpace($branch)) {
            $errors.Add("Detached HEAD or unreadable active branch.")
        } elseif ($branch -in @("main", "master", "development")) {
            $errors.Add("Active branch '$branch' cannot open a feature PR. Switch to a CSR-<number>-<kebab-case> branch.")
        } elseif ($branch -notmatch "^(CSR-(\d+))-[a-z0-9-]+$") {
            $errors.Add("Active branch '$branch' does not match required convention CSR-<number>-<kebab-case>.")
        } else {
            $csrPrefix = $Matches[1]
            $csrNumber = [int]$Matches[2]
        }

        if (-not $SkipFetch) {
            $fetchRes = Invoke-Native -Executable "git" -Arguments @("fetch", "origin", "--prune")
            if ($fetchRes.ExitCode -ne 0) {
                $errors.Add("Failed to fetch origin: " + ($fetchRes.Output -join " "))
            }
        }

        $commits = New-Object System.Collections.Generic.List[object]
        $nonConformingCommits = New-Object System.Collections.Generic.List[string]
        $csrCommitCount = 0
        $diffStat = @()

        $originMainVerify = Invoke-Native -Executable "git" -Arguments @("rev-parse", "--verify", "origin/main")
        if ($originMainVerify.ExitCode -ne 0) {
            $errors.Add("Remote branch origin/main could not be resolved.")
        } else {
            $sep = [char]31
            $logRes = Invoke-Native -Executable "git" -Arguments @("log", "origin/main..HEAD", "--reverse", "--format=%H%x1f%h%x1f%s")
            if ($logRes.ExitCode -ne 0) {
                $errors.Add("Failed to inspect commits ahead of origin/main.")
            } else {
                foreach ($line in $logRes.Output) {
                    if ([string]::IsNullOrWhiteSpace($line)) {
                        continue
                    }
                    $parts = $line -split [regex]::Escape([string]$sep), 3
                    if ($parts.Count -lt 3) {
                        continue
                    }
                    $hash = $parts[0].Trim()
                    $shortHash = $parts[1].Trim()
                    $subject = $parts[2].Trim()
                    $commits.Add([PSCustomObject]@{
                        Hash      = $hash
                        ShortHash = $shortHash
                        Subject   = $subject
                    })

                    if ($null -ne $csrPrefix) {
                        if ($subject -match ("^" + [regex]::Escape($csrPrefix) + " - \S")) {
                            $csrCommitCount++
                        } elseif ($subject -notmatch "^(Merge |Revert )") {
                            $nonConformingCommits.Add("$shortHash $subject")
                        }
                    }
                }
            }

            if ($commits.Count -eq 0) {
                $errors.Add("No commits exist ahead of origin/main on '$branch'. Create scoped commits using git-commit first.")
            } elseif ($null -ne $csrPrefix) {
                if ($csrCommitCount -eq 0) {
                    $errors.Add("No commits ahead of origin/main follow the git-commit prefix '$csrPrefix - <summary>'.")
                }
                if ($nonConformingCommits.Count -gt 0) {
                    $errors.Add("Commits ahead of origin/main must follow '$csrPrefix - <summary>': " + ($nonConformingCommits -join "; "))
                }
            }

            $statRes = Invoke-Native -Executable "git" -Arguments @("diff", "--stat", "origin/main..HEAD")
            if ($statRes.ExitCode -eq 0) {
                $diffStat = $statRes.Output
            }
        }

        $existingPr = $null
        if ($null -ne $ghPath -and $null -ne $repoSlug -and -not [string]::IsNullOrWhiteSpace($branch) -and ($errors.Count -eq 0 -or $commits.Count -gt 0)) {
            $prRes = Invoke-Native -Executable $ghPath -Arguments @("pr", "list", "--repo", $repoSlug, "--head", $branch, "--base", "main", "--state", "open", "--json", "number,url,title,author")
            if ($prRes.ExitCode -eq 0) {
                $prJsonText = ($prRes.Output -join "`n").Trim()
                if (-not [string]::IsNullOrWhiteSpace($prJsonText) -and $prJsonText -ne "[]") {
                    $parsedPrs = $prJsonText | ConvertFrom-Json
                    if ($parsedPrs -is [System.Array] -and $parsedPrs.Count -gt 0) {
                        $existingPr = $parsedPrs[0]
                    } elseif ($null -ne $parsedPrs -and $null -ne $parsedPrs.number) {
                        $existingPr = $parsedPrs
                    }
                }
            }
        }

        $dirtyFiles = Get-PorcelainFiles

        $result = [PSCustomObject]@{
            Ok           = ($errors.Count -eq 0)
            Errors       = @($errors.ToArray())
            GhPath       = $ghPath
            GhUser       = $ghUser
            RepoSlug     = $repoSlug
            Branch       = $branch
            CsrPrefix    = $csrPrefix
            CsrNumber    = $csrNumber
            CommitCount  = $commits.Count
            Commits      = @($commits.ToArray())
            DiffStat     = @($diffStat)
            ExistingPr   = $existingPr
            DirtyFiles   = @($dirtyFiles)
        }
        $result | ConvertTo-Json -Depth 6
        if ($errors.Count -gt 0) {
            exit 1
        }
        exit 0
    }

    "NextBranchNumber" {
        $info = Get-NextCsrBranchInfo -NoFetch:$SkipFetch
        $info | ConvertTo-Json -Depth 4
        exit 0
    }

    "PullMain" {
        $branchRes = Invoke-Native -Executable "git" -Arguments @("branch", "--show-current")
        $sourceBranch = ($branchRes.Output -join "").Trim()
        $dirtyFiles = Get-PorcelainFiles
        $stashCreated = $false
        $stashMessage = $null
        $stashRefOut = $null

        if ($dirtyFiles.Count -gt 0) {
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $stashMessage = "git-pr-carryover::$sourceBranch::$timestamp"
            $stashRes = Invoke-Native -Executable "git" -Arguments @("stash", "push", "--include-untracked", "-m", $stashMessage)
            if ($stashRes.ExitCode -ne 0) {
                throw ("Failed to stash uncommitted changes before pulling main: " + ($stashRes.Output -join " "))
            }
            $stashCreated = $true
            $stashRefOut = Find-CarryoverStashRef -PreferredRef $stashMessage
        }

        $mainExistsRes = Invoke-Native -Executable "git" -Arguments @("show-ref", "--verify", "--quiet", "refs/heads/main")
        if ($mainExistsRes.ExitCode -eq 0) {
            $coRes = Invoke-Native -Executable "git" -Arguments @("checkout", "main")
        } else {
            $coRes = Invoke-Native -Executable "git" -Arguments @("checkout", "-b", "main", "origin/main")
        }

        if ($coRes.ExitCode -ne 0) {
            $stashHint = if ($stashCreated) { " Uncommitted changes remain safely stashed in $stashRefOut ($stashMessage)." } else { "" }
            throw ("Failed to checkout main: " + ($coRes.Output -join " ") + $stashHint)
        }

        $pullRes = Invoke-Native -Executable "git" -Arguments @("pull", "--ff-only", "origin", "main")
        if ($pullRes.ExitCode -ne 0) {
            $stashHint = if ($stashCreated) { " Uncommitted changes remain safely stashed in $stashRefOut ($stashMessage)." } else { "" }
            throw ("Failed to pull origin/main cleanly: " + ($pullRes.Output -join " ") + $stashHint)
        }

        $headHashRes = Invoke-Native -Executable "git" -Arguments @("rev-parse", "--short", "HEAD")
        $mainHead = ($headHashRes.Output -join "").Trim()
        $nextInfo = Get-NextCsrBranchInfo -NoFetch:$SkipFetch

        $result = [PSCustomObject]@{
            Ok                 = $true
            SourceBranch       = $sourceBranch
            ActiveBranch       = "main"
            MainHeadShortHash  = $mainHead
            StashCreated       = $stashCreated
            StashRef           = $stashRefOut
            StashMessage       = $stashMessage
            StashedFiles       = @($dirtyFiles)
            NextBranchInfo     = $nextInfo
        }
        $result | ConvertTo-Json -Depth 6
        exit 0
    }

    "CreateNextBranch" {
        if ([string]::IsNullOrWhiteSpace($BranchName)) {
            throw "BranchName parameter is required for CreateNextBranch."
        }

        $slug = ConvertTo-KebabSlug -RawName $BranchName
        if ([string]::IsNullOrWhiteSpace($slug)) {
            throw "Provided branch name '$BranchName' yielded an empty kebab-case slug. Provide an English kebab-case name."
        }

        $nextInfo = Get-NextCsrBranchInfo -NoFetch:$SkipFetch
        $targetBranch = "$($nextInfo.NextPrefix)-$slug"

        if ($targetBranch -notmatch "^(CSR-\d+)-[a-z0-9-]+$") {
            throw "Computed branch '$targetBranch' failed CSR convention regex validation."
        }

        $localCheck = Invoke-Native -Executable "git" -Arguments @("show-ref", "--verify", "--quiet", "refs/heads/$targetBranch")
        if ($localCheck.ExitCode -eq 0) {
            throw "Local branch '$targetBranch' already exists."
        }
        $remoteCheck = Invoke-Native -Executable "git" -Arguments @("show-ref", "--verify", "--quiet", "refs/remotes/origin/$targetBranch")
        if ($remoteCheck.ExitCode -eq 0) {
            throw "Remote branch 'origin/$targetBranch' already exists."
        }

        $mainVerify = Invoke-Native -Executable "git" -Arguments @("show-ref", "--verify", "--quiet", "refs/heads/main")
        $baseRef = if ($mainVerify.ExitCode -eq 0) { "main" } else { "origin/main" }

        $createRes = Invoke-Native -Executable "git" -Arguments @("checkout", "-b", $targetBranch, $baseRef)
        if ($createRes.ExitCode -ne 0) {
            throw ("Failed to create branch '$targetBranch': " + ($createRes.Output -join " "))
        }

        $targetStash = $null
        if (-not $NoCarryover) {
            if (-not [string]::IsNullOrWhiteSpace($StashRef)) {
                $targetStash = Find-CarryoverStashRef -PreferredRef $StashRef
                if ([string]::IsNullOrWhiteSpace($targetStash)) {
                    throw "Requested carryover stash '$StashRef' was not found in git stash list."
                }
            } else {
                $targetStash = Find-CarryoverStashRef
            }
        }
        $carryoverStatus = "NONE"
        $carryoverOutput = @()

        if (-not [string]::IsNullOrWhiteSpace($targetStash)) {
            $popRes = Invoke-Native -Executable "git" -Arguments @("stash", "pop", $targetStash)
            $carryoverOutput = $popRes.Output
            if ($popRes.ExitCode -ne 0) {
                $carryoverStatus = "CONFLICT_STASH_PRESERVED"
            } else {
                # Ensure all restored changes (including any previously staged edits) land unstaged
                [void](Invoke-Native -Executable "git" -Arguments @("reset", "--quiet"))
                $carryoverStatus = "RESTORED_UNSTAGED"
            }
        } else {
            # Also normalize any pre-existing dirty edits into unstaged state if present
            $preDirty = Get-PorcelainFiles
            if ($preDirty.Count -gt 0) {
                [void](Invoke-Native -Executable "git" -Arguments @("reset", "--quiet"))
                $carryoverStatus = "PRESENT_UNSTAGED"
            }
        }

        $carriedOverFiles = Get-PorcelainFiles

        $result = [PSCustomObject]@{
            Ok                = ($carryoverStatus -ne "CONFLICT_STASH_PRESERVED")
            CreatedBranch     = $targetBranch
            BaseRef           = $baseRef
            NextBranchInfo    = $nextInfo
            CarryoverStatus   = $carryoverStatus
            StashRefUsed      = $targetStash
            CarryoverMessages = @($carryoverOutput)
            CarriedOverFiles  = @($carriedOverFiles)
        }
        $result | ConvertTo-Json -Depth 6
        if ($carryoverStatus -eq "CONFLICT_STASH_PRESERVED") {
            exit 1
        }
        exit 0
    }
}
