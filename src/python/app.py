from flask import Flask, request
import easyocr
import numpy as np
from PIL import Image, UnidentifiedImageError
import re

app = Flask(__name__)
leitor = easyocr.Reader(['pt'], gpu=False)

# Formato antigo (ABC1234) e Mercosul (ABC1D23)
PADRAO_PLACA = re.compile(r'^[A-Z]{3}\d[A-Z0-9]\d{2}$')


def validar_placa_brasileira(texto):
    """Valida formato brasileiro: ABC1234 ou ABC1D23 (Mercosul)"""
    limpo = texto.replace(" ", "").replace("-", "").replace(".", "").upper()
    if PADRAO_PLACA.match(limpo):
        return limpo
    return None


def escolher_placa(resultados):
    """
    Retorna (placa, confianca) do melhor candidato válido.
    O OCR costuma separar "ABC 1234" em dois trechos, então além de cada
    trecho isolado são testadas as junções de trechos vizinhos (esquerda → direita).
    """
    trechos = sorted(resultados, key=lambda r: min(p[0] for p in r[0]))
    candidatos = []

    for inicio in range(len(trechos)):
        for fim in range(inicio + 1, min(inicio + 3, len(trechos)) + 1):
            grupo = trechos[inicio:fim]
            placa = validar_placa_brasileira("".join(r[1] for r in grupo))
            if placa:
                confianca = sum(r[2] for r in grupo) / len(grupo)
                candidatos.append((placa, confianca))

    if not candidatos:
        return None, max(r[2] for r in resultados)
    return max(candidatos, key=lambda c: c[1])


@app.route('/v1/lpr/analisar', methods=['POST'])
def analisar_placa():
    try:
        if 'imagem' not in request.files:
            return {'erro': 'campo imagem obrigatório'}, 400

        arquivo = request.files['imagem']
        try:
            imagem = np.array(Image.open(arquivo.stream).convert('RGB'))
        except UnidentifiedImageError:
            return {'erro': 'arquivo enviado não é uma imagem válida'}, 400

        # OCR
        resultados = leitor.readtext(imagem, detail=1)

        if not resultados:
            return {'sucesso': False, 'placa': None, 'confianca': 0}, 200

        placa, confianca = escolher_placa(resultados)

        return {
            'sucesso': placa is not None,
            'placa': placa,
            'confianca': round(float(confianca), 4),
            'metodo': 'easyocr'
        }, 200

    except Exception as e:
        return {'erro': str(e)}, 500


# Página simples para testar o reconhecimento pelo navegador (desenvolvimento)
PAGINA_TESTE = """<!doctype html>
<html lang="pt-BR">
<head><meta charset="utf-8"><title>PkLots LPR - Teste</title>
<style>
  body { font-family: sans-serif; max-width: 560px; margin: 40px auto; padding: 0 16px; color: #0B2545; }
  h1 span { color: #FFAB00; }
  button { background: #0B2545; color: #fff; border: 0; padding: 10px 18px; border-radius: 6px; cursor: pointer; }
  pre { background: #f4f4f4; padding: 12px; border-radius: 6px; white-space: pre-wrap; }
</style></head>
<body>
  <h1>Pk<span>Lots</span> LPR</h1>
  <p>Envie a foto de uma placa para testar o reconhecimento.</p>
  <form id="f"><input type="file" name="imagem" accept="image/*" required> <button>Analisar</button></form>
  <pre id="r">Aguardando imagem...</pre>
  <script>
    document.getElementById('f').onsubmit = async (e) => {
      e.preventDefault();
      const r = document.getElementById('r');
      r.textContent = 'Analisando...';
      const resposta = await fetch('/v1/lpr/analisar', { method: 'POST', body: new FormData(e.target) });
      r.textContent = resposta.status + '\n' + JSON.stringify(await resposta.json(), null, 2);
    };
  </script>
</body>
</html>"""


@app.route('/', methods=['GET'])
def pagina_teste():
    return PAGINA_TESTE


@app.route('/health', methods=['GET'])
def saude():
    return {'status': 'healthy'}, 200


if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=False)
