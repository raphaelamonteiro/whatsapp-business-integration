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

var recipient = config["WhatsApp:Recipient"];

await service.SendTemplateAsync(to: recipient!);