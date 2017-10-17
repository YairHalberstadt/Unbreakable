@echo off
powershell "$versionSuffix = '%1'; $output = (Resolve-Path .); @('Build.PolicyReport', 'Policy', '[Package]') | %% { dotnet restore /p:VersionSuffix=$versionSuffix; dotnet pack $($_+'\'+$_+'.csproj') $(if ($versionSuffix) { '--version-suffix='+$versionSuffix } else { '' }) --output $output --configuration Release /p:UnbreakablePolicyReportEnabled=false }"