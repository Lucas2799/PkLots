"""Regras de placas brasileiras (sem dependência dos modelos de IA)."""
import re

# Formato antigo (ABC1234) e Mercosul (ABC1D23)
PADRAO_PLACA = re.compile(r'^[A-Z]{3}\d[A-Z0-9]\d{2}$')

# Confusões comuns do OCR, corrigidas conforme a posição do caractere na placa
DIGITO_PARA_LETRA = {'0': 'O', '1': 'I', '2': 'Z', '4': 'A', '5': 'S', '6': 'G', '7': 'T', '8': 'B'}
LETRA_PARA_DIGITO = {'O': '0', 'Q': '0', 'D': '0', 'U': '0', 'I': '1', 'L': '1', 'J': '1',
                     'Z': '2', 'A': '4', 'S': '5', 'G': '6', 'T': '7', 'B': '8'}

POSICOES_LETRA = (0, 1, 2)
POSICOES_DIGITO = (3, 5, 6)  # a posição 4 aceita letra (Mercosul) ou dígito (antiga)


def limpar(texto):
    """Mantém só letras e números, em maiúsculas: 'abc-1234' → 'ABC1234'."""
    return re.sub(r'[^A-Z0-9]', '', (texto or '').upper())


def corrigir_posicoes(texto):
    """Troca letra/dígito conforme a posição esperada (ex.: 'EDMSE0I' → 'EDM5E01')."""
    if len(texto) != 7:
        return texto
    caracteres = list(texto)
    for i in POSICOES_LETRA:
        caracteres[i] = DIGITO_PARA_LETRA.get(caracteres[i], caracteres[i])
    for i in POSICOES_DIGITO:
        caracteres[i] = LETRA_PARA_DIGITO.get(caracteres[i], caracteres[i])
    return ''.join(caracteres)


def validar_placa_brasileira(texto):
    """Retorna a placa normalizada (ABC1234 ou ABC1D23) ou None se não for uma placa válida."""
    limpo = corrigir_posicoes(limpar(texto))
    return limpo if PADRAO_PLACA.match(limpo) else None


def escolher_melhor(candidatos, confianca_minima=0.0):
    """
    Entre (texto, confiança), retorna (placa, confiança) do melhor candidato válido.
    Abaixo da confiança mínima não arrisca: devolve (None, confiança) para o operador conferir,
    evitando registrar a placa errada a partir de uma leitura ruim.
    """
    validos = [(placa, confianca) for texto, confianca in candidatos
               if (placa := validar_placa_brasileira(texto)) is not None]
    if validos:
        placa, confianca = max(validos, key=lambda c: c[1])
        return (placa, confianca) if confianca >= confianca_minima else (None, confianca)
    return None, max((c for _, c in candidatos), default=0.0)
