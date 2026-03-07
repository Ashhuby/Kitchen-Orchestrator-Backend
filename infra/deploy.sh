#!/bin/bash
# deploy.sh - Deploys Kitchen Orchestrator to OCI ARM64 instance
# Usage: ./infra/deploy.sh

set -e  # Exit immediately if any command fails

# ---- Configuration ----
OCI_HOST="opc@145.241.222.124"
SSH_KEY="C:/MyFiles/CookedKeys/ssh-key-2026-03-07.key"
REPO_URL="https://github.com/Ashhuby/Kitchen-Orchestrator-Backend.git"
APP_DIR="/home/opc/kitchen-orchestrator"
ENV_FILE=".env"

echo "=== Kitchen Orchestrator Deployment ==="
echo "Target: $OCI_HOST (ARM64)"
echo "======================================="

# ---- Step 1: Verify .env exists locally ----
if [ ! -f "$ENV_FILE" ]; then
    echo "ERROR: .env file not found at repo root. Aborting."
    exit 1
fi

# ---- Step 2: Install Docker and Compose (ARM64) ----
echo "[1/5] Checking Docker & Docker Compose (aarch64)..."
ssh -i "$SSH_KEY" "$OCI_HOST" bash << 'REMOTE'
    # Install Docker Engine if missing
    if ! command -v docker &> /dev/null; then
        echo "Installing Docker..."
        sudo dnf install -y docker
        sudo systemctl start docker
        sudo systemctl enable docker
        sudo usermod -aG docker opc
    fi

    # Install Docker Compose Standalone (aarch64) if missing
    if ! command -v docker-compose &> /dev/null; then
        echo "Installing Docker Compose for ARM64..."
        sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-linux-aarch64" -o /usr/local/bin/docker-compose
        sudo chmod +x /usr/local/bin/docker-compose
        echo "Docker Compose installed."
    fi
REMOTE

# ---- Step 3: Clone or update repo ----
echo "[2/5] Cloning/updating repository..."
# Note: pulling 'develop' as that is your current work branch
ssh -i "$SSH_KEY" "$OCI_HOST" bash << REMOTE
    if [ ! -d "$APP_DIR" ]; then
        git clone $REPO_URL $APP_DIR
        cd $APP_DIR && git checkout develop
    else
        cd $APP_DIR && git pull origin develop
    fi
REMOTE

# ---- Step 4: Copy .env file ----
echo "[3/5] Copying .env file..."
scp -i "$SSH_KEY" "$ENV_FILE" "$OCI_HOST:$APP_DIR/.env"

# ---- Step 5: Build and run containers ----
echo "[4/5] Building and starting containers..."
ssh -i "$SSH_KEY" "$OCI_HOST" bash << REMOTE
    cd $APP_DIR
    # Using the hyphenated version we just installed
    docker-compose build
    docker-compose up -d
REMOTE

# ---- Step 6: Verify ----
echo "[5/5] Verifying deployment..."
sleep 8
# Using localhost on the remote side via SSH for the health check
ssh -i "$SSH_KEY" "$OCI_HOST" "curl -s http://localhost:5000/health" | echo "IdentityAPI: $(cat)"
ssh -i "$SSH_KEY" "$OCI_HOST" "curl -s http://localhost:5001/health" | echo "GameServer: $(cat)"

echo ""
echo "=== Deployment Complete ==="
echo "REMINDER: Ensure OCI Security List allows inbound TCP on ports 5000 and 5001"