#!/usr/bin/env bash
# Installs SemaNami for the current user on macOS/Linux.
# Expects to be run from inside an extracted release folder that also contains the
# platform's published "SemaNami" binary (see dist/<rid>/ or a release tarball).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BINARY_SRC="$SCRIPT_DIR/SemaNami"
INSTALL_DIR="/usr/local/bin"
INSTALL_PATH="$INSTALL_DIR/SemaNami"

if [ ! -f "$BINARY_SRC" ]; then
  echo "Error: SemaNami binary not found next to install.sh ($BINARY_SRC)." >&2
  echo "Run this script from inside the extracted release folder for your platform." >&2
  exit 1
fi

if [ ! -w "$INSTALL_DIR" ]; then
  echo "Installing to $INSTALL_DIR (requires sudo)..."
  sudo cp "$BINARY_SRC" "$INSTALL_PATH"
  sudo chmod +x "$INSTALL_PATH"
else
  cp "$BINARY_SRC" "$INSTALL_PATH"
  chmod +x "$INSTALL_PATH"
fi

echo "Installed SemaNami to $INSTALL_PATH"
echo "($INSTALL_DIR is on PATH by default on macOS and most Linux distributions.)"
echo ""

if command -v SemaNami >/dev/null 2>&1; then
  echo "Let's finish setup now."
  SemaNami --setup
else
  echo "Open a new terminal, then run: SemaNami --setup"
fi
