# Qwen3 Reranker Service

This is a local Python microservice that hosts the `Qwen/Qwen3-Reranker-0.6B` model for cross-encoder reranking.
It exposes a REST API for evaluating Job and Candidate document pairs.

## Setup

1. Create a virtual environment:
   ```bash
   python -m venv venv
   source venv/bin/activate  # On Windows: venv\Scripts\activate
   ```

2. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```

## Running the Service

Start the service using uvicorn:
```bash
uvicorn main:app --host 127.0.0.1 --port 8766
```

## Endpoints

### `GET /health`
Returns service status, model name, and device.

### `POST /rerank`
Reranks a list of candidate documents against a job document.
