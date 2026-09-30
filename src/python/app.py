from flask import Flask, request
import easyocr
import numpy as np
from PIL import Image
import re

app = Flask(__name__)
leitor = easyocr.Reader(['pt'])

# Formato antigo (ABC1234) e Mercosul (ABC1D23)
PADRAO_PLACA = re.compile(r'^[A-Z]{3}\d[A-Z0-9]\d{2}$')


def validar_placa_brasileira(texto):
    """Valida formato brasileiro: ABC1234 ou ABC1D23 (Mercosul)"""
    limpo = texto.replace(" ", "").replace("-", "").upper()
    if PADRAO_PLACA.match(limpo):
        return limpo
    return None


@app.route('/v1/lpr/analisar', methods=['POST'])
def analisar_placa():
    try:
        if 'imagem' not in request.files:
            return {'erro': 'campo imagem obrigatório'}, 400

        arquivo = request.files['imagem']
        imagem = np.array(Image.open(arquivo.stream).convert('RGB'))

        # OCR
        resultados = leitor.readtext(imagem, detail=1)

        if not resultados:
            return {'sucesso': False, 'placa': None, 'confianca': 0}, 200

        melhor = max(resultados, key=lambda r: r[2])
        texto = melhor[1]
        confianca = melhor[2]

        placa = validar_placa_brasileira(texto)

        return {
            'sucesso': placa is not None,
            'placa': placa,
            'confianca': float(confianca),
            'metodo': 'easyocr'
        }, 200

    except Exception as e:
        return {'erro': str(e)}, 500


@app.route('/health', methods=['GET'])
def saude():
    return {'status': 'healthy'}, 200


if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=False)
