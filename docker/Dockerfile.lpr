# =========================================
# PkLots LPR (Python) - reconhecimento de placas com modelos ONNX locais
# fast-alpr: detector YOLOv9 + OCR treinado em placas, em CPU, sem PyTorch.
# Modelos (~13 MB) baixados no build: o container não depende de internet.
# Contexto de build: src/python
# =========================================

# =========================================
# Build: dependências em um virtualenv + modelos
# =========================================
FROM docker.io/library/python:3.11-slim AS build

ENV PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    HOME=/opt/pklots

RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

COPY requirements.txt .
RUN pip install -r requirements.txt

# Modelos baixados com curl (com novas tentativas) nas pastas que as bibliotecas usam,
# conferindo o SHA-256 de cada arquivo. Atualizar URL e hash juntos ao trocar de modelo.
ARG URL_DETECTOR=https://github.com/ankandrew/open-image-models/releases/download/assets/yolo-v9-t-384-license-plates-end2end.onnx
ARG URL_OCR=https://github.com/ankandrew/cnn-ocr-lp/releases/download/arg-plates
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates && rm -rf /var/lib/apt/lists/*
RUN set -e; \
    DET=/opt/pklots/.cache/open-image-models/yolo-v9-t-384-license-plate-end2end; \
    OCR=/opt/pklots/.cache/fast-plate-ocr/global-plates-mobile-vit-v2-model; \
    mkdir -p "$DET" "$OCR"; \
    baixar() { curl -fsSL --retry 5 --retry-all-errors --retry-delay 5 -o "$2" "$1"; }; \
    baixar "$URL_DETECTOR" "$DET/yolo-v9-t-384-license-plates-end2end.onnx"; \
    baixar "$URL_OCR/global_mobile_vit_v2_ocr.onnx" "$OCR/global_mobile_vit_v2_ocr.onnx"; \
    baixar "$URL_OCR/global_mobile_vit_v2_ocr_config.yaml" "$OCR/global_mobile_vit_v2_ocr_config.yaml"; \
    printf '%s  %s\n' \
      888397b96d761c89db40bc9c305838e8652660f5e282c2cadebbe8d2951a77a8 "$DET/yolo-v9-t-384-license-plates-end2end.onnx" \
      e2fe5946c02e73901f73d21b3b7223a0c3e3b02059d4964fbf279b01af38d63c "$OCR/global_mobile_vit_v2_ocr.onnx" \
      202b90f2a1f3a0d486b8f1fbd5cdc2f19f4f4881a094f086b983976ed3108aaf "$OCR/global_mobile_vit_v2_ocr_config.yaml" \
      | sha256sum -c -

# Confere que as bibliotecas carregam os modelos do cache, sem tentar baixar de novo
COPY reconhecedor.py placas.py ./
RUN python -c "from reconhecedor import ReconhecedorPlacas; ReconhecedorPlacas()" && du -sh /opt/pklots/.cache

# Remover arquivos que não são usados em execução
RUN find /opt/venv \( -name '__pycache__' -o -name 'tests' -o -name 'test' \) -type d -prune -exec rm -rf {} + \
    && find /opt/venv -name '*.pyc' -delete

# =========================================
# Runtime
# =========================================
FROM docker.io/library/python:3.11-slim AS final

ENV PATH="/opt/venv/bin:$PATH" \
    HOME=/opt/pklots \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    OMP_NUM_THREADS=2

# libglib é exigida pelo OpenCV headless
RUN apt-get update \
    && apt-get install -y --no-install-recommends libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --system --uid 10001 --no-create-home appuser

COPY --from=build /opt/venv /opt/venv
COPY --from=build /opt/pklots/.cache /opt/pklots/.cache

WORKDIR /app
COPY app.py reconhecedor.py placas.py ./

USER appuser
EXPOSE 5000

# Modelos leves: 2 workers com 2 threads cada
CMD ["gunicorn", "--bind", "0.0.0.0:5000", "--workers", "2", "--threads", "2", "--timeout", "60", "app:app"]
