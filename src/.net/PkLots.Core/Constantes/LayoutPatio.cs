namespace PkLots.Core.Constantes
{
    // Disposição das vagas: fileiras A, B, C... com até 10 vagas cada (A1..A10, B1..B10)
    public static class LayoutPatio
    {
        public const int VagasPorFileira = 10;
        public const int MaximoFileiras = 26; // A–Z
        public const int MaximoVagas = VagasPorFileira * MaximoFileiras;

        public static char Fileira(int posicao) => (char)('A' + posicao / VagasPorFileira);

        public static int Indice(int posicao) => posicao % VagasPorFileira + 1;

        public static string Numero(int posicao) => $"{Fileira(posicao)}{Indice(posicao)}";
    }
}
