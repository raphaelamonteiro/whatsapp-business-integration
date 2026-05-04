using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using chat_with_api.Services;
using chat_with_api.Plugins;
using chat_with_api.State;
using System.Net.Http;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// 1. carrega as confis
var config = builder.Configuration.AddJsonFile("appsettings.json").Build();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<PedidoState>();

// 3. serviços de negócio
builder.Services.AddSingleton<DeliveryApiService>(sp =>
    new DeliveryApiService(
        config["WhatsApp:API_TOKEN"] ?? "",
        config["DeliveryApi:BaseUrl"] ?? "http://localhost:5256"
    ));


builder.Services.AddSingleton<WhatsAppService>(sp =>
    new WhatsAppService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
        config["WhatsApp:PhoneNumberId"]!,
        config["WhatsApp:AccessToken"]!,
        config["WhatsApp:ApiUrl"] ?? "https://graph.facebook.com/v17.0"
    ));

// 4. configs do semantic kernel
builder.Services.AddScoped<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    // lendo o json
    string modelId = config["Ollama:ModelId"] ?? "ministral-3:14b";
    string apiKey = config["Ollama:ApiKey"] ?? "";
    string endpoint = config["Ollama:Endpoint"] ?? "";

    kernelBuilder.AddOpenAIChatCompletion(
        modelId: modelId,
        apiKey: apiKey,
        endpoint: new Uri(endpoint)
    );

    var kernel = kernelBuilder.Build();

    var apiService = sp.GetRequiredService<DeliveryApiService>();
    var pedidoState = sp.GetRequiredService<PedidoState>();
    kernel.ImportPluginFromObject(new DeliveryPlugin(apiService, pedidoState), "DeliveryPlugin");

    return kernel;
});

// histórico e prompt
builder.Services.AddSingleton<ChatHistory>(sp =>
{
    var history = new ChatHistory();
    history.AddSystemMessage("""
        Você é o TechBot 🤖, atendente virtual de um delivery.

        Fale de forma leve, educada e direta.
        Use poucos emojis (1 ou 2 por mensagem).

        ## OBJETIVO
        Conduzir o pedido do cliente passo a passo usando funções.
        Nunca invente dados.

        ## REGRA SOBRE FUNÇÕES (CRÍTICO)
        Sempre que precisar executar uma ação:
        → NÃO escreva texto
        → chame APENAS UMA função
        → aguarde o retorno antes de continuar

        ## FLUXO OBRIGATÓRIO
        1. telefone → InformarTelefone
        2. itens → ListarProdutos ou BuscarProdutos → AdicionarItemPedido
        3. endereço completo → InformarEndereco
        4. pagamento → InformarPagamento
        5. final → VerPedido → confirmação → FinalizarPedido

        ## TRAVA GLOBAL (TELEFONE)
        Se o telefone NÃO estiver registrado:
        → peça o telefone de forma simpática
        → NÃO execute mais nada

        EXCEÇÃO:
        → o cliente pode ver o cardápio sem informar telefone

        ## CARDÁPIO (CRÍTICO)
        Se o cliente pedir cardápio, opções ou similares:
        → chame ListarProdutos IMEDIATAMENTE
        → NÃO escreva nada antes disso

        Após receber o retorno:
        → exiba TODOS os produtos com nome e preço

        Formato obrigatório:
        *Pizza de Calabresa – R$52,00*

        PROIBIDO:
        → dizer "aqui está o cardápio" sem listar os itens abaixo

        ## PRODUTOS
        Se o cliente mencionar um produto ou demonstrar intenção de compra:
        → chame BuscarProdutos
        → após retorno, chame AdicionarItemPedido

        Depois confirme:
        "Ótima escolha! ✅"

        ## ENDEREÇO
        Solicite endereço COMPLETO:

        Obrigatório:
        → rua e número
        → bairro

        Opcional:
        → complemento

        Se faltar informação:
        → peça apenas o que falta

        Exemplo:
        "Poderia me informar seu bairro? 😊"

        ## OBSERVAÇÕES DO PEDIDO
        O cliente pode fazer observações sobre o item (ex: "sem cebola", "molho à parte").

        Se houver observação:
        → inclua junto ao item ao chamar AdicionarItemPedido
        → nunca ignore a observação

        Se a observação vier depois que o item já foi adicionado:
        → atualize o item chamando AdicionarItemPedido novamente com a observação

        ## ALTERAÇÃO DE ITENS
        Se o cliente quiser trocar um item:
        → substitua o item anterior ao chamar AdicionarItemPedido

        Se quiser remover um item:
        → chame AdicionarItemPedido com ação de remoção (se disponível)

        Nunca mantenha itens duplicados sem confirmação

        ## PAGAMENTO
        Pergunte:
        "Qual seria a forma de pagamento? Aceitamos dinheiro, cartão ou Pix 💳"

        ## FINALIZAÇÃO
        → chame VerPedido
        → mostre o resumo completo
        → peça confirmação do cliente

        Somente após confirmação:
        → chame FinalizarPedido

        Depois:
        → agradeça e finalize com simpatia 🎉

        ## PROIBIÇÕES
        - Nunca pular etapas
        - Nunca inventar produtos ou preços
        - Nunca chamar mais de uma função por vez
        - Nunca executar ação sem usar função
        - Nunca mostrar cardápio sem listar itens

        ## ESTILO
        - Máximo de 3 frases por mensagem
        - Tom natural e acolhedor
        - Seja objetivo
        - Em caso de dúvida: faça uma pergunta curta
        """);
    return history;
});

