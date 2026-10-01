import logging

import numpy as np
from flask import Flask, request
from PIL import Image, ImageOps, UnidentifiedImageError

from reconhecedor import ReconhecedorPlacas

logging.basicConfig(level=logging.INFO, format='%(asctime)s %(levelname)s %(name)s: %(message)s')
log = logging.getLogger('pklots.lpr')

app = Flask(__name__)
reconhecedor = ReconhecedorPlacas()


def ler_imagem(arquivo):
    """Abre a imagem enviada, aplica a orientação EXIF (fotos de celular) e converte para BGR."""
    imagem = ImageOps.exif_transpose(Image.open(arquivo.stream)).convert('RGB')
    return np.ascontiguousarray(np.array(imagem)[:, :, ::-1])


@app.route('/v1/lpr/analisar', methods=['POST'])
def analisar_placa():
    if 'imagem' not in request.files:
        return {'erro': 'campo imagem obrigatório'}, 400

    try:
        imagem = ler_imagem(request.files['imagem'])
    except UnidentifiedImageError:
        return {'erro': 'arquivo enviado não é uma imagem válida'}, 400

    try:
        placa, confianca, metodo = reconhecedor.reconhecer(imagem)
    except Exception:
        log.exception('falha ao analisar imagem')
        return {'erro': 'falha ao analisar a imagem'}, 500

    return {
        'sucesso': placa is not None,
        'placa': placa,
        'confianca': round(float(confianca), 4),
        'metodo': metodo
    }, 200


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
      r.textContent = resposta.status + '\\n' + JSON.stringify(await resposta.json(), null, 2);
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
