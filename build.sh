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
# Builds and tests phileas-net inside the official .NET 10 SDK Docker image, so
# the only requirement on the host is Docker — no .NET SDK needed.
#
# phileas-net references the PhiSQL .NET reference library via a ProjectReference
# (../../../phisql/reference/dotnet/PhiSql/PhiSql.csproj) because it is not yet on
# NuGet, so the phisql repository must be checked out as a SIBLING directory named
# "phisql" next to this repository, e.g.:
#
#     code/
#     ├── phileas-net/   <- this repository
#     └── phisql/        <- git clone https://github.com/philterd/phisql
#
# The parent directory containing both repos is mounted into the container so the
# relative ProjectReference (and the spec/schema files phisql embeds) resolve.
#
# Usage: ./build.sh [Release|Debug]   (default: Release)
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
parent="$(cd "$here/.." && pwd)"
phileas_dir="$(basename "$here")"
config="${1:-Release}"
image="mcr.microsoft.com/dotnet/sdk:10.0"

if ! command -v docker >/dev/null 2>&1; then
  echo "error: docker is not installed or not on PATH." >&2
  exit 1
fi

if [ ! -f "$parent/phisql/reference/dotnet/PhiSql/PhiSql.csproj" ]; then
  echo "error: the phisql repository was not found at $parent/phisql." >&2
  echo "       phileas-net references it via a ProjectReference, so clone it as a sibling:" >&2
  echo "         git clone https://github.com/philterd/phisql \"$parent/phisql\"" >&2
  exit 1
fi

# Run the container as the host user so build output (bin/obj) is not left
# root-owned; HOME=/tmp gives the SDK a writable NuGet/cache location.
exec docker run --rm \
  -u "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  -e BUILD_CONFIG="$config" \
  -e PHILEAS_DIR="$phileas_dir" \
  -v "$parent":/src \
  -w "/src/$phileas_dir" \
  "$image" \
  bash -ec '
    echo "==> Restoring [$BUILD_CONFIG]"
    dotnet restore Phileas.slnx
    echo "==> Building [$BUILD_CONFIG]"
    dotnet build --no-restore -c "$BUILD_CONFIG" Phileas.slnx
    echo "==> Testing [$BUILD_CONFIG]"
    dotnet test --no-build -c "$BUILD_CONFIG" --verbosity normal Phileas.slnx
    echo "==> Done."
  '
