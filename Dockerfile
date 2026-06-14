FROM eclipse-temurin:17-jre

WORKDIR /server

ARG FORGE_VERSION=1.20.1-47.4.20

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL "https://maven.minecraftforge.net/net/minecraftforge/forge/${FORGE_VERSION}/forge-${FORGE_VERSION}-installer.jar" -o forge-installer.jar \
    && java -jar forge-installer.jar --installServer \
    && rm -f forge-installer.jar forge-installer.jar.log

COPY server.properties user_jvm_args.txt ./
COPY mods ./mods
COPY config ./config
COPY docker-entrypoint.sh ./docker-entrypoint.sh

RUN chmod +x ./docker-entrypoint.sh

EXPOSE 25565/tcp

ENTRYPOINT ["./docker-entrypoint.sh"]
