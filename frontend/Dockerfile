# Prism frontend — Vite dev server in a container.
#
# Build context is ./frontend. This runs the same `npm run dev` the metal path
# runs, so hot-reload and behaviour match; it is a development image, not a
# production static build.

FROM node:22-alpine AS dev
WORKDIR /app

# Install dependencies against the lockfile first for layer caching. `npm ci`
# needs both files and fails loudly if they disagree, which is what we want.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Vite must listen on all interfaces to be reachable from the host, and its
# proxy target is the API — inside the compose network that is the api service,
# passed in as PRISM_API_URL by docker-compose.yml.
ENV PRISM_API_URL=http://api:5000
EXPOSE 5173

CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
