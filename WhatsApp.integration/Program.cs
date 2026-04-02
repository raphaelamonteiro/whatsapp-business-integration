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

await service.SendTemplateAsync(
    to: "+55119XXXXXXXX",        // coloca o seu número aqui
    templateName: "hello_world", // template que já vem aprovado em toda conta de teste
    languageCode: "en_US",
    variables: []
);