var app = builder.Build();

// endpoints (webhook Meta)

app.MapGet("/webhook", (HttpContext context) =>
{
    var query = context.Request.Query;
    string verifyToken = config["WhatsApp:VerifyToken"] ?? "";
    if (query["hub.mode"] == "subscribe" && query["hub.verify_token"] == verifyToken)
    {
        return Results.Text(query["hub.challenge"].ToString());
    }
    return Results.BadRequest();
});

app.MapPost("/webhook", async (HttpContext context, WhatsAppService whatsapp, ChatHistory history, Kernel k) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    using var json = JsonDocument.Parse(body);

    try
    {
        var entry = json.RootElement.GetProperty("entry")[0];
        var changes = entry.GetProperty("changes")[0];
        var value = changes.GetProperty("value");

        if (value.TryGetProperty("messages", out var messages))
        {
            var msg = messages[0];
            if (msg.TryGetProperty("text", out var textObj))
            {
                var userMessage = textObj.GetProperty("body").GetString() ?? "";
                var from = msg.GetProperty("from").GetString() ?? "";
                var messageId = msg.GetProperty("id").GetString() ?? "";

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await whatsapp.SendTypingAsync(from, messageId);
                        history.AddUserMessage(userMessage);

                        var chatService = k.GetRequiredService<IChatCompletionService>();
                        var settings = new OpenAIPromptExecutionSettings
                        {
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                        };

                        var result = await chatService.GetChatMessageContentAsync(history, settings, k);

                        // tratamento da resposta
                        string respostaBruta = result.Content ?? "";

                        // remove o think
                        string respostaParaEnviar = Regex.Replace(respostaBruta, @"<think>.*?</think>", "", RegexOptions.Singleline).Trim();

                        if (string.IsNullOrEmpty(respostaParaEnviar))
                        {
                            result = await chatService.GetChatMessageContentAsync(history, settings, k);
                            respostaParaEnviar = result.Content ?? "Como posso ajudar?";
                        }

                        await whatsapp.SendTextMessageAsync(from, respostaParaEnviar);
                        history.AddAssistantMessage(respostaParaEnviar);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Erro na IA: {ex.Message}");
                    }
                });
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Webhook: {ex.Message}");
    }

    return Results.Ok();
});

app.Run("http://0.0.0.0:5000");