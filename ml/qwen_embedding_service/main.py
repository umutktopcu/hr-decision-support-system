"""
Qwen Embedding Service
======================
A minimal HTTP inference service that loads Qwen/Qwen3-Embedding-0.6B once
on startup and serves embedding requests over a simple REST API.

Responsibilities:
  - Text → embedding vector (query or document mode)
  - Model loaded once at startup; kept in memory
  - Normalize embeddings before returning
  - Validate output (finite values, correct dimension)

This service does NOT:
  - Connect to databases
  - Query candidates or job requisitions
  - Build semantic documents
  - Calculate cosine similarity
  - Rank candidates
"""

import os
import logging
from contextlib import asynccontextmanager
from typing import Annotated, Literal

import torch
import torch.nn.functional as F
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field
from sentence_transformers import SentenceTransformer

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

MODEL_ID = os.getenv("QWEN_MODEL_ID", "Qwen/Qwen3-Embedding-0.6B")
EMBEDDING_DIMENSION = 1024
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"
BATCH_SIZE = int(os.getenv("QWEN_BATCH_SIZE", "8"))

# Query instruction applied internally for asymmetric retrieval.
# Candidate documents are encoded WITHOUT this instruction.
QUERY_INSTRUCTION = (
    "Given a job description and its requirements, retrieve candidate CVs "
    "that are semantically relevant and suitable for the role."
)

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Model lifecycle
# ---------------------------------------------------------------------------

_model: SentenceTransformer | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _model
    logger.info("Loading model %s on device=%s …", MODEL_ID, DEVICE)
    _model = SentenceTransformer(MODEL_ID, device=DEVICE, trust_remote_code=True)
    _model.eval()
    logger.info(
        "Model loaded. dimension=%d, max_seq_len=%d",
        EMBEDDING_DIMENSION,
        _model.max_seq_length,
    )
    yield
    logger.info("Shutting down — releasing model.")
    _model = None


app = FastAPI(
    title="Qwen Embedding Service",
    version="1.0.0",
    description="Local embedding inference service for HR Decision Support.",
    lifespan=lifespan,
)

# ---------------------------------------------------------------------------
# Request / response models
# ---------------------------------------------------------------------------


class EmbeddingRequest(BaseModel):
    input_type: Annotated[Literal["query", "document"], Field(description="'query' for job documents, 'document' for candidate documents.")]
    texts: Annotated[list[str], Field(min_length=1, description="List of texts to embed.")]


class EmbeddingResponse(BaseModel):
    embeddings: list[list[float]]
    dimension: int
    count: int


class HealthResponse(BaseModel):
    status: str
    model: str
    device: str
    dimension: int


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _validate_vectors(vectors: list[list[float]], expected_dim: int) -> None:
    """Raise ValueError if any vector is malformed."""
    for i, vec in enumerate(vectors):
        if len(vec) != expected_dim:
            raise ValueError(
                f"Vector {i} has dimension {len(vec)}, expected {expected_dim}."
            )
        for j, val in enumerate(vec):
            if not isinstance(val, float) or (val != val) or (val == float("inf")) or (val == float("-inf")):
                raise ValueError(
                    f"Vector {i} contains invalid value at index {j}: {val}"
                )


def _encode(texts: list[str], prompt: str | None = None) -> list[list[float]]:
    """Encode texts and return normalized float lists."""
    assert _model is not None, "Model not loaded"

    with torch.inference_mode():
        if prompt:
            embeddings = _model.encode(
                texts,
                prompt=prompt,
                batch_size=BATCH_SIZE,
                convert_to_tensor=True,
                normalize_embeddings=True,
            )
        else:
            embeddings = _model.encode(
                texts,
                batch_size=BATCH_SIZE,
                convert_to_tensor=True,
                normalize_embeddings=True,
            )

    # Convert to float32 (Qwen outputs bfloat16 on CPU) before converting to Python
    embeddings_f32 = embeddings.float().cpu()
    result = embeddings_f32.tolist()
    return result


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    if _model is None:
        raise HTTPException(status_code=503, detail="Model not loaded.")
    return HealthResponse(
        status="ok",
        model=MODEL_ID,
        device=DEVICE,
        dimension=EMBEDDING_DIMENSION,
    )


@app.post("/embeddings", response_model=EmbeddingResponse)
def embeddings(request: EmbeddingRequest) -> EmbeddingResponse:
    if _model is None:
        raise HTTPException(status_code=503, detail="Model not loaded.")

    if any(not t or not t.strip() for t in request.texts):
        raise HTTPException(status_code=422, detail="All texts must be non-empty.")

    prompt = QUERY_INSTRUCTION if request.input_type == "query" else None

    try:
        vecs = _encode(request.texts, prompt=prompt)
    except Exception as exc:
        logger.exception("Embedding inference failed: %s", exc)
        raise HTTPException(status_code=500, detail=f"Inference error: {exc}") from exc

    try:
        _validate_vectors(vecs, EMBEDDING_DIMENSION)
    except ValueError as exc:
        logger.error("Invalid output vectors: %s", exc)
        raise HTTPException(status_code=500, detail=f"Invalid embedding output: {exc}") from exc

    return EmbeddingResponse(
        embeddings=vecs,
        dimension=EMBEDDING_DIMENSION,
        count=len(vecs),
    )
