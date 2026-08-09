from train_model import run_pipeline
from train_model import MODEL_NAME

TRAIN_FILES = [
    "grup_11_c_alis_an_bilgileri_merged.xlsx"
]

TEST_FILE = "test_set.xlsx"

run_pipeline(
    TRAIN_FILES,
    TEST_FILE,
    model_name=MODEL_NAME
)