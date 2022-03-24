param(
    [string] $sonarSecret
)


Install-package BuildUtils -Confirm:$false -Scope CurrentUser -Force
Import-Module BuildUtils

$runningDirectory = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition

$testOutputDir = "$runningDirectory/TestResults"

if (Test-Path $testOutputDir) 
{
    Write-host "Cleaning temporary Test Output path $testOutputDir"
    Remove-Item $testOutputDir -Recurse -Force
}

$version = Invoke-Gitversion
$assemblyVer = $version.assemblyVersion 

$branch = git branch --show-current
Write-Host "branch is $branch"

dotnet tool restore
dotnet tool install --global dotnet-sonarscanner
dotnet-sonarscanner begin /k:"mohanpaladugu_VirtualEconomyFramework" /v:"$assemblyVer" /o:"mohanpaladugu" /d:sonar.login="$sonarSecret" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.cs.vstest.reportsPaths=TestResults/*.trx /d:sonar.cs.opencover.reportsPaths=TestResults/*/coverage.opencover.xml /d:sonar.coverage.exclusions="**Test*.cs" /d:sonar.branch.name="$branch"

dotnet restore src
dotnet build ./VirtualEconomyFramework/VENFTApp-Server --configuration release
dotnet test "./VirtualEconomyFramework/VEFrameworkUnitTest/VEFrameworkUnitTest.csproj" --collect:"XPlat Code Coverage" --results-directory TestResults/ --logger "trx;LogFileName=unittests.trx" --no-build --no-restore --configuration release -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
         

dotnet tool run dotnet-sonarscanner end /d:sonar.login="$sonarSecret"
