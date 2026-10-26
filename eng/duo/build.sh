#!/bin/bash
set -euo pipefail

: "${DEVELOPER_DIR:?Set DEVELOPER_DIR to the side-by-side Xcode 27.1 developer directory}"
: "${MauiSourceRoot:?Set MauiSourceRoot to MAUI net11.0 at a561eaec4aacbe962416c64491aa5ffbc6bde9ae}"
: "${DOTNET_ROOT:?Set DOTNET_ROOT to an isolated SDK 10.0.402 with workload set 10.0.402.1}"

cd "$(dirname "$0")"
"$DOTNET_ROOT/dotnet" build PitchLens.Duo.slnx \
  -p:PitchLensDuo=true \
  -p:MauiSourceRoot="$MauiSourceRoot" \
  -p:_MauiDotNetVersionMajor=10 \
  -p:IosTargetFrameworkVersion=27.1 \
  -p:IncludeAndroidTargetFrameworks=false \
  -p:IncludeMacCatalystTargetFrameworks=false \
  -p:IncludeWindowsTargetFrameworks=false \
  -p:SupportedOSPlatformVersion=17.0 \
  -p:TreatWarningsAsErrors=false \
  -p:EnableNETAnalyzers=false \
  -p:EnableTrimAnalyzer=false \
  -p:EnableAotAnalyzer=false \
  "$@"
