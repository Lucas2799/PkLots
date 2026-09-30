# =========================================
# PkLots LPR (Python + EasyOCR) - imagem enxuta
# PyTorch somente CPU, OpenCV headless, modelos baixados no build.
# Contexto de build: src/python
# =========================================

# =========================================
# Build: dependências em um virtualenv
# =========================================
FROM docker.io/library/python:3.11-slim AS build

ENV PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# PyTorch CPU (evita os vários GB das bibliotecas CUDA)
RUN pip install torch==2.4.1 torchvision==0.19.1 --index-url https://download.pytorch.org/whl/cpu

COPY requirements.txt .
RUN pip install -r requirements.txt

# Baixar modelos do EasyOCR (detecção + reconhecimento latino) durante o build
ENV EASYOCR_MODULE_PATH=/opt/easyocr
RUN python -c "import easyocr; easyocr.Reader(['pt'], gpu=False)" \
    && rm -f /opt/easyocr/model/*.zip

# Remover arquivos que não são usados em execução
RUN find /opt/venv \( -name '__pycache__' -o -name 'tests' -o -name 'test' \) -type d -prune -exec rm -rf {} + \
    && find /opt/venv -name '*.pyc' -delete

# =========================================
# Runtime
# =========================================
FROM docker.io/library/python:3.11-slim AS final

ENV PATH="/opt/venv/bin:$PATH" \
    EASYOCR_MODULE_PATH=/opt/easyocr \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    OMP_NUM_THREADS=2

# libglib é exigida pelo OpenCV headless
RUN apt-get update \
    && apt-get install -y --no-install-recommends libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --system --uid 10001 --no-create-home appuser

COPY --from=build /opt/venv /opt/venv
COPY --from=build /opt/easyocr /opt/easyocr

WORKDIR /app
COPY app.py .

USER appuser
EXPOSE 5000

# 1 worker (o modelo ocupa memória por processo) com 2 threads
CMD ["gunicorn", "--bind", "0.0.0.0:5000", "--workers", "1", "--threads", "2", "--timeout", "120", "app:app"]
