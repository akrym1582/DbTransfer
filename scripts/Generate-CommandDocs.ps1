[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src/DbTransfer/DbTransfer.csproj"
$guideDirectory = Join-Path $repoRoot "docs/guides"
$providerGuide = Join-Path $repoRoot "docs/providers.md"
$scriptGuide = Join-Path $repoRoot "docs/csharp-scripts.md"
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot "docs/commands"
}

$examples = @{
    copy = @(
        "dbtransfer copy --source-provider postgresql --source-connection `$SOURCE_DATABASE --query 'select * from public.orders order by id' --destination-provider sqlserver --destination-connection `$DESTINATION_DATABASE --destination-table dbo.Orders",
        "dbtransfer copy --source-provider mysql --source-connection `$SOURCE_DATABASE --query 'select id, name from users order by id' --destination-provider oracle --destination-connection `$DESTINATION_DATABASE --destination-table APP.USERS --create-table --checkpoint copy.json --resume"
    )
    export = @(
        "dbtransfer export --provider postgresql --connection `$DATABASE --query 'select * from orders order by id' --format jsonl --output -",
        "dbtransfer export --provider postgresql --connection `$DATABASE --query 'select * from orders order by id' --format extended-json | gzip > orders.jsonl.gz"
    )
    import = @(
        "dbtransfer import --input orders.csv --format csv --destination-provider postgresql --destination-connection `$DATABASE --destination-table public.orders",
        "gzip -dc orders.jsonl.gz | dbtransfer import --format extended-json --destination-provider postgresql --destination-connection `$DATABASE --destination-table public.orders"
    )
    exec = @(
        "dbtransfer exec --provider postgresql --connection `$DATABASE --sql 'select id, total from orders' --format jsonl",
        "dbtransfer exec --provider sqlserver --connection `$DATABASE --sql 'select Id, Name from dbo.Jobs' --format csv --output jobs.csv"
    )
    inspect = @(
        "dbtransfer inspect --provider postgresql --connection `$DATABASE --query 'select * from orders limit 0'"
    )
    validate = @(
        "dbtransfer validate --provider postgresql --connection `$DATABASE --query 'select * from orders limit 0'"
    )
}

function Invoke-DbTransferHelp {
    param([string[]]$Arguments)

    $dotnetArguments = @("run", "--project", $project, "--configuration", $Configuration, "--no-build")
    $dotnetArguments += "--"
    $dotnetArguments += $Arguments

    $output = & dotnet @dotnetArguments 2>&1 | Out-String
    if ($LASTEXITCODE -notin @(0, 2)) {
        throw "DbTransfer help failed with exit code $LASTEXITCODE.`n$output"
    }

    return (($output.TrimEnd() -split "`r?`n") |
        ForEach-Object { $_ -replace '^DbTransfer .+$', 'DbTransfer <version>' }) -join "`n"
}

if (-not $NoBuild) {
    & dotnet build $project --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "DbTransfer build failed with exit code $LASTEXITCODE."
    }
}

$rootHelp = Invoke-DbTransferHelp -Arguments @("--help")
$commands = @()
$currentCommand = $null
foreach ($line in ($rootHelp -split "`n")) {
    if ($line -match '^  (?<name>\S+)\s{2,}(?<description>.+)$') {
        $currentCommand = if ($Matches.name -notin @("help", "version")) {
            [pscustomobject]@{ Name = $Matches.name; Description = $Matches.description.Trim() }
        }
        else {
            $null
        }

        if ($null -ne $currentCommand) {
            $commands += $currentCommand
        }
    }
    elseif ($null -ne $currentCommand -and $line -match '^\s{4,}(?<continuation>\S.+)$') {
        $currentCommand.Description += " $($Matches.continuation.Trim())"
    }
    else {
        $currentCommand = $null
    }
}

if (-not $commands) {
    throw "No commands were discovered in the root help output."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Get-ChildItem -Path $OutputDirectory -Filter "*.md" -File | Remove-Item
$fence = '```'

foreach ($command in $commands) {
    $help = Invoke-DbTransferHelp -Arguments @($command.Name, "--help")
    $sampleLines = if ($examples.ContainsKey($command.Name)) {
        ($examples[$command.Name] | ForEach-Object { "$fence" + "sh`n$_`n$fence" }) -join "`n`n"
    }
    else {
        "_No usage example has been defined._"
    }

    $note = "This command has a production streaming implementation."
    $guidePath = Join-Path $guideDirectory "$($command.Name).md"
    $learnMoreLinks = @()
    if (Test-Path -LiteralPath $guidePath -PathType Leaf) {
        $relativeGuidePath = [System.IO.Path]::GetRelativePath($OutputDirectory, $guidePath).Replace('\', '/')
        $learnMoreLinks += "- See the [$($command.Name) guide]($relativeGuidePath) for a walkthrough, practical examples, and operational notes."
    }

    if (Test-Path -LiteralPath $providerGuide -PathType Leaf) {
        $relativeProviderGuide = [System.IO.Path]::GetRelativePath($OutputDirectory, $providerGuide).Replace('\', '/')
        $learnMoreLinks += "- See [data providers]($relativeProviderGuide) for provider-specific query syntax, capabilities, and limitations."
    }

    if ($command.Name -in @("export", "import") -and (Test-Path -LiteralPath $scriptGuide -PathType Leaf)) {
        $relativeScriptGuide = [System.IO.Path]::GetRelativePath($OutputDirectory, $scriptGuide).Replace('\', '/')
        $learnMoreLinks += "- See [in-process C# record scripts]($relativeScriptGuide) for the script API, result contract, and examples."
    }

    $learnMore = if ($learnMoreLinks.Count -gt 0) {
        "## Learn more`n`n$($learnMoreLinks -join "`n")`n"
    }
    else {
        ""
    }
    $content = @"
<!-- Generated by scripts/Generate-CommandDocs.ps1. Do not edit directly. -->

# dbtransfer $($command.Name)

$($command.Description)

> [!NOTE]
> $note

$learnMore
## Help

$($fence)text
$help
$fence

## Usage examples

$sampleLines
"@
    $path = Join-Path $OutputDirectory "$($command.Name).md"
    [System.IO.File]::WriteAllText($path, $content.TrimEnd() + "`n", [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Generated $($commands.Count) command pages in $OutputDirectory"
