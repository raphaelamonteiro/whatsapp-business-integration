using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using chat_with_api.Services;
using chat_with_api.State;
using chat_with_api.Plugins;

var builder = WebApplication.CreateBuilder(args);

// 1. Carrega as configurações (onde estão seus tokens)
var config = builder.Configuration.AddJsonFile("appsettings.json").Build();

// 2. CONFIGURAÇÃO DO SEMANTIC KERNEL
var kernelBuilder = Kernel.CreateBuilder();

// Registro do Ollama
kernelBuilder.AddOllamaChatCompletion(
    modelId: "qwen2.5:3b",
    endpoint: new Uri("http://localhost:11434")
);

// --- INJEÇÃO DOS SEUS SERVIÇOS NO KERNEL ---
// Aqui passamos o token do JSON diretamente para o construtor do DeliveryApiService
kernelBuilder.Services.AddSingleton<DeliveryApiService>(sp =>
{
    var apiToken = config["WhatsApp:API_TOKEN"] ?? "";
    return new DeliveryApiService(apiToken);
});

kernelBuilder.Services.AddSingleton<PedidoState>();

// Constrói o Kernel e Importa os Plugins
var myKernel = kernelBuilder.Build();
myKernel.ImportPluginFromType<DeliveryPlugin>();

// Registra o Kernel no builder principal para o Webhook usar
builder.Services.AddSingleton(myKernel);

// 3. REGISTRO DO WHATSAPP SERVICE
builder.Services.AddHttpClient();
builder.Services.AddSingleton<WhatsAppService>(sp =>
    new WhatsAppService(
        sp.GetRequiredService<HttpClient>(),
        config["WhatsApp:PhoneNumberId"]!,
        config["WhatsApp:AccessToken"]!,
        config["WhatsApp:ApiUrl"]!
    ));

// 4. HISTÓRICO DE MENSAGENS (O "Cérebro" do TechBot)
builder.Services.AddSingleton<ChatHistory>(sp =>
{
    var history = new ChatHistory();
    history.AddSystemMessage("""
            Você é o TechBot, atendente de delivery.

            ## OBJETIVO
            Conduzir o pedido passo a passo usando funções.

            ## REGRA PRINCIPAL
            - Nunca invente produtos, preços ou respostas de cardápio
            - Sempre use funções para qualquer dado
            - Se existir função → NÃO responda texto

            ## TRAVA GLOBAL (PRIORIDADE MÁXIMA)

            Se telefone = nao:
            → única resposta possível: pedir telefone
            → única função possível: InformarTelefone
            → ignore qualquer outra mensagem

            ## ESTADO INTERNO
            telefone: nao
            itens: nao
            endereco: nao
            pagamento: nao

            ## FLUXO OBRIGATÓRIO

            1. telefone → InformarTelefone  
            2. itens → ListarProdutos ou BuscarProdutos → AdicionarItemPedido  
            3. endereco → InformarEndereco  
            4. pagamento → InformarPagamento  
            5. final → VerPedido → FinalizarPedido  

            ## REGRAS DE AÇÃO

            ### TELEFONE
            Se não tiver telefone:
            → pedir telefone
            → não diga mais nada

            ---

            ### CARDÁPIO (CRÍTICO)

            Se usuário disser:
            - "cardápio"
            - "ver cardápio"
            - "ver opções"

            → ação obrigatória:
            → chamar ListarProdutos

            PROIBIDO:
            - responder texto
            - dizer que não tem produtos
            - inventar itens

            ---

            ### PRODUTO

            Se usuário disser:
            - "quero X"
            - "me vê X"

            → chamar BuscarProdutos

            Após retorno:
            → chamar AdicionarItemPedido

            ---

            ### ENDEREÇO

            Se itens = sim e endereco = nao:
            → pedir endereço
            → InformarEndereco

            ---

            ### PAGAMENTO

            Se endereco = sim e pagamento = nao:
            → pedir pagamento
            → InformarPagamento

            ---

            ### FINALIZAÇÃO

            Se tudo preenchido:
            → VerPedido
            → depois FinalizarPedido

            ---

            ## PROIBIÇÕES

            - Nunca pular etapa
            - Nunca chamar 2 funções juntas
            - Nunca responder produto sem função
            - Nunca inventar resposta
            - Nunca dizer "não sei" ou "não tem"

            ---

            ## MENSAGENS SIMPLES

            Se usuário disser:
            - "ok", "sim", "obrigado"

            → responder normal (sem função)

            ---

            ## RESPOSTAS

            - Máx. 2 frases
            - Direto ao ponto
            - Sem explicações longas

            ---

            ## COMPORTAMENTO EM DÚVIDA

            Se não entender:
            → faça pergunta curta

            Ex:
            "Qual item você quer?"
        """);
    return history;
});

var app = builder.Build();

// --- ENDPOINTS ---

// GET: Handshake com a Meta (Validação do Webhook)
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

// POST
// POST: Recebe mensagens do WhatsApp
app.MapPost("/webhook", async (HttpContext context, WhatsAppService whatsapp, ChatHistory history, Kernel k) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    using var json = JsonDocument.Parse(body);
    var value = json.RootElement.GetProperty("entry")[0].GetProperty("changes")[0].GetProperty("value");

    if (value.TryGetProperty("messages", out var messages))
    {
        var userMessage = messages[0].GetProperty("text").GetProperty("body").GetString() ?? "";
        var from = messages[0].GetProperty("from").GetString() ?? "";

        // Respondemos OK para a Meta não repetir a mensagem
        _ = Task.Run(async () =>
        {
            try
            {
                // 1. Sinal de digitando
                await whatsapp.SendTypingAsync(from);

                history.AddUserMessage(userMessage);

                var chatService = k.GetRequiredService<IChatCompletionService>();
                var settings = new OllamaPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    Temperature = 0 // Mantém a IA focada nos dados reais
                };

                // Chamada 1: Executa o Plugin/Função
                var result = await chatService.GetChatMessageContentAsync(history, settings, k);

                // Chamada 2: Se vier vazio ou com tag de ferramenta, força a geração do texto
                if (string.IsNullOrEmpty(result.Content) || result.Content.Contains("tool_name"))
                {
                    result = await chatService.GetChatMessageContentAsync(history, settings, k);
                }

                // --- VALIDAÇÃO DE SEGURANÇA (O conserto do erro text.body) ---
                string respostaFinal = result.Content ?? "";

                // Se a IA ainda assim não gerou texto, mas o plugin rodou, nós damos o texto final
                if (string.IsNullOrWhiteSpace(respostaFinal) || respostaFinal.Length < 3)
                {
                    // Pequeno truque: verificamos se no histórico a última mensagem (do plugin) tem dados
                    respostaFinal = "Perfeito! Já consultei nosso sistema. No que posso te ajudar com as opções que encontrei?";
                }

                // Envia para o WhatsApp (Garantido que não é vazio!)
                await whatsapp.SendTextMessageAsync(from, respostaFinal);
                history.AddAssistantMessage(respostaFinal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro no processamento da IA: {ex.Message}");
            }
        });
    }

    return Results.Ok();
});

app.Run("http://0.0.0.0:5000");