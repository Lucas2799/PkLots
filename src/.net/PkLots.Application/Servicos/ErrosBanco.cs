using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PkLots.Application.Servicos
{
    // Erros de gravação causados por duas requisições mexendo no mesmo registro ao mesmo tempo
    public static class ErrosBanco
    {
        // 23505: o banco barrou um registro duplicado (índice único), ex.: duas entradas ativas da mesma placa
        public static bool ViolouUnicidade(Exception excecao) =>
            excecao is DbUpdateException { InnerException: DbException { SqlState: "23505" } };

        // Outra requisição gravou antes: registro alterado (xmin) ou duplicado
        public static bool Concorrencia(Exception excecao) => excecao is DbUpdateConcurrencyException || ViolouUnicidade(excecao);
    }
}
