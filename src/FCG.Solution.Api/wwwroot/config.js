// 🌐 Configuração centralizada via Azure API Management
const apiGatewayBase = "https://fcg-apim-fiap-klztt.azure-api.net";

// 🧱 Endpoints padronizados (rota APIM + prefixo interno do Users API)
const apiUsers = `${apiGatewayBase}/users`;
const apiStore = `${apiGatewayBase}/users/store/games`;    // 🏪 Loja
const apiLibrary = `${apiGatewayBase}/users/users/me/games`; // 📚 Biblioteca
const apiPayments = `${apiGatewayBase}/payments/payments`;    // 💳 Pagamentos

// 🔑 Token e headers
function getAuthToken() {
    return localStorage.getItem("fcg_token");
}
function getAuthHeaders() {
    const token = getAuthToken();
    return token
        ? { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" }
        : { "Content-Type": "application/json" };
}
function ensureAuthenticated() {
    const token = getAuthToken();
    if (!token) {
        alert("❌ Sessão expirada. Faça login novamente.");
        window.location.href = "login.html";
        return false;
    }
    return true;
}
window.apiConfig = {
    base: apiGatewayBase,
    users: apiUsers,
    store: apiStore,
    library: apiLibrary,
    payments: apiPayments,
    getAuthHeaders,
    ensureAuthenticated
};