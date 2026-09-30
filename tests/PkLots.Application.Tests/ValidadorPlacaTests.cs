using PkLots.Application.Servicos;

namespace PkLots.Application.Tests
{
    public class ValidadorPlacaTests
    {
        [Theory]
        [InlineData("ABC1234", "ABC1234")]
        [InlineData("abc-1234", "ABC1234")]
        [InlineData(" ABC 1234 ", "ABC1234")]
        [InlineData("BRA2E19", "BRA2E19")]
        [InlineData("bra-2e19", "BRA2E19")]
        public void Aceita_formatos_brasileiros(string entrada, string esperado) =>
            Assert.Equal(esperado, ValidadorPlaca.Normalizar(entrada));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("AB1234")]
        [InlineData("ABCD123")]
        [InlineData("ABC12345")]
        [InlineData("1BC1234")]
        [InlineData("ABC1D2E")]
        public void Rejeita_formatos_invalidos(string? entrada) =>
            Assert.Null(ValidadorPlaca.Normalizar(entrada));
    }
}
