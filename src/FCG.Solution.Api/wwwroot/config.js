// Configuração central da solução FCG (Frontend → APIM → Microsserviços)

// Gateway do Azure API Management
const apiGatewayBase = "https://fcg-apim-fiap-klztt.azure-api.net";

// Endpoints dos microsserviços (via APIM)
const apiUsers = `${apiGatewayBase}/users`;
const apiGames = `${apiGatewayBase}/games`;
const apiPayments = `${apiGatewayBase}/payments`;

// Cabeçalhos padrão para requisições autenticadas
function getAuthHeaders() {
    const token = localStorage.getItem("fcg_token");
    return token
        ? { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" }
        : { "Content-Type": "application/json" };
}

// 🧠 Função utilitária de tratamento de erro global
async function handleApiError(response) {
    if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `Erro HTTP ${response.status}`);
    }
    return response.json();
}

// Função de log simples para debug local (desativada em produção)
function log(msg) {
    if (window.location.hostname === "localhost") console.log(`[FCG] ${msg}`);
}
