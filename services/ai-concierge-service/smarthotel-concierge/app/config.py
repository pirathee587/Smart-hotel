from pydantic_settings import BaseSettings, SettingsConfigDict
from pydantic import Field


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore"
    )

    # Service Info
    SERVICE_NAME: str = "smarthotel-concierge"
    ENVIRONMENT: str = "development"
    PORT: int = 5005

    # JWT & Authentication
    JWT_ISSUER: str = "SmartHotel.Identity"
    JWT_AUDIENCE: str = "SmartHotel.Clients"
    JWT_JWKS_URI: str = "http://identity-service:5001/.well-known/jwks.json"
    JWKS_CACHE_TTL_SECONDS: int = 600

    # Downstream Services
    BOOKING_SERVICE_URL: str = "http://booking-payments-service:5003"
    BOOKING_GRPC_HOST: str = "booking-payments-service"
    BOOKING_GRPC_PORT: int = 5013

    # RabbitMQ Event Bus
    RABBITMQ_HOST: str = "rabbitmq"
    RABBITMQ_PORT: int = 5672
    RABBITMQ_USER: str = "guest"
    RABBITMQ_PASSWORD: str = "guest"
    RABBITMQ_EXCHANGE: str = "smarthotel.events"

    # Rate Limiting
    RATE_LIMIT_PER_MINUTE: int = 20

    # ChromaDB & RAG
    CHROMA_PERSIST_DIR: str = "./chroma_db"
    RAG_TOP_K: int = 3


settings = Settings()
