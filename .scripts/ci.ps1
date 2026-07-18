Param(
    [String]$SolutionFilter = "K7.CI.slnf"
)

$testProjects = @(
    "tests/Domain.UnitTests/Domain.UnitTests.csproj",
    "tests/Application.UnitTests/Application.UnitTests.csproj",
    "tests/Clients.ComponentTests/Clients.ComponentTests.csproj",
    "tests/Web.SmokeTests/Web.SmokeTests.csproj",
    "tests/Clients.DesignSystem.SmokeTests/Clients.DesignSystem.SmokeTests.csproj"
)

function RunStep {
    param(
        [String]$Name,
        [ScriptBlock]$Command
    )

    Write-Host "==> $Name" -ForegroundColor Green
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "'$Name' failed with exit code $LASTEXITCODE."
    }
}

try {
    RunStep "Restore" { dotnet restore $SolutionFilter }
    RunStep "Build" { dotnet build $SolutionFilter --configuration Release --no-restore }
    RunStep "Verify formatting" { dotnet format $SolutionFilter --verify-no-changes --no-restore }

    foreach ($testProject in $testProjects) {
        RunStep "Test ($testProject)" { dotnet test $testProject --configuration Release --no-build --verbosity normal }
    }
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host "CI script terminated due to a failed step." -ForegroundColor Red
    exit 1
}

Write-Host "Done" -ForegroundColor Green
