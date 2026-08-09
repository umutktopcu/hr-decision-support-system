# Employee Retention Prediction Model

## Project Overview
Bu model çalışanın işte kalma süresini
Short / Normal / Long olarak tahmin eder.

## Features
- En kısa önceki iş (ay)
- En uzun önceki iş (ay)

## Label Definition
avg_job_stay = total_experience / (company_changes + 1)

<2      -> Short
2-5     -> Normal
>5      -> Long

## Train

python main.py

## Generated Artifacts

models/
    retention_rf_v1.joblib
    retention_rf_v1.metadata.json

results/
    retention_rf_v1.metrics.json

## JSON Examples

examples/

## Notes

Model yalnız retention prediction içindir.

Job Fit modeli değildir.

Probability değerleri "işte kalma olasılığı" değildir.