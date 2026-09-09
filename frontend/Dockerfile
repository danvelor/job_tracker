# syntax=docker/dockerfile:1

FROM node:22-alpine AS deps
WORKDIR /app
# npm ci, not npm install: it installs exactly the lockfile and fails if the
# two disagree, which is the behaviour an image build wants.
COPY package.json package-lock.json ./
RUN npm ci

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

# JOBTRACKER_API_URL is deliberately absent at build time. The container reads
# it from the environment, so one image serves both the in-memory demo and the
# real stack — baking it in would make the image environment-specific for no
# gain (D-01).
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

RUN addgroup --system --gid 1001 nodejs \
    && adduser --system --uid 1001 --ingroup nodejs nextjs
USER nextjs

# The three pieces standalone output produces: the server and its traced
# dependencies, the static assets it does not serve itself, and public/.
COPY --from=build --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=build --chown=nextjs:nodejs /app/public ./public

# Not decoration: the standalone server binds localhost by default, and a
# container that binds localhost is a container nothing outside it can reach.
ENV HOSTNAME=0.0.0.0
ENV PORT=3000
EXPOSE 3000

CMD ["node", "server.js"]
