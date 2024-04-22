
namespace DiscogsKebakenHelper.Model
{
    public class Release
    {
        public int Uid { get; set; }

        public int? ChatId { get; set; }

        public string? Artist { get; set; }

        public string? ReleaseName { get; set; } = null;

        public string? ReleaseYear { get; set; } = null;

        public string? ReleaseId { get; set; } = null;

        public string? InstanceId { get; set; } = null;

        public string? StoragePlace { get; set; } = null;

        public string? Thumb { get; set; } = null;
    }
}
