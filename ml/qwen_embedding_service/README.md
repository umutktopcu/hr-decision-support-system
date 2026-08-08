# Qwen Embedding Service

Local Python inference service for the HR Decision Support System.
Serves `Qwen/Qwen3-Embedding-0.6B` embeddings over HTTP.

## Architecture

```
.NET Application
       ↓ HTTP (POST /embeddings)
Qwen Embedding Service (this service)
       ↓
Qwen/Qwen3-Embedding-0.6B (sentence-transformers / HuggingFace)
```

## Responsibilities

- Accept text → return normalized embedding vectors (1024-dim)
- Support two input modes: `query` (job document) and `document` (candidate documents)
- Apply Qwen retrieval query instruction **only** for `input_type=query`
- Load the model **once** at startup; keep in memory
- Validate output: correct dimension, no NaN/Infinity

## Non-responsibilities

This service does **not**:
- Connect to PostgreSQL
- Query candidates or job requisitions
- Build semantic documents
- Calculate cosine similarity
- Rank candidates

## Endpoints

### `GET /health`

Returns service status, model name, device, and embedding dimension.

### `POST /embeddings`

**Request:**
```json
{
  "input_type": "query",
  "texts": ["Job document text ..."]
}
```

```json
{
  "input_type": "document",
  "texts": ["Candidate A text ...", "Candidate B text ..."]
}
```

**Response:**
```json
{
  "embeddings": [[0.123, -0.456, ...]],
  "dimension": 1024,
  "count": 1
}
```

## Setup

```bash
python -m venv .venv
.venv\Scripts\activate       # Windows
pip install -r requirements.txt
```

## Running

```bash
uvicorn main:app --host 127.0.0.1 --port 8765
```

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `QWEN_MODEL_ID` | `Qwen/Qwen3-Embedding-0.6B` | HuggingFace model ID |
| `QWEN_BATCH_SIZE` | `8` | Batch size for CPU inference |

## Notes

- Model weights are cached in the HuggingFace Hub cache (`~/.cache/huggingface`).
  Do **not** commit model weights to the repository.
- First startup downloads the model (~1.2 GB). Subsequent startups use the local cache.
- On CPU, encoding 35 candidates takes approximately 17 seconds.
