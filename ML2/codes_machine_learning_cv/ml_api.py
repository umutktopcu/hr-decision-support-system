from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
import joblib
import pandas as pd

app = FastAPI()

# ── YENİ EKLENEN CORS İZİN KISMI ──
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], # Güvenlik için canlıya alırken buraya C# uygulamasının adresi yazılır
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)
# ──────────────────────────────────

# Eğitilmiş modeli belleğe al
bundle = joblib.load("models/retention_rf_v1.joblib")
model = bundle["model"]
feature_cols = bundle["feature_cols"]
scaler = bundle["scaler"]

# C#'tan gelecek veri formatı
class PredictRequest(BaseModel):
    shortest_job_months: float
    longest_job_months: float

@app.post("/predict")
def predict(req: PredictRequest):
    # Gelen veriyi modelin anlayacağı tabloya çevir
    df = pd.DataFrame([{
        "En kısa önceki iş (ay)": req.shortest_job_months,
        "En uzun önceki iş (ay)": req.longest_job_months
    }])
    
    X = df[feature_cols]
    if scaler:
        X = scaler.transform(X)
        
    # Tahmin yap (0: Kısa, 1: Normal, 2: Uzun)
    pred = model.predict(X)[0]
    return {"label": int(pred)}