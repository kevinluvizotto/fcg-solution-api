// Config central via Azure API Management
const apiGatewayBase = "https://fcg-apim-fiap-klztt.azure-api.net";

// Endpoints ajustados para a nova estrutura no APIM
const apiUsers = `${apiGatewayBase}/users`;              
const apiGames = `${apiGatewayBase}/games`;   
const apiPayments = `${apiGatewayBase}/payments`; 
// Headers padrão
function getAuthHeaders() {
    const token = localStorage.getItem("fcg_token");
    return token
        ? { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" }
        : { "Content-Type": "application/json" };
}
