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
dotnet-sonarscanner begin /k:"mohanpaladugu_VirtualEconomyFramework"  /o:"mohanpaladugu" /d:sonar.login="$sonarSecret" /d:sonar.host.url="https://sonarcloud.io"  /d:sonar.cs.opencover.reportsPaths=TestResults/*/*.opencover.xml

dotnet restore ./VirtualEconomyFramework/VENFTApp-Server
dotnet build ./VirtualEconomyFramework/VENFTApp-Server --configuration release
dotnet test "./VirtualEconomyFramework/VEFrameworkUnitTest/VEFrameworkUnitTest.csproj"  --collect:"XPlat Code Coverage" --results-directory TestResults/ --logger "trx;LogFileName=unittests.trx" --no-build --no-restore --configuration release -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
         
dotnet-sonarscanner end /d:sonar.login="$sonarSecret"
