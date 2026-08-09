"""
train_model.py
================
Simple, swappable scikit-learn training pipeline for the synthetic
"Kalış etiketi" (job-stay label) dataset.

Uses ONLY the numerical columns for now. The text columns (Teknoloji ve
araçlar, Proje deneyimleri, Teknik beceriler, ...) are intentionally left
out here -- those are for a later BERT-embedding step (not implemented
in this script, per request).

Features:
  - Load train / test data straight from the .xlsx files
  - Pick any scikit-learn classifier from a small registry and train it
  - Save / load trained models with joblib
  - Compute accuracy, precision, recall, f1 (weighted + per-class)
  - Visualize confusion matrix + metric bar chart, saved as PNGs

IMPORTANT NOTE ON LABEL LEAKAGE
--------------------------------
The label was generated with a fixed rule:

    avg_job_stay = Toplam deneyim / (Toplam şirket değişimi sayısı + 1)
    label = 0 if avg_job_stay < 2   (short)
            1 if 2 <= avg_job_stay <= 5   (normal)
            2 if avg_job_stay > 5   (long)

That means if you feed the model BOTH "Toplam deneyim" and
"Toplam şirket değişimi sayısı" (and/or the derived
"İş değiştirme sayısı / toplam deneyim" column, which already IS
num_changes / total_exp), it can reconstruct the label almost perfectly
and you'll see near-100% accuracy -- that's not a bug, it's just because
those two columns deterministically define the label.

If you want a more realistic/challenging benchmark (closer to what will
happen with real, non-rule-labeled CVs), edit LEAK_COLUMNS below to also
drop 'Toplam deneyim' and 'Toplam şirket değişimi sayısı', or engineer
different features. Left in by default so you can sanity-check the
pipeline end-to-end first.
"""

import os
import json
import joblib
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
from constants import MODEL_FILE
from datetime import datetime
import sklearn

from sklearn.preprocessing import StandardScaler
from sklearn.metrics import (
    accuracy_score, precision_score, recall_score, f1_score,
    confusion_matrix, classification_report
)

from sklearn.linear_model import LogisticRegression
from sklearn.ensemble import RandomForestClassifier, GradientBoostingClassifier
from sklearn.svm import SVC
from sklearn.neighbors import KNeighborsClassifier
from sklearn.tree import DecisionTreeClassifier
from sklearn.naive_bayes import GaussianNB


# ----------------------------------------------------------------------
# 1. CONFIG
# ----------------------------------------------------------------------

LABEL_COLUMN = "Kalış etiketi (0: Kısa, 1: Normal, 2: Uzun)"
LABEL_NAMES = {0: "Kısa (short)", 1: "Normal", 2: "Uzun (long)"}

# The purely numerical columns available in the dataset.
NUMERIC_FEATURE_COLUMNS = [
    "En kısa önceki iş (ay)",
    "En uzun önceki iş (ay)",
]

# Columns that directly define the label (see docstring above).
# Set DROP_LEAK_COLUMNS = True to exclude them from training for a more
# realistic (harder) prediction task.
DROP_LEAK_COLUMNS = False
LEAK_COLUMNS = ["Toplam deneyim", "Toplam şirket değişimi sayısı",
                "İş değiştirme sayısı / toplam deneyim"]

# Registry of interchangeable scikit-learn models.
# All of them expose .fit(X, y) / .predict(X) / .predict_proba(X),
# so swapping between them only means changing MODEL_NAME below.
MODEL_REGISTRY = {
    "logistic_regression": LogisticRegression(max_iter=1000, random_state=42),
    "random_forest": RandomForestClassifier(n_estimators=300, random_state=42),
    "gradient_boosting": GradientBoostingClassifier(random_state=42),
    "svm": SVC(kernel="rbf", probability=True, random_state=42),
    "knn": KNeighborsClassifier(n_neighbors=15),
    "decision_tree": DecisionTreeClassifier(max_depth=8, random_state=42),
    "naive_bayes": GaussianNB(),
}

