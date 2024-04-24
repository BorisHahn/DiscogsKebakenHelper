using DiscogsKebakenHelper.Data;
using DiscogsKebakenHelper.Model;
using RestSharpHelper.OAuth1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiscogsKebakenHelper
{
    public enum MoveState
    {
        initial = 0,
        setNewStoragePlace = 1,
    };

    public class MoveModeState
    {
        public MoveState Mode { get; set; }
    }

    public class MoveStoragePlace
    {
        public string InstanceId { get; set; }
        public string NewStoragePlace { get; set; }
        public Dictionary<long, MoveModeState> ChatDict { get; set; } = new();

        public async Task StartMoveProcess(ITelegramBotClient client, Message message, User currentUser,
        CancellationToken ct, string instanceId)
        {
            if (!ChatDict.TryGetValue(message!.Chat.Id, out var state))
            {
                ChatDict.Add(message!.Chat.Id, new MoveModeState());
            }
            state = ChatDict[message!.Chat.Id];

            switch (state.Mode)
            {
                case MoveState.initial:
                    InstanceId = instanceId;
                    await client.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Введите новое физическое место хранения:",
                        cancellationToken: ct);
                    state.Mode = MoveState.setNewStoragePlace;
                    break;
                case MoveState.setNewStoragePlace:
                    NewStoragePlace = message.Text;
                    Move(NewStoragePlace, InstanceId, currentUser);
                    await client.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "Физическое место хранения релиза успешно изменено!",
                        cancellationToken: ct);
                    break;
            }
        }

        async void Move(string newStorage, string instanceId, User user)
        {
            using (PostgresContext db = new())
            {
                ReleaseData.UpdateReleaseStoragePlace(db, newStorage, instanceId);
            }

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
                    UserRequestToken = user.UserRequestToken
                });
            }
        }
    }
}
