namespace NetworkCommunicator.Interfaces
{
    public interface IServiceNetworkClient<TCallbackService, TRemoteService>
        where TCallbackService : class
        where TRemoteService : class
    {
        TRemoteService RemoteService { get; }
        TCallbackService Service { get; }
    }
}