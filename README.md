# 🕹️ FCG Solution API

Frontend oficial da plataforma **FIAP Cloud Games (FCG)** — responsável por servir as páginas web (Login, Dashboard e Admin) e integrar com os microsserviços da solução.

Este projeto atua como **gateway visual**, hospedando uma aplicação **.NET 9 Minimal Web** que serve conteúdo estático (`wwwroot`) e consome as APIs:

- 👤 [`fcg-users-api`](https://fcg-apim-fiap-klztt01.azure-api.net/users)
- 🎮 [`fcg-games-api`](https://fcg-apim-fiap-klztt01.azure-api.net/games)
- 💳 [`fcg-payments-api`](https://fcg-apim-fiap-klztt01.azure-api.net/payments)

---

## 🚀 Tecnologias

- .NET 9 (ASP.NET Minimal Web Host)
- HTML, CSS e JavaScript puro (Bootstrap 5)
- Docker
- GitHub Actions (CI/CD)
- Azure Web App for Containers

---

## 🧩 Estrutura do Projeto

```
fcg-solution-api/
├── fcg-solution-api.sln
├── Dockerfile
├── .github/
│   └── workflows/
│       ├── ci.yml
│       └── cd.yml
└── src/
    └── FCG.Solution.Api/
        ├── Program.cs
        ├── FCG.Solution.Api.csproj
        └── wwwroot/
            ├── index.html
            ├── login.html
            ├── dashboard.html
            ├── admin.html
            └── config.js
```

---

## ⚙️ Configuração

### 📁 `config.js`
Arquivo de configuração central com as URLs das APIs da plataforma:

```js
// wwwroot/config.js
const apiUsers = "https://fcg-users-api-klztt.azurewebsites.net";
const apiGames = "https://fcg-games-api-klztt.azurewebsites.net";
const apiPayments = "https://fcg-payments-api-klztt.azurewebsites.net";
```

> 💡 Se estiver rodando localmente, altere para:
> ```js
> const apiUsers = "http://localhost:5092";
> const apiGames = "http://localhost:5093";
> const apiPayments = "http://localhost:5094";
> ```

---

## 🧰 Como rodar localmente

### Pré-requisitos
- .NET SDK 9.0+
- Docker (opcional)
- Visual Studio Code ou terminal

### Passos

```bash
# Restaurar e compilar
dotnet build fcg-solution-api.sln

# Executar localmente
dotnet run --project src/FCG.Solution.Api/FCG.Solution.Api.csproj
```

Acesse em:
👉 [http://localhost:80](http://localhost:80)

---

## 🐳 Docker

### Build local
```bash
docker build -t klztt/fcg-solution-api:latest .
```

### Executar localmente
```bash
docker run -d -p 8080:80 klztt/fcg-solution-api:latest
```

Acesse:
👉 [http://localhost:8080](http://localhost:8080)

---

## 🧪 CI - Continuous Integration

> `.github/workflows/ci.yml`

Executa o build e validação do projeto a cada `push` ou `pull request` na branch **`development`**.

```yaml
on:
  push:
    branches: [ "development" ]
  pull_request:
    branches: [ "development" ]
```

---

## 🚀 CD - Continuous Deployment

> `.github/workflows/cd.yml`

Publica automaticamente a imagem Docker no **Docker Hub** a cada `push` na branch **`production`**.

```yaml
on:
  push:
    branches: [ "production" ]
```

A imagem é publicada como:

```
klztt/fcg-solution-api:latest
klztt/fcg-solution-api:<commit-sha>
```

> ⚠️ Requer secrets configurados no GitHub:
> - `DOCKERHUB_USERNAME`
> - `DOCKERHUB_TOKEN`

---

## ☁️ Deploy no Azure Web App for Containers

1. Crie um novo App Service:  
   **Nome:** `fcg-solution-api-klztt`  
   **Tipo:** *Web App for Containers*  
   **Imagem:** `klztt/fcg-solution-api:latest`

2. Configure o `WEBSITES_PORT` para `80`.

3. O frontend ficará disponível em:  
   👉 https://fcg-solution-api-klztt.azurewebsites.net

---

## 🧠 Funcionalidades

| Página | Descrição |
|--------|------------|
| `login.html` | Autenticação e registro de novos usuários |
| `dashboard.html` | Perfil do usuário, loja de jogos e biblioteca |
| `admin.html` | Administração de usuários e jogos (restrito a Admin) |
| `config.js` | Central de configuração de endpoints das APIs |

---

## 📦 Resultado Final

| Serviço | Tipo | Hospedagem | Status |
|----------|------|-------------|--------|
| 🧑‍💻 `fcg-users-api` | .NET 9 Minimal API | Azure App Service | ✅ |
| 🎮 `fcg-games-api` | .NET 9 Minimal API | Azure App Service | ✅ |
| 💳 `fcg-payments-api` | .NET 9 Minimal API | Azure App Service | ✅ |
| 🌐 **`fcg-solution-api`** | .NET 9 Static Web | Azure Web App for Containers | 🚀 |

---

## 📜 Licença

Este projeto faz parte do **Tech Challenge FIAP - Fase 3**, desenvolvido por **Kevin Luvizotto**.  
Uso educacional e demonstrativo.
