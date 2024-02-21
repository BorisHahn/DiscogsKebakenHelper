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
            new DiscogsAuthentifierClient(oAuthConsumerInformation)));
    }

    var currentUser = arrayOfUsers.Find(user => user.ChatId == update.Message!.Chat.Id);
    var state = currentUser.ChatMode;

    if (update.Message.Text == "/exit")
    {
        currentUser.ChatMode = ChatMode.AskMenuCommand;
        await SendMenu(client, update, currentUser);
    }
    else
    {
        switch (state)
        {
            case ChatMode.Initial:
                await client.SendTextMessageAsync(
                    chatId: update.Message!.Chat.Id,
                    text:
                    "Для использования всего функционала приложения пройдите пожалуйста аутентификацию. Для этого введите команду /auth или нажмите на кнопку ниже",
                    cancellationToken: ct
                );
                currentUser.ChatMode = ChatMode.AskMenuCommand;
                break;
            case ChatMode.ReadyToAuth:

                await client.SendTextMessageAsync(
                    chatId: update.Message!.Chat.Id,
                    text:
                    "Аутентификация прошла успешно",
                    cancellationToken: ct);
                //var oauthInformFinish = currentUser.DiscogsClient.Authorize(s => Task.FromResult(GetToken(s, update))).Result;

                /*static string GetToken(string url, Update update)
                {
                    Console.WriteLine("Please authourize the application and enter the final key in the console");
                    Console.WriteLine(url);
                    //Process.Start(url);
                    string tokenKey = update.Message.Text;
                    tokenKey = string.IsNullOrEmpty(tokenKey) ? null : tokenKey;
                    return tokenKey;
                }*/
                currentUser.ChatMode = ChatMode.AskMenuCommand;
                break;
            case ChatMode.AskMenuCommand:
                switch (update.Message.Text)
                {
                    case "/auth":
                        InlineKeyboardMarkup inlineKeyboard = new(new[]
                        {
                            InlineKeyboardButton.WithUrl(
                                text: "Получить код",
                                url:
                                "https://www.discogs.com/oauth/authorize?oauth_token=cjUZQQrgpqDbyTuIbVLavbFqpykypwSxXUAeKjLq")
                        });

                        await client.SendTextMessageAsync(
                            chatId: update.Message!.Chat.Id,
                            text: "1) Перейдите по сгенерированной ссылке ниже\n" +
                                  "2) Если потребуется, пройдите авторизацию в сервисе Discogs\n" +
                                  "3) Скопируйте код и отправьте в чат",
                            replyMarkup: inlineKeyboard,
                            cancellationToken: ct);

                        currentUser.ChatMode = ChatMode.ReadyToAuth;
                        break;

                    default:
                        await SendMenu(client, update, currentUser);
                        break;
                }

                break;
            default:
                switch (update.Message.Text)
                {
                    case "/auth":
                        await client.SendTextMessageAsync(
                            chatId: update.Message!.Chat.Id,
                            text:
                            "1) Перейдите по сгенерированной ссылке ниже\n" +
                            "2) Пройдите авторизацию в сервисе Discogs\n" +
                            "3) Скопируйте код и отправьте в чат",
                            cancellationToken: ct);
                        break;
                    default:
                        await SendMenu(client, update, currentUser);
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

async Task SendMenu(ITelegramBotClient client, Update update, User currentUser)
{
    ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
    {
        new KeyboardButton[] { "/auth", "/exit" },
    })
    {
        ResizeKeyboard = true
    };
    string authCommand = currentUser.UserName == "" ? "/auth - аутентификация пользователя" : "";
    await client.SendTextMessageAsync(chatId: update.Message!.Chat.Id,
        text: "Выберите меню:\n\n" +
              "/exit - вернуться в главное меню\n" +
              $"{authCommand}\n",
        replyMarkup: replyKeyboardMarkup);
}

public enum ChatMode
{
    Initial = 0,
    AskMenuCommand = 1,
    ReadyToAuth = 2,
};