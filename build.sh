#!/usr/bin/env bash
# Copyright 2026 Philterd, LLC.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# Builds and tests phileas-dotnet. Requires the .NET 10 SDK on PATH.
#
# phileas-dotnet references the PhiSQL .NET reference library via the Philterd.PhiSql
# NuGet package, which is restored from NuGet.org during the build. No local checkout
# of the phisql repository is required.
#
# Usage: ./build.sh [Release|Debug]   (default: Release)
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
config="${1:-Release}"

cd "$here"

echo "==> Building [$config]"
dotnet build -c "$config" Phileas.slnx
echo "==> Testing [$config]"
dotnet test -c "$config" --verbosity normal Phileas.slnx
echo "==> Done."
