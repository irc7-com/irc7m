#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# Setup script for the Irc7m self-hosted GitHub Actions runner (macOS arm64)
#
# Prerequisites already met on this machine:
#   ✓ Xcode 26.x installed and selected (xcode-select -p)
#   ✓ .NET 10 SDK installed
#   ✓ MAUI workload installed (dotnet workload install maui)
#
# Usage:
#   1. Go to: GitHub → your repo → Settings → Actions → Runners
#              → "New self-hosted runner" → macOS → arm64
#   2. Copy the token shown on that page.
#   3. Run:  bash .github/setup-runner.sh <YOUR_TOKEN> <REPO_URL>
#      e.g.  bash .github/setup-runner.sh AABBCC1234 https://github.com/youruser/irc7m
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

TOKEN="${1:?Usage: $0 <token> <repo-url>}"
REPO="${2:?Usage: $0 <token> <repo-url>}"
RUNNER_DIR="$HOME/actions-runner"
RUNNER_VERSION="2.320.0"   # update to the latest from https://github.com/actions/runner/releases

echo "==> Creating runner directory: $RUNNER_DIR"
mkdir -p "$RUNNER_DIR"
cd "$RUNNER_DIR"

echo "==> Downloading GitHub Actions runner v${RUNNER_VERSION}"
curl -fsSL -o actions-runner-osx-arm64.tar.gz \
  "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-osx-arm64-${RUNNER_VERSION}.tar.gz"

echo "==> Extracting"
tar xzf actions-runner-osx-arm64.tar.gz

echo "==> Configuring runner (labels: self-hosted, macos, arm64)"
./config.sh \
  --url "$REPO" \
  --token "$TOKEN" \
  --name "$(hostname)-macos-arm64" \
  --labels "self-hosted,macos,arm64" \
  --unattended \
  --replace

echo "==> Installing as a launch daemon (runs on login)"
./svc.sh install
./svc.sh start

echo ""
echo "✅  Runner installed and started."
echo "    Check status:  cd $RUNNER_DIR && ./svc.sh status"
echo "    View logs:     tail -f $RUNNER_DIR/_diag/Runner_*.log"

