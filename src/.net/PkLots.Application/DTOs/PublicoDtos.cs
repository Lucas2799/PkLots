namespace PkLots.Application.DTOs
{
    // Página aberta pelo QR do ticket: só os dados daquela estadia, sem login
    public record EstadiaPublicaDto(
        string NomePatio,
        string Placa,
        string? NumeroVaga,
        string Codigo,
        DateTime DataHoraEntrada,
        string Situacao,           // ativa, concluida, cancelada
        string Duracao,
        decimal Valor,             // ativa: valor se saísse agora; concluída: valor pago
        decimal ValorHora,
        int MinutosTolerancia,
        bool CobertoPorMensalidade,
        DateTime? DataHoraSaida,
        string? FormaPagamento,
        DateTime ConsultadoEm);

    // Convite de mensalista (antes de aceitar): placas mascaradas para a pessoa conferir se é ela
    public record ConviteMensalistaDto(
        string NomeMensalista,
        string NomePatio,
        string Email,
        IReadOnlyList<string> PlacasMascaradas,
        bool ContaExistente);

    public record AceitarConviteRequisicao(string? NomeCompleto, string? Senha);

    // Conta criada: já entra no app (Token). Conta existente: o contrato aparece no próximo login.
    public record AceiteConviteDto(bool ContaCriada, TokenDto? Token);

    public record EsqueciSenhaRequisicao(string? Email);

    // Convite de admin/operador: o que a pessoa vê antes de criar a senha
    public record ConviteUsuarioDto(string NomeCompleto, string Email, string NomeEmpresa, string Perfil, IReadOnlyList<string> Patios);

    public record AtivarContaRequisicao(string? Token, string? Senha);

    public record RedefinirSenhaPorEmailRequisicao(string? Token, string? NovaSenha);
}
