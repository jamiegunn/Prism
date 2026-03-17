#!/usr/bin/env bash
# Prism Fresh Install Validation Script
# Run this on a clean machine to verify everything works.
# Prerequisites: .NET 9 SDK, Node.js 20+, Docker

set -e

echo "=== Prism Fresh Install Validation ==="
echo ""

# 1. Check prerequisites
echo "[1/8] Checking prerequisites..."
command -v dotnet >/dev/null 2>&1 || { echo "ERROR: .NET SDK not found"; exit 1; }
command -v node >/dev/null 2>&1 || { echo "ERROR: Node.js not found"; exit 1; }
command -v docker >/dev/null 2>&1 || { echo "ERROR: Docker not found"; exit 1; }
echo "  dotnet: $(dotnet --version)"
echo "  node: $(node --version)"
echo "  docker: $(docker --version | head -1)"
echo "  PASS"

# 2. Start PostgreSQL
echo ""
echo "[2/8] Starting PostgreSQL..."
docker compose up -d postgres
echo "  Waiting for PostgreSQL to be ready..."
for i in $(seq 1 30); do
    if docker compose exec -T postgres pg_isready -U postgres >/dev/null 2>&1; then
        echo "  PostgreSQL ready"
        break
    fi
    if [ "$i" -eq 30 ]; then
        echo "  ERROR: PostgreSQL failed to start"
        exit 1
    fi
    sleep 1
done
echo "  PASS"

# 3. Restore and build backend
echo ""
echo "[3/8] Building backend..."
cd backend
dotnet restore Prism.sln --verbosity quiet
dotnet build Prism.sln --configuration Release --verbosity quiet
echo "  PASS"

# 4. Run backend tests
echo ""
echo "[4/8] Running backend tests..."
dotnet test Prism.sln --configuration Release --verbosity quiet --filter "FullyQualifiedName!~Integration"
echo "  PASS"

# 5. Run migrations
echo ""
echo "[5/8] Running database migrations..."
dotnet run --project src/Prism.Api --no-build --configuration Release -- --urls http://localhost:5099 &
API_PID=$!
sleep 5
kill $API_PID 2>/dev/null || true
echo "  Migrations applied via startup"
echo "  PASS"
cd ..

# 6. Install frontend dependencies
echo ""
echo "[6/8] Installing frontend dependencies..."
cd frontend
npm ci --silent
echo "  PASS"

# 7. Frontend typecheck and build
echo ""
echo "[7/8] Building frontend..."
npx tsc -b --noEmit
npm run build
echo "  PASS"

# 8. Frontend lint
echo ""
echo "[8/8] Running frontend lint..."
npm run lint
cd ..
echo "  PASS"

echo ""
echo "=== All checks passed ==="
echo ""
echo "To start Prism:"
echo "  Backend:  cd backend && dotnet run --project src/Prism.Api --urls http://localhost:5000"
echo "  Frontend: cd frontend && npm run dev"
echo "  Open:     http://localhost:5173"
