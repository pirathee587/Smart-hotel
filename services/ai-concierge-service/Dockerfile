FROM python:3.11-slim

WORKDIR /app

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    ENVIRONMENT=production \
    PORT=5005

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

COPY smarthotel-concierge/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY smarthotel-concierge/ .

EXPOSE 5005

HEALTHCHECK --interval=10s --timeout=5s --retries=5 --start-period=10s \
    CMD curl -f http://localhost:5005/health || exit 1

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "5005"]
