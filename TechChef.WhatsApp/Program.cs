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

// Coloca aqui o SEU número que você cadastrou como destinatário de teste
// Formato internacional, sem espaços: +5511999999999
await service.SendTemplateAsync(to: "+55119XXXXXXXX");