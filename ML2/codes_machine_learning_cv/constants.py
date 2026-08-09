FEATURES = [
    "En kısa önceki iş (ay)",
    "En uzun önceki iş (ay)"
]

FEATURE_NAMES = [
    "shortest_previous_job_months",
    "longest_previous_job_months"
]

TARGET = "Kalış etiketi (0: Kısa, 1: Normal, 2: Uzun)"

CLASS_NAMES = {
    0: "Short",
    1: "Normal",
    2: "Long"
}

MODEL_FILE = "retention_rf_v1.joblib"
