FROM node:22-alpine AS build
WORKDIR /app
RUN corepack enable
COPY LedgerService/package.json LedgerService/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY LedgerService/ ./
RUN pnpm build
FROM node:22-alpine
WORKDIR /app
COPY --from=build /app/package.json /app/pnpm-lock.yaml ./
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/dist ./dist
EXPOSE 8060
CMD ["node","dist/server.js"]
