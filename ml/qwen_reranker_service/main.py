import os
import math
from typing import List
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field
from sentence_transformers import CrossEncoder

app = FastAPI(title="Qwen3 Reranker Service", version="1.0.0")

# Model configuration
MODEL_NAME = "Qwen/Qwen3-Reranker-0.6B"
# Use CPU by default, allow override via env var
DEVICE = os.environ.get("DEVICE", "cpu")

# Global variables for model
model = None

class CandidateInput(BaseModel):
    candidate_id: str
    candidate_document: str

class RerankRequest(BaseModel):
    job_document: str
    candidates: List[CandidateInput]

class RerankResult(BaseModel):
    candidate_id: str
    raw_score: float
    job_fit_score: float

class RerankResponse(BaseModel):
    results: List[RerankResult]

class HealthResponse(BaseModel):
    status: str
    model: str
    device: str

@app.on_event("startup")
def startup_event():
    """Load the model exactly once when the service starts."""
    global model
    print(f"Loading model {MODEL_NAME} on {DEVICE}...")
    try:
        TASK_INSTRUCTION = "Given a job description and its requirements, determine whether the candidate CV is relevant and suitable for the job."
        model = CrossEncoder(
            MODEL_NAME, 
            device=DEVICE, 
            trust_remote_code=True,
            prompts={"job_fit": TASK_INSTRUCTION},
            default_prompt_name="job_fit"
        )
        print("Model loaded successfully.")
    except Exception as e:
        print(f"Failed to load model: {e}")
        raise

@app.get("/health", response_model=HealthResponse)
def health_check():
    """Report service health."""
    if model is None:
        raise HTTPException(status_code=503, detail="Model is not loaded yet.")
    return HealthResponse(status="ok", model=MODEL_NAME, device=DEVICE)

def _sigmoid(x: float) -> float:
    """Safely calculate sigmoid of x."""
    # Clip x to prevent math domain error or overflow in math.exp
    if x < -100:
        return 0.0
    if x > 100:
        return 1.0
    return 1.0 / (1.0 + math.exp(-x))

@app.post("/rerank", response_model=RerankResponse)
def rerank(request: RerankRequest):
    """
    Reranks candidates against the job document.
    """
    if model is None:
        raise HTTPException(status_code=503, detail="Model is not loaded.")

    if not request.candidates:
        return RerankResponse(results=[])

    # Construct pairs using canonical text unmodified. 
    # The CrossEncoder prompt mechanism handles the instruction internally.
    pairs = [(request.job_document, c.candidate_document) for c in request.candidates]

    try:
        # Perform ONE model inference to get raw scores
        # Qwen CrossEncoder expects a list of (text1, text2) pairs
        raw_scores = model.predict(pairs)
        
        # Ensure raw_scores is an iterable even if a single pair was sent
        if len(request.candidates) == 1:
            raw_scores = [raw_scores]
            
        results = []
        for candidate, raw_score in zip(request.candidates, raw_scores):
            raw_val = float(raw_score)
            
            # Apply sigmoid EXACTLY ONCE here
            job_fit_score = _sigmoid(raw_val)
            
            # Ensure job_fit_score is finite and within [0, 1]
            if math.isnan(job_fit_score) or math.isinf(job_fit_score):
                job_fit_score = 0.0
            job_fit_score = max(0.0, min(1.0, job_fit_score))

            results.append(RerankResult(
                candidate_id=candidate.candidate_id,
                raw_score=raw_val,
                job_fit_score=job_fit_score
            ))
            
        return RerankResponse(results=results)
    
    except Exception as e:
        print(f"Error during reranking: {e}")
        raise HTTPException(status_code=500, detail=str(e))
