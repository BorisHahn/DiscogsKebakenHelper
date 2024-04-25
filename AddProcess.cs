using Telegram.Bot.Types;
using Telegram.Bot;
using RestSharp;
using RestSharp.Authenticators;
using System.Text.Json.Nodes;
using DiscogsKebakenHelper.Data;
using DiscogsKebakenHelper.Model;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiscogsKebakenHelper
{

    public enum AddState
    {
        initial = 0,
        setStoragePlace = 1,
        addRelease = 2,
    };

    public class AddModeState
    {
        public AddState Mode { get; set; }
    }

    public class AddProcess
    {
        public string ReleaseId { get; set; }
        public string? StoragePlace { get; set; }
        public Dictionary<long, AddModeState> ChatDict { get; set; } = new();

        public async Task StartAddProcess(ITelegramBotClient client, Message message, User currentUser,
        CancellationToken ct, string releaseId)
        {

            if (!ChatDict.TryGetValue(message!.Chat.Id, out var state))
            {
                ChatDict.Add(message!.Chat.Id, new AddModeState());
            }
            state = ChatDict[message!.Chat.Id];

            switch (state.Mode)
            {
                case AddState.initial:
                    ReleaseId = releaseId;
                    ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
                    {
                    new KeyboardButton[] { "Да", "Нет" },

                     });
                    await client.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Добавить физическое место хранения винила?\nПример: Стеллаж: 7, Полка: 3",
                        replyMarkup: replyKeyboardMarkup,
                        cancellationToken: ct);
                    state.Mode = AddState.setStoragePlace;
                    break;
                case AddState.setStoragePlace:
                    switch (message.Text)
                    {
                        case "Да":
                            await client.SendTextMessageAsync(
                                chatId: message.Chat.Id,
                                text: "Где будете хранить?",
                                replyMarkup: new ReplyKeyboardRemove(),
                                cancellationToken: ct);
                            state.Mode = AddState.addRelease;
                            break;
                        case "Нет":
                            StoragePlace = null;
                            await client.SendTextMessageAsync(
                                chatId: message.Chat.Id,
                                text: "Добавляем...",
                                replyMarkup: new ReplyKeyboardRemove(),
                                cancellationToken: ct);
                            Add(client, message, ct, ReleaseId, StoragePlace, currentUser, state);
                            state.Mode = AddState.initial;
                            break;
                    }
                    break;
                case AddState.addRelease:
                    StoragePlace = message.Text;
                    Add(client, message, ct, ReleaseId, StoragePlace, currentUser, state);
                    state.Mode = AddState.initial;
                    break;
            }
        }

        async void Add(ITelegramBotClient TelegramClient, Message message,
            CancellationToken ct, string releaseId, string? storagePlace, User user, AddModeState state)
        {
            var clientTest = new RestClient($"https://api.discogs.com/users/{user.UserName}/collection/folders/1/releases/{releaseId}")
            {
                Authenticator = OAuth1Authenticator.ForProtectedResource(AppConfiguration.ConsumerKey, AppConfiguration.ConsumerSecret, user.OauthToken, user.OauthTokenSecret)
            };

            var request = new RestRequest(Method.POST);
            IRestResponse response = clientTest.Execute(request);

            if (response.IsSuccessful)
            {
                var test = response.Content;
                var jsonObject = JsonNode.Parse(test);
                using (PostgresContext db = new())
                {
                    ReleaseData.AddRelease(db, new Release
                    {
                        ChatId = user.ChatId,
                        Artist = $"{jsonObject["basic_information"]["artists"][0]["name"]}",
                        ReleaseName = $"{jsonObject["basic_information"]["title"]}",
                        ReleaseYear = $"{jsonObject["basic_information"]["year"]}",
                        ReleaseId = $"{jsonObject["id"]}",
                        InstanceId = $"{jsonObject["instance_id"]}",
                        StoragePlace = storagePlace,
                        Thumb = $"{jsonObject["basic_information"]["thumb"]}"
                    });
                }
                await TelegramClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Релиз успешно добавлен в коллекцию!",
                    cancellationToken: ct);
                await TelegramClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text:
                              $"{jsonObject["basic_information"]["thumb"]}\n" +
                              $"Aртист: {jsonObject["basic_information"]["artists"][0]["name"]}\n" +
                              $"Наименование релиза: {jsonObject["basic_information"]["title"]}\n" +
                              $"Год: {jsonObject["basic_information"]["year"]}\n" +
                              $"Место хранение: {(storagePlace != null ? storagePlace : "Место хранения не указано")}\n" +
                              $"Id: {jsonObject["id"]}\n" +
                              $"InstanceId: {jsonObject["instance_id"]}\n",
                        cancellationToken: ct);
                using (PostgresContext db = new())
                {
                    UserData.UpdateUser(db, new User
                    {
                        Uid = user.Uid,
                        ChatId = user.ChatId,
                        ChatMode = Enums.chatMode[1],
                        OauthToken = user.OauthToken,
                        OauthTokenSecret = user.OauthTokenSecret,
                        UserName = user.UserName,
                        UserRequestToken = user.UserRequestToken,
                    });
                }
            }
        }
    }
}

