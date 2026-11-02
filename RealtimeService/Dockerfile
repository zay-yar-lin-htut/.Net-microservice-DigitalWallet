FROM node:22-alpine AS build
WORKDIR /app
RUN corepack enable
COPY RealtimeService/package.json RealtimeService/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY RealtimeService/ ./
RUN pnpm build

FROM node:22-alpine
WORKDIR /app
COPY --from=build /app/package.json /app/pnpm-lock.yaml ./
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/dist ./dist
EXPOSE 8090
CMD ["node", "dist/server.js"]
