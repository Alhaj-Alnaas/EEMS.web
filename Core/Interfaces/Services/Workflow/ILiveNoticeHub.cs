namespace Core.Interfaces.Services.Workflow
{
    public sealed record LiveNotice(string UserId, string Title, string Body, Guid? PermitId);

    /// <summary>Pushes a saved notification to open screens in this process.</summary>
    public interface ILiveNoticeHub
    {
        event Action<LiveNotice>? Published;

        void Publish(LiveNotice notice);
    }
}
