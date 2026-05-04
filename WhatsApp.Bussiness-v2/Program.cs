using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var service = new WhatsAppService(
    new HttpClient(),
    config["WhatsApp:PhoneNumberId"]!,
    config["WhatsApp:AccessToken"]!,
    config["WhatsApp:ApiUrl"]!
);

var recipient = config["WhatsApp:Recipient"]!;


await service.SendTemplateWithVariablesAsync(
   to: recipient,
   templateName: "pizza_friday_reminder",
   languageCode: "pt_BR",
    variables: ["Raphaela"]   // substitui o {{1}} no template
);

// para mandar para vários clientes de uma vez:
// var clientes = new List<(string telefone, string nome)>
// {
//     ("+5511999999999", "Maria"),
//     ("+5511888888888", "Carlos"),
//     ("+5511777777777", "Ana")
// };
//
// foreach (var cliente in clientes)
// {
//     await service.SendTemplateWithVariablesAsync(
//         to: cliente.telefone,
//         templateName: "friday_pizza_promo",
//         languageCode: "pt_BR",
//         variables: [cliente.nome]
//     );
//
//     // pequena pausa para não estourar o limite
await Task.Delay(500);
// }