import pytest

from placas import corrigir_posicoes, escolher_melhor, validar_placa_brasileira


@pytest.mark.parametrize('texto, esperado', [
    ('ABC1234', 'ABC1234'),
    ('abc-1234', 'ABC1234'),
    ('EDM5E01', 'EDM5E01'),
    ('BRA 2E19', 'BRA2E19'),
])
def test_aceita_placas_validas(texto, esperado):
    assert validar_placa_brasileira(texto) == esperado


@pytest.mark.parametrize('texto, esperado', [
    ('EDMSE0I', 'EDM5E01'),   # S→5 na posição 4, I→1 na posição 7
    ('EDM5EDI', 'EDM5E01'),   # leitura real do EasyOCR na foto de teste
    ('0BC1234', 'OBC1234'),   # 0→O nas posições de letra
    ('A8C1234', 'ABC1234'),   # 8→B
    ('ABCI234', 'ABC1234'),   # I→1 na posição 4
])
def test_corrige_confusoes_por_posicao(texto, esperado):
    assert validar_placa_brasileira(texto) == esperado


def test_posicao_5_mantem_letra_ou_digito():
    assert corrigir_posicoes('ABC1D23') == 'ABC1D23'
    assert corrigir_posicoes('ABC1234') == 'ABC1234'


@pytest.mark.parametrize('texto', ['', 'BRASIL', 'BR', '4567', 'ABC123', 'ABC12345', 'CAPTIVA'])
def test_rejeita_textos_que_nao_sao_placas(texto):
    assert validar_placa_brasileira(texto) is None


def test_escolhe_o_candidato_valido_mais_confiante():
    candidatos = [('BRASIL', 0.99), ('EDM5E01', 0.97), ('4567', 0.95), ('EDM5E0', 0.98)]
    assert escolher_melhor(candidatos) == ('EDM5E01', 0.97)


def test_sem_candidato_valido_retorna_none_e_maior_confianca():
    assert escolher_melhor([('BRASIL', 0.8), ('4567', 0.6)]) == (None, 0.8)
    assert escolher_melhor([]) == (None, 0.0)


def test_leitura_pouco_confiavel_nao_vira_placa():
    # Lixo que a correção por posição transformaria em "ACA3108": não pode ser aceito
    assert escolher_melhor([('4C4310B', 0.44)], confianca_minima=0.6) == (None, 0.44)
    assert escolher_melhor([('EDM5E01', 0.98)], confianca_minima=0.6) == ('EDM5E01', 0.98)
