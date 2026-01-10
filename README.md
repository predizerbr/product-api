# PredictX back

API em .NET 8 para o back-end do PredictX. Inclui autenticacao com ASP.NET Core Identity (cookies e bearer tokens), EF Core com PostgreSQL e Swagger para exploracao dos endpoints.

## Requisitos

- .NET SDK 8.0+
- PostgreSQL 14+ rodando localmente
- Ferramentas do EF Core CLI (`dotnet tool install --global dotnet-ef` se ainda nao tiver)

## Configuracao rapida

1. Crie/ajuste a string de conexao em `src/Pruduct.Api/appsettings.Development.json` (chave `ConnectionStrings:DefaultConnection`) ou defina a variavel de ambiente `ConnectionStrings__DefaultConnection`.
2. Ajuste `Frontend:BaseUrl` e as expiracoes em `IdentityTokens`.
3. Configure SMTP em `EmailSettings` e remetente em `Email`.
4. Opcional: ajuste a lista `Cors:Allow` para os dominios do seu front.

## Restaurar, migrar e rodar

Na raiz do repo:

```bash
dotnet restore

# aplica migracoes do EF Core usando o projeto de dados como origem de migracoes
dotnet ef database update --project src/Pruduct.Data --startup-project src/Pruduct.Api

# executa a API
dotnet run --project src/Pruduct.Api
```

A API sobe, por padrao, nas portas configuradas no `launchSettings.json` (Kestrel e/ou IIS Express).

## Estrutura rápida

- `src/Pruduct.Api` – host da API, configuracao de DI, middlewares, Swagger.
- `src/Pruduct.Business` – regras de negocio, validacoes, servicos.
- `src/Pruduct.Data` – EF Core com PostgreSQL e migracoes; Dapper para consultas pontuais.
- `src/Pruduct.Contracts` / `src/Pruduct.Common` – DTOs, enums, entidades base e utilitarios.


