using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Criação das vagas de um pátio seguindo a disposição em fileiras (A1..A10, B1..)
    public static class VagasDoPatio
    {
        public static Vaga Nova(Guid patioId, int posicao, DateTime agora) => new()
        {
            PatioId = patioId,
            Numero = LayoutPatio.Numero(posicao),
            Fileira = LayoutPatio.Fileira(posicao),
            Indice = LayoutPatio.Indice(posicao),
            CriadoEm = agora,
            AtualizadoEm = agora
        };

        public static IEnumerable<Vaga> Criar(Guid patioId, int total, DateTime agora) =>
            Enumerable.Range(0, total).Select(posicao => Nova(patioId, posicao, agora));
    }
}
