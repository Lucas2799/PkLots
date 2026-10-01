"""Reconhecimento de placas com modelos ONNX locais (fast-alpr): detector YOLOv9 + OCR de placas."""
import logging
import os

import cv2
import numpy as np
import onnxruntime
from fast_alpr import ALPR

from placas import escolher_melhor, validar_placa_brasileira

onnxruntime.set_default_logger_severity(3)  # só erros
log = logging.getLogger(__name__)

MODELO_DETECTOR = 'yolo-v9-t-384-license-plate-end2end'
MODELO_OCR = 'global-plates-mobile-vit-v2-model'
METODO = 'fast-alpr'

# Abaixo disso a placa não é aceita (o operador confere/digita). Configurável por variável de ambiente.
CONFIANCA_MINIMA = float(os.getenv('LPR_CONFIANCA_MINIMA', '0.6'))

# Fotos sem orientação EXIF podem chegar giradas: tenta as outras orientações se não achar placa
ROTACOES = (None, cv2.ROTATE_90_CLOCKWISE, cv2.ROTATE_90_COUNTERCLOCKWISE, cv2.ROTATE_180)

# Proporção largura/altura de uma foto que já é só a placa (Mercosul ≈ 3,1; antiga ≈ 3,1)
PROPORCAO_PLACA = (2.0, 6.0)


class ReconhecedorPlacas:
    def __init__(self, confianca_minima=CONFIANCA_MINIMA):
        self._alpr = ALPR(detector_model=MODELO_DETECTOR, ocr_model=MODELO_OCR)
        self._confianca_minima = confianca_minima

    @staticmethod
    def _confianca(resultado_ocr):
        """Média da confiança dos caracteres lidos (o modelo devolve também posições vazias)."""
        if resultado_ocr is None or not resultado_ocr.text:
            return 0.0
        valores = resultado_ocr.confidence
        if isinstance(valores, (int, float)):
            return float(valores)
        valores = list(valores)[: len(resultado_ocr.text)]
        return float(np.mean(valores)) if valores else 0.0

    def _detectar_e_ler(self, imagem_bgr):
        return [(r.ocr.text, self._confianca(r.ocr)) for r in self._alpr.predict(imagem_bgr) if r.ocr is not None]

    @staticmethod
    def _parece_so_a_placa(imagem_bgr):
        altura, largura = imagem_bgr.shape[:2]
        return altura > 0 and PROPORCAO_PLACA[0] <= largura / altura <= PROPORCAO_PLACA[1]

    def reconhecer(self, imagem_bgr):
        """Retorna (placa | None, confiança, método)."""
        candidatos = []

        # 1) Detecta a placa na foto (tentando outras orientações se nada for encontrado) e lê o recorte
        for rotacao in ROTACOES:
            imagem = imagem_bgr if rotacao is None else cv2.rotate(imagem_bgr, rotacao)
            candidatos = self._detectar_e_ler(imagem)
            if any(validar_placa_brasileira(texto) for texto, _ in candidatos):
                break

        # 2) Nenhuma placa detectada e a foto tem formato de placa: lê a imagem inteira
        if not candidatos and self._parece_so_a_placa(imagem_bgr):
            resultado = self._alpr.ocr.predict(imagem_bgr)
            if resultado is not None:
                candidatos.append((resultado.text, self._confianca(resultado)))

        placa, confianca = escolher_melhor(candidatos, self._confianca_minima)
        log.info('candidatos=%s placa=%s confianca=%.3f', candidatos, placa, confianca)
        return placa, confianca, METODO
