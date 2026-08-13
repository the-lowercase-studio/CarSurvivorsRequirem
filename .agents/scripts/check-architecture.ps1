<#
.SYNOPSIS
    Architecture & Coding Standard Linter for Unity C# codebase (Car Survivors).
.DESCRIPTION
    Scans Assets/Scripts/ for architectural anti-patterns, forbidden scene lookups,
    singleton leaks, public mutable fields, LINQ usage, arrow-method syntax, and unsafe attributes.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .agents/scripts/check-architecture.ps1
#>

param(
    [string]$ScriptsPath = "Assets/Scripts"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ScriptsPath)) {
    Write-Error "Scripts path '$ScriptsPath' not found."
    exit 1
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Running Unity C# Architecture & Standards Linter" -ForegroundColor Cyan
Write-Host " Target Path: $ScriptsPath" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$csFiles = Get-ChildItem -Path $ScriptsPath -Recurse -Filter "*.cs"
$totalFiles = $csFiles.Count
$violationCount = 0
$warningCount = 0

Write-Host "Found $totalFiles C# files to analyze.`n" -ForegroundColor Gray

# Rules definition
$rules = @(
    @{
        Id = "ARCH001"
        Name = "Forbidden Scene Lookup"
        Severity = "Error"
        Pattern = "\b(FindObjectOfType|FindObjectsOfType|FindAnyObjectByType|FindFirstObjectByType|FindObjectsByType|FindWithTag|FindGameObjectsWithTag|FindGameObjectWithTag|GameObject\.Find)\b"
        Description = "Direct scene lookup violates DI architecture. Use [Inject] via Reflex DI instead."
    },
    @{
        Id = "ARCH002"
        Name = "Static Singleton Anti-Pattern"
        Severity = "Error"
        Pattern = "\bpublic\s+static\s+[A-Za-z0-9_<>.]+\s+(Instance|instance)\b"
        Description = "Static singleton instance leaks state. Bind services in Reflex DI installers."
    },
    @{
        Id = "ARCH003"
        Name = "Unsafe DontDestroyOnLoad"
        Severity = "Warning"
        Pattern = "\bDontDestroyOnLoad\b"
        AllowedPathKeywords = @("ReflexDI", "ObjectLifecycle")
        Description = "DontDestroyOnLoad should only be called inside Reflex boot installers or dedicated lifecycle scripts."
    },
    @{
        Id = "ARCH004"
        Name = "Public Mutable Inspector Field"
        Severity = "Warning"
        Pattern = "^\s*public\s+(?!const\b|static\b|class\b|struct\b|enum\b|interface\b|event\b|override\b|virtual\b|abstract\b|sealed\b|async\b|readonly\b)[A-Za-z0-9_<>\[\]?,\s]+\s+[a-zA-Z0-9_]+\s*;"
        Description = "Prefer [SerializeField] private fields or { get; private set; } auto-properties over public mutable inspector fields."
    },
    @{
        Id = "ARCH005"
        Name = "Banned LINQ Usage"
        Severity = "Error"
        Pattern = "\busing\s+System\.Linq\b|\bSystem\.Linq\."
        Description = "LINQ usage is banned in gameplay and system code to prevent GC allocations. Use standard explicit loops."
    },
    @{
        Id = "ARCH006"
        Name = "Expression-Bodied Method Syntax Violation"
        Severity = "Warning"
        Pattern = "^\s*(public|private|protected|internal)\s+(static\s+|virtual\s+|override\s+|async\s+)*[A-Za-z0-9_<>\[\]?,\s]+\s+[A-Za-z0-9_]+\s*\([^)]*\)\s*=>"
        Description = "Do not use expression-bodied arrow syntax (=>) for methods. Always use standard block bodies {} with curly braces."
    },
    @{
        Id = "ARCH007"
        Name = "Unsafe Serialized Readonly Field"
        Severity = "Error"
        Pattern = "\[SerializeField\].*?\breadonly\b"
        Description = "[SerializeField] fields must not be readonly as Unity cannot serialize readonly fields."
    },
    @{
        Id = "ARCH008"
        Name = "Interface Naming Convention Violation"
        Severity = "Warning"
        Pattern = "^\s*(public|internal|private)?\s*interface\s+(?!I[A-Z])[A-Za-z0-9_]+"
        Description = "Interface names must start with a capital 'I' (e.g., IPlayerService)."
    }
)

foreach ($file in $csFiles) {
    $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "").Replace("\", "/")
    $lines = Get-Content -Path $file.FullName
    $inBlockComment = $false
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $lineNum = $i + 1
        $lineText = $lines[$i]
        $trimmedLine = $lineText.Trim()
        
        if ($trimmedLine.StartsWith("/*")) {
            $inBlockComment = $true
        }
        if ($inBlockComment) {
            if ($trimmedLine.EndsWith("*/") -or $trimmedLine.Contains("*/")) {
                $inBlockComment = $false
            }
            continue
        }
        
        # Skip single-line comments
        if ($trimmedLine.StartsWith("//")) {
            continue
        }
        
        foreach ($rule in $rules) {
            if ($lineText -match $rule.Pattern) {
                # Check allowed path keywords
                $isAllowed = $false
                if ($rule.AllowedPathKeywords) {
                    foreach ($keyword in $rule.AllowedPathKeywords) {
                        if ($relativePath -like "*$keyword*") {
                            $isAllowed = $true
                            break
                        }
                    }
                }
                if ($isAllowed) {
                    continue
                }
                
                $fileLink = "file:///$($file.FullName.Replace('\', '/'))#L$lineNum"
                
                if ($rule.Severity -eq "Error") {
                    $violationCount++
                    Write-Host "[$($rule.Id)] ERROR: $($rule.Name)" -ForegroundColor Red
                } else {
                    $warningCount++
                    Write-Host "[$($rule.Id)] WARN: $($rule.Name)" -ForegroundColor Yellow
                }
                
                Write-Host "  File: $fileLink" -ForegroundColor White
                Write-Host "  Line ${lineNum}: $trimmedLine" -ForegroundColor Gray
                Write-Host "  Note: $($rule.Description)`n" -ForegroundColor DarkGray
            }
        }
    }
}

$errorColor = if ($violationCount -gt 0) { "Red" } else { "Green" }
$warningColor = if ($warningCount -gt 0) { "Yellow" } else { "Green" }

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Architecture & Standards Audit Summary" -ForegroundColor Cyan
Write-Host " Total Files Audited: $totalFiles" -ForegroundColor White
Write-Host " Errors Found:       $violationCount" -ForegroundColor $errorColor
Write-Host " Warnings Found:     $warningCount" -ForegroundColor $warningColor
Write-Host "==================================================" -ForegroundColor Cyan

if ($violationCount -gt 0) {
    Write-Host "`nResult: Audit FAILED with $violationCount critical error(s)." -ForegroundColor Red
    exit 1
} else {
    Write-Host "`nResult: Audit PASSED cleanly!" -ForegroundColor Green
    exit 0
}

