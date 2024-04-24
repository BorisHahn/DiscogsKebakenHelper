using DiscogsClient;
using DiscogsKebakenHelper;
using DiscogsKebakenHelper.Data;
using DiscogsKebakenHelper.Model;
using Newtonsoft.Json;
using RestSharpHelper.OAuth1;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using User = DiscogsKebakenHelper.User;
LoadConfiguration();
var botClient = new TelegramBotClient(AppConfiguration.TelegramBotToken);
var newSearchProcessDict = new Dictionary<long, SearchProcess>();
var newSearchProcessInDbDict = new Dictionary<long, SearchProcessInDB>();
var newAddProcessDict = new Dictionary<long, AddProcess>();
var newDeleteProcessDict = new Dictionary<long, DeleteProcess>();
var newMoveProcessDict = new Dictionary<long, MoveStoragePlace>();
var oAuthConsumerInformation =
    new OAuthConsumerInformation(AppConfiguration.ConsumerKey, AppConfiguration.ConsumerSecret);
var dateTime = DateTime.UtcNow;

var ro = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

botClient.StartReceiving(updateHandler: Handler, pollingErrorHandler: ErrorHandler, receiverOptions: ro);

Console.WriteLine("Стартовали");
Console.ReadLine();

void LoadConfiguration()
{
    using (StreamReader r = new StreamReader("configuration.json"))
    {
        string json = r.ReadToEnd();
        AppConfigurationParse configuration = JsonConvert.DeserializeObject<AppConfigurationParse>(json);
        AppConfiguration.ConsumerSecret = configuration.ConsumerSecret;
        AppConfiguration.TelegramBotToken = configuration.TelegramBotToken;
        AppConfiguration.ConsumerKey = configuration.ConsumerKey;
        AppConfiguration.SubdPassword = configuration.SubdPassword;
    }
}
async Task Handler(ITelegramBotClient client, Update update, CancellationToken ct)
{
    User? checkUser;
    User? currentUser;
    var message = update.CallbackQuery != null ? update.CallbackQuery.Message : update.Message;
    if (message?.Date < dateTime)
    {
        return;
    }
   
    using (PostgresContext db = new ())
    {
        checkUser = UserData.GetUser(db, (int)message!.Chat.Id);
        
        if (checkUser == null)
        {
            UserData.AddUser(db, new User
            {
                ChatId = (int)message!.Chat.Id,
                ChatMode = Enums.chatMode[0],
                UserName = "",
                OauthToken = "",
                OauthTokenSecret = "",
                UserRequestToken = "",
            });
        }
        currentUser = UserData.GetUser(db, (int)message!.Chat.Id);
    }
    if (currentUser == null)
    {
        await client.SendTextMessageAsync(
            chatId: message!.Chat.Id,
            text:
            "ОШИБКА ПОИСКА ПОЛЬЗОВАТЕЛЯ.\nДля использования всего функционала приложения пройдите пожалуйста аутентификацию. Для этого введите команду /auth",
            cancellationToken: ct
        );
        return;
    }
    if (!newSearchProcessDict.TryGetValue(message!.Chat.Id, out var newSearchProcess))
    {
        newSearchProcessDict.Add(message!.Chat.Id, new SearchProcess());
    }
    if (!newSearchProcessInDbDict.TryGetValue(message!.Chat.Id, out var newSearchProcessInDb))
    {
        newSearchProcessInDbDict.Add(message!.Chat.Id, new SearchProcessInDB());
    }
    if (!newAddProcessDict.TryGetValue(message!.Chat.Id, out var newAddProcess))
    {
        newAddProcessDict.Add(message!.Chat.Id, new AddProcess());
    }
    if (!newDeleteProcessDict.TryGetValue(message!.Chat.Id, out var newDeleteProcess))
    {
        newDeleteProcessDict.Add(message!.Chat.Id, new DeleteProcess());
    }
    if (!newMoveProcessDict.TryGetValue(message!.Chat.Id, out var newMoveProcess))
    {
        newMoveProcessDict.Add(message!.Chat.Id, new MoveStoragePlace());
    }
    if (update.CallbackQuery != null)
    {
        var chatId = update.CallbackQuery.Message.Chat.Id;
        var splitedData = update.CallbackQuery.Data.Split(',');
        var method = splitedData[0];

        switch (method)
        {
            case "delete":
                var releaseId = splitedData[1];
                var instanceId = splitedData[2];
                DeleteOutsideProcess.Delete(chatId, releaseId, instanceId);
                await client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "Удалено");
                break;
            case "move":
                var instanceIdMove = splitedData[1];
                await newMoveProcess.StartMoveProcess(client, message, currentUser, ct, instanceIdMove);
                using (PostgresContext db = new())
                {
                    UserData.UpdateUser(db, new User
                    {
                        Uid = currentUser.Uid,
                        ChatId = currentUser.ChatId,
                        ChatMode = Enums.chatMode[7],
                        OauthToken = currentUser.OauthToken,
                        OauthTokenSecret = currentUser.OauthTokenSecret,
                        UserName = currentUser.UserName,
                        UserRequestToken = currentUser.UserRequestToken
                    });
                }
                break;
        }
        return;
    }

    var state = currentUser.ChatMode;
    
    if (update.Message.Text == "/exit" || update.Message.Text == "/menu")
    {
        using (PostgresContext db = new())
        {
            UserData.UpdateUser(db, new User
            {
                Uid = currentUser.Uid,
                ChatId = currentUser.ChatId,
                ChatMode = Enums.chatMode[1],
                OauthToken = currentUser.OauthToken,
                OauthTokenSecret = currentUser.OauthTokenSecret,
                UserName = currentUser.UserName,
                UserRequestToken = currentUser.UserRequestToken
            });
        }
        await SendMenu(client, update, currentUser, ct);
    }
    else
    {
        switch (state)
        {
            case "Initial":
                await client.SendTextMessageAsync(
                    chatId: update.Message!.Chat.Id,
                    text:
                    "Для использования всего функционала приложения пройдите пожалуйста аутентификацию. Для этого введите команду /auth",
                    cancellationToken: ct
                );
                using (PostgresContext db = new())
                {
                    UserData.UpdateUser(db, new User
                    {
                        Uid = currentUser.Uid,
                        ChatId = currentUser.ChatId,
                        ChatMode = Enums.chatMode[1],
                        OauthToken = currentUser.OauthToken,
                        OauthTokenSecret = currentUser.OauthTokenSecret,
                        UserName = currentUser.UserName,
                        UserRequestToken = currentUser.UserRequestToken
                    });
                }
                break;
            case "ReadyToAuth":
                using (PostgresContext db = new())
                {
                    UserData.UpdateUser(db, new User
                    {
                        Uid = currentUser.Uid,
                        ChatId = currentUser.ChatId,
                        ChatMode = Enums.chatMode[1],
                        OauthToken = currentUser.OauthToken,
                        OauthTokenSecret = currentUser.OauthTokenSecret,
                        UserName = currentUser.UserName,
                        UserRequestToken = update.Message.Text
                    });
                }
                break;
            case "SearchProcess":
                await newSearchProcess.StartSearchProcess(client, update, currentUser, ct, oAuthConsumerInformation);
                break;
            case "SearchInDbProcess":
                await newSearchProcessInDb.StartSearchInDbProcess(client, update, currentUser, ct, oAuthConsumerInformation);
                break;
            case "AddProcess":
                await newAddProcess.StartAddProcess(client, update, currentUser, ct,
                            oAuthConsumerInformation);
                break;
            case "DeleteProcess":
                await newDeleteProcess.StartDeleteProcess(client, update, currentUser, ct,
                            oAuthConsumerInformation);
                break;
            case "MoveStoragePlace":
                await newMoveProcess.StartMoveProcess(client, message, currentUser, ct, newMoveProcess.InstanceId);
                break;
            case "AskMenuCommand":
                switch (update.Message.Text)
                {
                    case "/auth":
                        Auth(client, update, currentUser, ct);
                        using (PostgresContext db = new())
                        {
                            UserData.UpdateUser(db, new User
                            {
                                Uid = currentUser.Uid,
                                ChatId = currentUser.ChatId,
                                ChatMode = Enums.chatMode[2],
                                OauthToken = currentUser.OauthToken,
                                OauthTokenSecret = currentUser.OauthTokenSecret,
                                UserName = currentUser.UserName,
                                UserRequestToken = currentUser.UserRequestToken
                            });
                        }
                        break;
                    case "/search":
                        await newSearchProcess.StartSearchProcess(client, update, currentUser, ct,
                            oAuthConsumerInformation);
                        using (PostgresContext db = new())
                        {
                            UserData.UpdateUser(db, new User
                            {
                                Uid = currentUser.Uid,
                                ChatId = currentUser.ChatId,
                                ChatMode = Enums.chatMode[3],
                                OauthToken = currentUser.OauthToken,
                                OauthTokenSecret = currentUser.OauthTokenSecret,
                                UserName = currentUser.UserName,
                                UserRequestToken = currentUser.UserRequestToken
                            });
                        }
                        break;
                    case "/searchDB":
                        await newSearchProcessInDb.StartSearchInDbProcess(client, update, currentUser, ct,
                            oAuthConsumerInformation);
                        using (PostgresContext db = new())
                        {
                            UserData.UpdateUser(db, new User
                            {
                                Uid = currentUser.Uid,
                                ChatId = currentUser.ChatId,
                                ChatMode = Enums.chatMode[6],
                                OauthToken = currentUser.OauthToken,
                                OauthTokenSecret = currentUser.OauthTokenSecret,
                                UserName = currentUser.UserName,
                                UserRequestToken = currentUser.UserRequestToken
                            });
                        }
                        break;
                    case "/add":
                        await newAddProcess.StartAddProcess(client, update, currentUser, ct,
                            oAuthConsumerInformation);
                        using (PostgresContext db = new())
                        {
                            UserData.UpdateUser(db, new User
                            {
                                Uid = currentUser.Uid,
                                ChatId = currentUser.ChatId,
                                ChatMode = Enums.chatMode[4],
                                OauthToken = currentUser.OauthToken,
                                OauthTokenSecret = currentUser.OauthTokenSecret,
                                UserName = currentUser.UserName,
                                UserRequestToken = currentUser.UserRequestToken
                            });
                        }
                        break;
                    case "/delete":
                        await newDeleteProcess.StartDeleteProcess(client, update, currentUser, ct,
                            oAuthConsumerInformation);
                        using (PostgresContext db = new())
                        {
                            UserData.UpdateUser(db, new User
                            {
                                Uid = currentUser.Uid,
                                ChatId = currentUser.ChatId,
                                ChatMode = Enums.chatMode[5],
                                OauthToken = currentUser.OauthToken,
                                OauthTokenSecret = currentUser.OauthTokenSecret,
                                UserName = currentUser.UserName,
                                UserRequestToken = currentUser.UserRequestToken
                            });
                        }
                        break;
                    case "/menu":
                        await SendMenu(client, update, currentUser, ct);
                        break;
                    case "/start":
                        await SendMenu(client, update, currentUser, ct);
                        break;
                    default:
                        await SendMenu(client, update, currentUser, ct);
                        break;
                }

                break;
            default:
                switch (update.Message.Text)
                {
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
    User current;
    using (PostgresContext db = new())
    {
        current = UserData.GetUser(db, (int)update.Message!.Chat.Id);
    }

    string authCommand = current.UserName == ""
        ? "/auth - Аутентификация пользователя в Discogs"
        : "/search - Поиск по базе данных Discogs\n/searchDB - Поиск по персональной базе данных\n/add - Добавить релиз в коллекцию\n/delete - Удалить релиз из коллекции";
    await client.SendTextMessageAsync(chatId: update.Message!.Chat.Id,
        text: "Выберите меню:\n\n" +
              $"{authCommand}\n" +
              "/exit - Вернуться в главное меню\n",
        cancellationToken: ct);
}

async Task Auth(ITelegramBotClient client, Update update, User currentUser, CancellationToken ct)
{
    await new DiscogsAuthentifierClient(oAuthConsumerInformation).Authorize(s =>
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

        var token = "";
        while (token == "")
        {
            Thread.Sleep(3000);
            using (PostgresContext db = new())
            {
                var curUser = UserData.GetUser(db, (int)update.Message!.Chat.Id);
                token = curUser.UserRequestToken;
            }
        }

        return Task.FromResult(token);
    }).ContinueWith((res) =>
    {
        var _DiscogsClient = new DiscogsClient.DiscogsClient(res.Result);
        _DiscogsClient.GetUserIdentityAsync().ContinueWith((r) =>
        {
            User curUser;
            using (PostgresContext db = new())
            {
                curUser = UserData.GetUser(db, (int)update.Message!.Chat.Id);
            }
            using (PostgresContext db = new())
            {
                UserData.UpdateUser(db, new User
                {
                    Uid = curUser.Uid,
                    ChatId = curUser.ChatId,
                    ChatMode = curUser.ChatMode,
                    OauthToken = res.Result.TokenInformation.Token,
                    OauthTokenSecret = res.Result.TokenInformation.TokenSecret,
                    UserName = r.Result.username,
                    UserRequestToken = curUser.UserRequestToken
                });
            }
            SendMenu(client, update, currentUser, ct);
        });
        client.SendTextMessageAsync(
            chatId: update.Message!.Chat.Id,
            text:
            "Вы успешно прошли аутентификацию!",
            cancellationToken: ct
        );
        return Task.FromResult(res);
    });
}

public class Enums
{
    public static Dictionary<int, string> chatMode = new()
    {
        { 0, "Initial"},
        { 1, "AskMenuCommand"},
        { 2, "ReadyToAuth"},
        { 3, "SearchProcess" },
        { 4, "AddProcess" },
        { 5, "DeleteProcess" },
        { 6, "SearchInDbProcess" },
        { 7, "MoveStoragePlace" },
    };
};

public class AppConfigurationParse
{
    public string ConsumerKey { get; set; }
    public string ConsumerSecret { get; set; }
    public string TelegramBotToken { get; set; }
    public string SubdPassword { get; set; }
}