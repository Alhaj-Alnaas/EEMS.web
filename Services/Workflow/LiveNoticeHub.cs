using Core.Interfaces.Services.Workflow;

namespace Services.Workflow
{
    public class LiveNoticeHub : ILiveNoticeHub
    {
        public event Action<LiveNotice>? Published;

        public void Publish(LiveNotice notice)
        {
            var handler = Published;
            if (handler == null)
                return;

            foreach (var subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((Action<LiveNotice>)subscriber)(notice);
                }
                catch
                {
                    // A closed screen must not stop the others.
                }
            }
        }
    }
}
