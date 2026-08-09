FROM python:3.11-slim-bookworm

ENV PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PIP_NO_CACHE_DIR=1 \
    PYTHONDONTWRITEBYTECODE=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends libsndfile1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

RUN pip install --index-url https://download.pytorch.org/whl/cpu \
        torch==2.6.0 torchaudio==2.6.0

COPY requirements.txt requirements.lock.txt ./
RUN pip install --requirement requirements.lock.txt \
    && pip install --no-deps --requirement requirements.txt

COPY app ./app
COPY config ./config
COPY tests ./tests

EXPOSE 5081

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "5081"]