MODEL_NAME = "random_forest"          # <-- change this to switch models
MODEL_DIR = "models"
RESULTS_DIR = "results"


# ----------------------------------------------------------------------
# 2. DATA LOADING
# ----------------------------------------------------------------------

def load_dataset(paths):
    """Load one or several .xlsx files and concatenate them into one DataFrame."""
    if isinstance(paths, str):
        paths = [paths]
    frames = [pd.read_excel(p, sheet_name="Sayfa1") for p in paths]
    df = pd.concat(frames, ignore_index=True)
    return df


def get_feature_columns():
    cols = NUMERIC_FEATURE_COLUMNS.copy()
    if DROP_LEAK_COLUMNS:
        cols = [c for c in cols if c not in LEAK_COLUMNS]
    return cols


def split_features_labels(df):
    feature_cols = get_feature_columns()
    X = df[feature_cols].copy()
    y = df[LABEL_COLUMN].copy()
    return X, y, feature_cols


# ----------------------------------------------------------------------
# 3. TRAIN / SAVE / LOAD
# ----------------------------------------------------------------------

def train_model(model_name, X_train, y_train, scaler=None):
    if model_name not in MODEL_REGISTRY:
        raise ValueError(f"Unknown model '{model_name}'. Options: {list(MODEL_REGISTRY)}")

    model = MODEL_REGISTRY[model_name]

    if scaler is not None:
        X_train = scaler.transform(X_train)

    model.fit(X_train, y_train)
    return model


def save_model(model, scaler, feature_cols, model_name, out_dir=MODEL_DIR):
    os.makedirs(out_dir, exist_ok=True)

    bundle = {
        "model": model,
        "scaler": scaler,
        "feature_cols": feature_cols,
        "model_name": model_name,
    }

    # Modeli kaydet
    model_path = os.path.join(
    out_dir,
    MODEL_FILE
    )
    joblib.dump(bundle, model_path)

    # Metadata oluştur
    metadata = {
    "modelVersion": "retention-rf-v1",

    "featureSchemaVersion": "retention-features-v1",

    "algorithm": model_name,

    "features": feature_cols,

    "classMapping": {
        "0": "Short",
        "1": "Normal",
        "2": "Long"
    },

    "trainedAtUtc": datetime.utcnow().isoformat() + "Z",

    "sklearnVersion": sklearn.__version__,

    "modelFileName": MODEL_FILE
}

    metadata_path = os.path.join(
    out_dir,
    MODEL_FILE.replace(".joblib", ".metadata.json")
    )

    with open(metadata_path, "w", encoding="utf-8") as f:
        json.dump(metadata, f, ensure_ascii=False, indent=4)

    print(f"Model saved to: {model_path}")
    print(f"Metadata saved to: {metadata_path}")

    return model_path


def load_model(path):

    metadata_path = path.replace(".joblib", ".metadata.json")

    if not os.path.exists(metadata_path):
        raise FileNotFoundError("Metadata file not found.")

    with open(metadata_path, "r", encoding="utf-8") as f:
        metadata = json.load(f)

    if metadata["modelVersion"] != "retention-rf-v1":
        raise ValueError("Unsupported model version.")

    if metadata["featureSchemaVersion"] != "retention-features-v1":
        raise ValueError("Unsupported feature schema.")

    if metadata["features"] != NUMERIC_FEATURE_COLUMNS:
        raise ValueError("Feature schema mismatch.")

    bundle = joblib.load(path)

    print(f"Loaded model: {metadata['modelVersion']}")

    return bundle


# ----------------------------------------------------------------------
# 4. EVALUATION
# ----------------------------------------------------------------------

