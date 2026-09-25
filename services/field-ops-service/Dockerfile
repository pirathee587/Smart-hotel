# Build Stage
FROM maven:3.9.6-eclipse-temurin-21-alpine AS build
WORKDIR /app

# Cache dependencies
COPY smarthotel-fieldops/pom.xml .
RUN mvn dependency:go-offline -B

# Build application jar
COPY smarthotel-fieldops/src ./src
RUN mvn clean package -DskipTests -B

# Runtime Stage
FROM eclipse-temurin:21-jre-alpine
WORKDIR /app

# Create non-root user
RUN addgroup -S smarthotel && adduser -S smarthotel -G smarthotel

COPY --from=build /app/target/smarthotel-fieldops-*.jar app.jar
RUN chown -R smarthotel:smarthotel /app

USER smarthotel

EXPOSE 8084

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD wget -qO- http://localhost:8084/actuator/health || exit 1

ENTRYPOINT ["java", "-jar", "app.jar"]
