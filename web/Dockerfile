# web/Dockerfile
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . ./
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.d/40-write-app-config.sh /docker-entrypoint.d/40-write-app-config.sh
COPY --from=build /app/dist /usr/share/nginx/html
RUN chmod +x /docker-entrypoint.d/40-write-app-config.sh
ENV API_BASE=/api
ENV AUTH_MODE=demo
ENV ENTRA_CLIENT_ID=
ENV ENTRA_TENANT_ID=
ENV ENTRA_AUDIENCE=
ENV ENTRA_REDIRECT_URI=
EXPOSE 80
