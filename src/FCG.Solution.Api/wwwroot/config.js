// 🌐 Configuração centralizada via BFF (fcg-solution-api)
// Assim não existe CORS/preflight no browser.
const apiGatewayBase = ""; // same-origin (ex: https://fcg-solution-api-...)

// 🧱 Endpoints (mantém os mesmos paths que você já usa no APIM)
const apiMgmt = `${apiGatewayBase}/users/users`;
const apiUsers = `${apiGatewayBase}/users`;
const apiStore = `${apiGatewayBase}/games/games`;
const apiLibrary = `${apiGatewayBase}/users/users/me/games`;
const apiPayments = `${apiGatewayBase}/payments`;

// 🔎 Search (via BFF -> Search API)
const apiSearch = `${apiGatewayBase}/search`;
const apiAnalyticsTop = `${apiGatewayBase}/analytics/top`;
const apiAnalyticsPrices = `${apiGatewayBase}/analytics/prices`;
const apiRecommendSimilar = `${apiGatewayBase}/recommendations/similar`;
const apiReindex = `${apiGatewayBase}/reindex`;

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
    mgmt: apiMgmt,
    store: apiStore,
    library: apiLibrary,
    payments: apiPayments,

    // Search
    search: apiSearch,
    analyticsTop: apiAnalyticsTop,
    analyticsPrices: apiAnalyticsPrices,
    recommendSimilar: apiRecommendSimilar,
    reindex: apiReindex,

    getAuthHeaders,
    ensureAuthenticated
};
