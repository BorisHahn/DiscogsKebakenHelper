using Telegram.Bot.Types;
using Telegram.Bot;

namespace Otus.Telegram
{

    public enum Mode
    {
        Initial = 0,
        SetLat = 1,
        SetLon = 2,
    }

    public class MapGenState
    {
        public double Lat { get; set; }
        public double Long { get; set; }
        public Mode Mode { get; set; }
    }

    public class MapGenerator
    {
        public Dictionary<long, MapGenState> ChatDict { get; set; } = new();

        public async Task Process(ITelegramBotClient client, Update update, CancellationToken ct, User currentUser)
        {
            if (!ChatDict.TryGetValue(update.Message!.Chat.Id, out var state))
            {
                ChatDict.Add(update.Message!.Chat.Id, new MapGenState());
            }
            
            state = ChatDict[update.Message!.Chat.Id];

            switch (state.Mode)
            {
                case Mode.Initial:
                    await SendInitial(client, update, state, ct);
                    break;
                case Mode.SetLat:
                    await SendSetLat(client, update, state, ct);
                    break;
                case Mode.SetLon:
                    await SendSetLong(client, update, state, ct, currentUser);
                    break;
            }

        }
        private static async Task SendSetLong(ITelegramBotClient client, Update update, MapGenState state, CancellationToken ct, User currentUser)
        {
            var lonText = update.Message.Text;
            if (lonText == null || !double.TryParse(lonText, out var lon))
            {
                await client.SendTextMessageAsync(
                   chatId: update.Message!.Chat.Id,
                   text: "Введите долготу повторно",
                   cancellationToken: ct);
            }
            else
            {
                state.Long = lon;
                try
                {
                    await client.SendLocationAsync(
                         chatId: update.Message!.Chat.Id,
                         longitude: state.Long,
                         latitude: state.Lat);
                    await client.SendTextMessageAsync(
                        chatId: update.Message!.Chat.Id,
                        text: "Вот ваша точка",
                        cancellationToken: ct);
                    state.Mode = Mode.Initial;
                    currentUser.ChatMode = ChatMode.AskMenuCommand;
                }
                catch (Exception ex)
                {
                    await client.SendTextMessageAsync(
                       chatId: update.Message!.Chat.Id,
                       text: "Координаты некорректные",
                       cancellationToken: ct);
                    await SendInitial(client, update, state, ct);
                }
            }
        }

        private static async Task SendSetLat(ITelegramBotClient client, Update update, MapGenState state, CancellationToken ct)
        {
            var latText = update.Message.Text;
            if (latText == null || !double.TryParse(latText, out var lat))
            {
                await client.SendTextMessageAsync(
                   chatId: update.Message!.Chat.Id,
                   text: "Введите широту повторно",
                   cancellationToken: ct);
            }
            else
            {
                state.Lat = lat;
                await client.SendTextMessageAsync(
                      chatId: update.Message!.Chat.Id,
                      text: "Введите Долготу",
                      cancellationToken: ct);
                state.Mode = Mode.SetLon;
            }
        }

        private static async Task SendInitial(ITelegramBotClient client, Update update, MapGenState? state, CancellationToken ct)
        {
            await client.SendTextMessageAsync(
                chatId: update.Message!.Chat.Id,
                text: "Введите широту",
                cancellationToken: ct);
            state.Mode = Mode.SetLat;
        }
    }
}
