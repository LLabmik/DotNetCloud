#!/bin/bash
# tools/setup-docker-wsl.sh
# Installs Docker Engine inside WSL (Linux Mint 22 / Ubuntu 24.04 base)
#
# Usage from PowerShell:
#   wsl bash /mnt/d/Repos/dotnetcloud/tools/setup-docker-wsl.sh
#
# After installation, restart WSL from PowerShell:
#   wsl --shutdown
#
# Then verify:
#   wsl docker run --rm hello-world

set -euo pipefail

echo "=== DotNetCloud: Docker Engine Setup for WSL ==="
echo ""

# Check if Docker is already installed
if command -v docker &>/dev/null; then
    echo "Docker is already installed: $(docker --version)"
    if sudo systemctl is-active --quiet docker 2>/dev/null; then
        echo "Docker daemon is running."
    else
        echo "Starting Docker daemon..."
        sudo systemctl start docker
        sudo systemctl enable docker
    fi
    # Ensure current user is in docker group
    if ! groups "$USER" | grep -q docker; then
        sudo usermod -aG docker "$USER"
        echo "Added $USER to docker group. Restart WSL for this to take effect."
    fi
    echo ""
    echo "Done! Docker is ready."
    exit 0
fi

echo "Installing Docker Engine..."
echo ""

# Remove conflicting packages
sudo apt-get remove -y docker.io docker-doc docker-compose podman-docker containerd runc 2>/dev/null || true

# Install prerequisites
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg

# Add Docker's official GPG key
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

# Determine Ubuntu codename (Mint 22 = noble, Mint 21 = jammy)
UBUNTU_CODENAME=$(. /etc/os-release && echo "${UBUNTU_CODENAME:-noble}")
echo "Using Ubuntu codename: $UBUNTU_CODENAME"

# Add Docker repository
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $UBUNTU_CODENAME stable" | \
    sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update

# Install Docker Engine
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Add current user to docker group (run docker without sudo)
sudo usermod -aG docker "$USER"

# Enable systemd in WSL (required for Docker daemon auto-start)
if [ -f /etc/wsl.conf ]; then
    if ! grep -q "\[boot\]" /etc/wsl.conf; then
        printf "\n[boot]\nsystemd=true\n" | sudo tee -a /etc/wsl.conf > /dev/null
    elif ! grep -q "systemd=true" /etc/wsl.conf; then
        sudo sed -i '/\[boot\]/a systemd=true' /etc/wsl.conf
    fi
else
    printf "[boot]\nsystemd=true\n" | sudo tee /etc/wsl.conf > /dev/null
fi

echo ""
echo "========================================="
echo " Docker installed successfully!"
echo "========================================="
echo ""
echo "Next steps (run from PowerShell):"
echo ""
echo "  1. Restart WSL:        wsl --shutdown"
echo "  2. Verify Docker:      wsl docker run --rm hello-world"
echo "  3. Run DB tests:       dotnet test tests\DotNetCloud.Integration.Tests --filter DockerDatabase --no-build"
echo ""