def evaluate_model(model, X_test, y_test, scaler=None):
    if scaler is not None:
        X_test = scaler.transform(X_test)

    y_pred = model.predict(X_test)

    metrics = {
        "accuracy": accuracy_score(y_test, y_pred),
        "precision_weighted": precision_score(y_test, y_pred, average="weighted", zero_division=0),
        "recall_weighted": recall_score(y_test, y_pred, average="weighted", zero_division=0),
        "f1_weighted": f1_score(y_test, y_pred, average="weighted", zero_division=0),
        "precision_macro": precision_score(y_test, y_pred, average="macro", zero_division=0),
        "recall_macro": recall_score(y_test, y_pred, average="macro", zero_division=0),
        "f1_macro": f1_score(y_test, y_pred, average="macro", zero_division=0),
    }

    report = classification_report(
        y_test, y_pred,
        target_names=[LABEL_NAMES[k] for k in sorted(LABEL_NAMES)],
        zero_division=0
    )
    cm = confusion_matrix(y_test, y_pred, labels=sorted(LABEL_NAMES))

    return metrics, report, cm, y_pred


# ----------------------------------------------------------------------
# 5. VISUALIZATION
# ----------------------------------------------------------------------

def plot_confusion_matrix(cm, model_name, out_dir=RESULTS_DIR):
    os.makedirs(out_dir, exist_ok=True)
    labels = [LABEL_NAMES[k] for k in sorted(LABEL_NAMES)]

    fig, ax = plt.subplots(figsize=(5.5, 5))
    im = ax.imshow(cm, cmap="Blues")
    ax.set_xticks(range(len(labels)))
    ax.set_yticks(range(len(labels)))
    ax.set_xticklabels(labels, rotation=30, ha="right")
    ax.set_yticklabels(labels)
    ax.set_xlabel("Tahmin edilen (Predicted)")
    ax.set_ylabel("Gerçek (True)")
    ax.set_title(f"Confusion Matrix - {model_name}")

    thresh = cm.max() / 2.0
    for i in range(cm.shape[0]):
        for j in range(cm.shape[1]):
            ax.text(j, i, format(cm[i, j], "d"),
                     ha="center", va="center",
                     color="white" if cm[i, j] > thresh else "black")

    fig.colorbar(im, ax=ax, fraction=0.046, pad=0.04)
    fig.tight_layout()
    path = os.path.join(out_dir, f"confusion_matrix_{model_name}.png")
    fig.savefig(path, dpi=150)
    plt.close(fig)
    print(f"Confusion matrix plot saved to: {path}")
    return path


def plot_metrics_bar(metrics, model_name, out_dir=RESULTS_DIR):
    os.makedirs(out_dir, exist_ok=True)
    keys = ["accuracy", "precision_weighted", "recall_weighted", "f1_weighted"]
    values = [metrics[k] for k in keys]
    labels = ["Accuracy", "Precision\n(weighted)", "Recall\n(weighted)", "F1\n(weighted)"]

    fig, ax = plt.subplots(figsize=(6, 4.5))
    bars = ax.bar(labels, values, color=["#4C72B0", "#55A868", "#C44E52", "#8172B2"])
    ax.set_ylim(0, 1.05)
    ax.set_ylabel("Score")
    ax.set_title(f"Model Performance - {model_name}")
    for b, v in zip(bars, values):
        ax.text(b.get_x() + b.get_width() / 2, v + 0.02, f"{v:.3f}", ha="center")

    fig.tight_layout()
    path = os.path.join(out_dir, f"metrics_{model_name}.png")
    fig.savefig(path, dpi=150)
    plt.close(fig)
    print(f"Metrics bar chart saved to: {path}")
    return path


# ----------------------------------------------------------------------
# 6. MAIN PIPELINE
# ----------------------------------------------------------------------

