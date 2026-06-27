@echo off
rem Copyright 2026 Philterd, LLC.
rem
rem Licensed under the Apache License, Version 2.0 (the "License");
rem you may not use this file except in compliance with the License.
rem You may obtain a copy of the License at
rem
rem     http://www.apache.org/licenses/LICENSE-2.0
rem
rem Unless required by applicable law or agreed to in writing, software
rem distributed under the License is distributed on an "AS IS" BASIS,
rem WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
rem See the License for the specific language governing permissions and
rem limitations under the License.
rem
rem Builds and tests phileas-dotnet. Requires the .NET 10 SDK on PATH.
rem
rem phileas-dotnet references the PhiSQL .NET reference library via the Philterd.PhiSql
rem NuGet package, which is restored from NuGet.org during the build. No local checkout
rem of the phisql repository is required.
rem
rem Usage: build.bat [Release^|Debug]   (default: Release)
setlocal

set "here=%~dp0"
set "config=%~1"
if "%config%"=="" set "config=Release"

cd /d "%here%"

echo ==^> Building [%config%]
dotnet build -c "%config%" Phileas.slnx
if errorlevel 1 exit /b %errorlevel%
echo ==^> Testing [%config%]
dotnet test -c "%config%" --verbosity normal Phileas.slnx
if errorlevel 1 exit /b %errorlevel%
echo ==^> Done.
