#!/bin/bash
# deploy.sh - Deploys Kitchen Orchestrator to OCI x86_64 instance
# Usage: ./infra/deploy.sh

set -e

# ---- Configuration ----
OCI_HOST="ubuntu@145.241.214.219"
SSH_KEY="C:/MyFiles/CookedKeys/ssh-key-2026-03-11.key"
REPO_URL="https://github.com/Ashhuby/Kitchen-Orchestrator-Backend.git"
APP_DIR="/home/ubuntu/kitchen-orchestrator"
ENV_FILE=".env"

echo "=== Kitchen Orchestrator Deployment ==="
echo "Target: $OCI_HOST (x86_64)"
echo "======================================="

# ---- Step 1: Verify .env exists locally ----
if [ ! -f "$ENV_FILE" ]; then
    echo "ERROR: .env file not found at repo root. Aborting."
    exit 1
fi

# ---- Step 2: Install Docker + ensure swap exists ----
echo "[1/5] Checking Docker, Compose, and swap..."
ssh -i "$SSH_KEY" "$OCI_HOST" bash << 'REMOTE'
    # Install Docker if missing
    if ! command -v docker &> /dev/null; then
        echo "Installing Docker..."
        sudo apt-get update -y
        sudo apt-get install -y ca-certificates curl gnupg
        sudo install -m 0755 -d /etc/apt/keyrings
        curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
        sudo chmod a+r /etc/apt/keyrings/docker.gpg
        echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
        sudo apt-get update -y
        sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
        sudo systemctl start docker
        sudo systemctl enable docker
        sudo usermod -aG docker ubuntu
        echo "Docker installed."
    fi

    # Add 2GB swap if not already present — dotnet publish needs it on 1GB RAM bc we BROKE
    if [ ! -f /swapfile ]; then
        echo "Creating 2GB swapfile..."
        sudo fallocate -l 2G /swapfile
        sudo chmod 600 /swapfile
        sudo mkswap /swapfile
        sudo swapon /swapfile
        echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
        echo "Swap created."
    else
        echo "Swap already exists, skipping."
    fi

    echo "Memory status:"
    free -h

    docker compose version
REMOTE

# ---- Step 3: Clone or update repo ----
echo "[2/5] Cloning/updating repository..."
ssh -i "$SSH_KEY" "$OCI_HOST" bash << REMOTE
    if [ ! -d "$APP_DIR" ]; then
        git clone $REPO_URL $APP_DIR
        cd $APP_DIR && git checkout feature/station-interactions
    else
        cd $APP_DIR && git pull origin feature/station-interactions
    fi
REMOTE

# ---- Step 4: Copy .env file ----
echo "[3/5] Copying .env file..."
scp -i "$SSH_KEY" "$ENV_FILE" "$OCI_HOST:$APP_DIR/.env"

# ---- Step 5: Build sequentially then start ----
echo "[4/5] Building and starting containers..."
echo "      (Building one at a time to avoid OOM on 1GB RAM — this will take ~5 mins)"
ssh -i "$SSH_KEY" "$OCI_HOST" bash << REMOTE
    cd $APP_DIR
    sudo docker compose down || true

    echo "--- Building identity-api ---"
    sudo docker compose build identity-api

    echo "--- Building game-server ---"
    sudo docker compose build game-server

    echo "--- Building nginx ---"
    sudo docker compose build nginx || true  # nginx uses a pre-built image, this is a no-op

    echo "--- Starting all containers ---"
    sudo docker compose up -d
REMOTE

# ---- Step 6: Verify ----
echo "[5/5] Verifying deployment..."
sleep 10
echo -n "IdentityAPI: "
ssh -i "$SSH_KEY" "$OCI_HOST" "curl -s http://localhost/health-identity"
echo ""
echo -n "GameServer:  "
ssh -i "$SSH_KEY" "$OCI_HOST" "curl -s http://localhost/health-game"
echo ""

echo ""
echo "=== Deployment Complete ==="
echo "Server IP: 145.241.214.219"