def run_pipeline(train_paths, test_path, model_name=MODEL_NAME, use_scaler=True):
    print(f"\n=== Training model: {model_name} ===")

    train_df = load_dataset(train_paths)
    test_df = load_dataset(test_path)

    X_train, y_train, feature_cols = split_features_labels(train_df)
    X_test, y_test, _ = split_features_labels(test_df)

    print(f"Train rows: {len(X_train)} | Test rows: {len(X_test)}")
    print(f"Features used ({len(feature_cols)}): {feature_cols}")

    scaler = None
    if use_scaler:
        scaler = StandardScaler()
        scaler.fit(X_train)

    model = train_model(model_name, X_train, y_train, scaler=scaler)

    metrics, report, cm, y_pred = evaluate_model(model, X_test, y_test, scaler=scaler)

    print("\n--- Metrics ---")
    for k, v in metrics.items():
        print(f"{k}: {v:.4f}")

    print("\n--- Classification report ---")
    print(report)

    save_model(model, scaler, feature_cols, model_name)

    plot_confusion_matrix(cm, model_name)
    plot_metrics_bar(metrics, model_name)

    os.makedirs(RESULTS_DIR, exist_ok=True)
    
    results = {
    "modelVersion": "retention-rf-v1",

    "accuracy": metrics["accuracy"],

    "weightedF1": metrics["f1_weighted"],

    "classificationReport": report,

    "confusionMatrix": cm.tolist(),

    "featureList": feature_cols,

    "testRowCount": len(X_test)
}
    
    results_path = os.path.join(
    RESULTS_DIR,
    "retention_rf_v1.metrics.json"
)
    with open(results_path, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=4)
    print(f"Metrics saved to: {results_path}")
    return model, scaler, metrics


def predict_with_saved_model(model_path, df_new):
    """Load a saved model bundle and predict labels for new (unlabeled) rows."""
    bundle = load_model(model_path)
    model, scaler, feature_cols = bundle["model"], bundle["scaler"], bundle["feature_cols"]

    X_new = df_new[feature_cols].copy()
    if scaler is not None:
        X_new = scaler.transform(X_new)

    preds = model.predict(X_new)
    return preds

def predict_one(model, scaler, shortest, longest):

    if shortest is None or longest is None:
        raise ValueError("Missing feature")

    if shortest < 0 or longest < 0:
        raise ValueError("Negative values are not allowed")

    if shortest > longest:
        raise ValueError(
            "Shortest cannot exceed longest."
        )

    X = [[shortest, longest]]

    if scaler is not None:
        X = scaler.transform(X)

    pred = model.predict(X)[0]

    result = {
        "predictedClass": int(pred),
        "className": LABEL_NAMES[pred]
    }

    if hasattr(model, "predict_proba"):
        probs = model.predict_proba(X)[0]

        result["probabilities"] = {
            "Short": float(probs[0]),
            "Normal": float(probs[1]),
            "Long": float(probs[2]),
        }

    return result
    
def predict_batch(model, scaler, records):

    results = []

    for r in records:

        try:

            prediction = predict_one(
                model,
                scaler,
                r["shortestPreviousJobMonths"],
                r["longestPreviousJobMonths"]
            )

            results.append({
                "referenceId": r.get("referenceId"),
                "success": True,
                "prediction": prediction
            })

        except Exception as e:

            results.append({
                "referenceId": r.get("referenceId"),
                "success": False,
                "message": str(e)
            })

    return results

if __name__ == "__main__":
    # ---- EDIT THESE PATHS TO MATCH YOUR FILES ----
    # TRAIN_FILES = [
    #     "grup_11_c_alıs_an_bilgileri.xlsx",                      # EMP0001-EMP0800
    #     "grup_11_c_alıs_an_bilgileri_batch2_EMP0801-EMP1300.xlsx",
    #     "grup_11_c_alıs_an_bilgileri_batch3_EMP1301-EMP1800.xlsx",
    #     "grup_11_c_alıs_an_bilgileri_batch4_EMP1801-EMP6800.xlsx",
    # ]
    TRAIN_FILES = ["grup_11_c_alis_an_bilgileri_merged.xlsx"]
    TEST_FILE = "test_set.xlsx"

    # Train + evaluate the model chosen in MODEL_NAME above.
    run_pipeline(TRAIN_FILES, TEST_FILE, model_name=MODEL_NAME)

    # To try a different model, just do e.g.:
    # run_pipeline(TRAIN_FILES, TEST_FILE, model_name="logistic_regression")
    # run_pipeline(TRAIN_FILES, TEST_FILE, model_name="gradient_boosting")
