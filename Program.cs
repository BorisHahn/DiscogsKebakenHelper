using DiscogsClient;
using DiscogsKebakenHelper;
using RestSharpHelper.OAuth1;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using User = DiscogsKebakenHelper.User;

var botClient = new TelegramBotClient("6736675222:AAGVMHDj0qK1nQmga8a9NZkX4LE-B0AhDbk");
var appConfiguration = new AppConfiguration();
var newSearchProcess = new SearchProcess();
var oAuthConsumerInformation =
    new OAuthConsumerInformation(appConfiguration.ConsumerKey, appConfiguration.ConsumerSecret);
var arrayOfUsers = new List<User>();
var ro = new ReceiverOptions
{
    AllowedUpdates = new Telegram.Bot.Types.Enums.UpdateType[] { },
};

botClient.StartReceiving(updateHandler: Handler, pollingErrorHandler: ErrorHandler, receiverOptions: ro);

Console.WriteLine("Стартовали");
Console.ReadLine();

async Task Handler(ITelegramBotClient client, Update update, CancellationToken ct)
{
    var checkUser = arrayOfUsers.Any(user => user.ChatId == update.Message!.Chat.Id);
    if (checkUser == false)
    {
        arrayOfUsers.Add(new User(update.Message!.Chat.Id, ChatMode.Initial, "", "", "",
            new DiscogsAuthentifierClient(oAuthConsumerInformation), ""));
    }

    var currentUser = arrayOfUsers.Find(user => user.ChatId == update.Message!.Chat.Id);
    var state = currentUser.ChatMode;

    Console.WriteLine(currentUser.ChatId);
    Console.WriteLine(currentUser.UserName);
    Console.WriteLine(currentUser.OauthToken);
    Console.WriteLine(currentUser.OauthTokenSecret);
    
    if (update.Message.Text == "/exit")
    {
        currentUser.ChatMode = ChatMode.AskMenuCommand;
        await SendMenu(client, update, currentUser, ct);
    }
    else
    {
        switch (state)
        {
            case ChatMode.Initial:
                await client.SendTextMessageAsync(
                    chatId: update.Message!.Chat.Id,
                    text:
                    "Для использования всего функционала приложения пройдите пожалуйста аутентификацию. Для этого введите команду /auth",
                    cancellationToken: ct
                );
                currentUser.ChatMode = ChatMode.AskMenuCommand;
                break;
            case ChatMode.ReadyToAuth:
                currentUser.UserRequestToken = update.Message.Text;
                currentUser.ChatMode = ChatMode.AskMenuCommand;
                break;
            case ChatMode.AskMenuCommand:
                switch (update.Message.Text)
                {
                    case "/auth":
                        currentUser.DiscogsClient.Authorize(s =>
                        {
                            InlineKeyboardMarkup inlineKeyboard = new(new[]
                            {
                                InlineKeyboardButton.WithUrl(
                                    text: "Получить код",
                                    url:
                                    $"{s}")
                            });

                            client.SendTextMessageAsync(
                                chatId: update.Message!.Chat.Id,
                                text: "1) Перейдите по сгенерированной ссылке ниже\n" +
                                      "2) Если потребуется, пройдите авторизацию в сервисе Discogs\n" +
                                      "3) Скопируйте код и отправьте в чат",
                                replyMarkup: inlineKeyboard,
                                cancellationToken: ct);
                            while (currentUser.UserRequestToken == "")
                            {
                                /*Console.WriteLine("userRequestToken");
                                Console.WriteLine(userRequestToken);*/
                            }

                            return Task.FromResult(currentUser.UserRequestToken);
                        }).ContinueWith((res) =>
                        {
                            currentUser.OauthToken = res.Result.TokenInformation.Token;
                            currentUser.OauthTokenSecret = res.Result.TokenInformation.TokenSecret;
                            var _DiscogsClient = new DiscogsClient.DiscogsClient(res.Result);
                            _DiscogsClient.GetUserIdentityAsync().ContinueWith((r) =>
                            {
                                currentUser.UserName = r.Result.username;
                            });
                            client.SendTextMessageAsync(
                                chatId: update.Message!.Chat.Id,
                                text:
                                "Вы успешно прошли аутентификацию!",
                                cancellationToken: ct
                            );
                            return Task.FromResult(res);
                        });
                        
                        currentUser.ChatMode = ChatMode.ReadyToAuth;
                        
                        break;
                    case "/search":
                        await newSearchProcess.StartSearchProcess(client, update, currentUser, ct);
                        break;
                    default:
                        await SendMenu(client, update, currentUser, ct);
                        break;
                }

                break;
            default:
                switch (update.Message.Text)
                {
                    case "/search":
                        currentUser.ChatMode = ChatMode.SearchProcess;
                        await newSearchProcess.StartSearchProcess(client, update, currentUser, ct);
                        break;
                    default:
                        await SendMenu(client, update, currentUser, ct);
                        break;
                }

                break;
        }
    }
}

async Task ErrorHandler(ITelegramBotClient client, Exception exception, CancellationToken ct)
{
    Console.WriteLine("");
}

async Task SendMenu(ITelegramBotClient client, Update update, User currentUser, CancellationToken ct)
{
    string authCommand = currentUser.UserName == "" ? "/auth - аутентификация пользователя" : "";
    await client.SendTextMessageAsync(chatId: update.Message!.Chat.Id,
        text: "Выберите меню:\n\n" +
              "/search - поиск по базе данных\n" +
              "/exit - вернуться в главное меню\n" +
              $"{authCommand}\n",
        cancellationToken: ct);
}

public enum ChatMode
{
    Initial = 0,
    AskMenuCommand = 1,
    ReadyToAuth = 2,
    SearchProcess = 3
};