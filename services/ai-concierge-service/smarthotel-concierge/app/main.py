import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.config import settings
from app.database import db_manager
from app.rag.vector_store import knowledge_store
from app.clients.rabbitmq_client import rabbitmq_publisher
from app.api.routes import router as concierge_router

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s"
)
logger = logging.getLogger("smarthotel-concierge")


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info(f"Starting {settings.SERVICE_NAME} on port {settings.PORT}...")
    try:
        db_manager.initialize()
    except Exception as e:
        logger.warning(f"Database initialization warning: {e}")

    try:
        knowledge_store.initialize()
    except Exception as e:
        logger.warning(f"ChromaDB initialization deferred or mock: {e}")

    try:
        await rabbitmq_publisher.connect()
    except Exception as e:
        logger.warning(f"RabbitMQ initial connection warning: {e}")

    yield

    logger.info(f"Shutting down {settings.SERVICE_NAME}...")
    try:
        await rabbitmq_publisher.close()
    except Exception as e:
        logger.warning(f"RabbitMQ shutdown cleanup error: {e}")

    try:
        db_manager.close()
    except Exception as e:
        logger.warning(f"Database shutdown cleanup error: {e}")


app = FastAPI(
    title="SmartHotel AI Concierge Service",
    description="Intelligent AI guest concierge with RAG, guest stay context, and operational service request dispatching.",
    version="1.0.0",
    lifespan=lifespan
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(concierge_router)


@app.get("/health", tags=["Health"])
async def root_health():
    return {
        "status": "Healthy",
        "service": settings.SERVICE_NAME,
        "environment": settings.ENVIRONMENT
    }


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=settings.PORT)
